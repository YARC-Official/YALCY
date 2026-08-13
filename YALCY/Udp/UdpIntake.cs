using System;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using ReactiveUI;
using YALCY.Safety;
using YALCY.ViewModels;
using YALCY.Views.Components;

namespace YALCY.Udp;

public partial class UdpIntake : ReactiveObject
{
    public const int LEGACY_PACKET_SIZE = 47;
    public const int MIN_PACKET_SIZE = LEGACY_PACKET_SIZE;
    private const int PLAYER_STAR_POWER_COUNT_SIZE = sizeof(ushort);
    private const int PLAYER_STAR_POWER_ENTRY_SIZE = 2;
    private const int V4_FIXED_PACKET_SIZE = LEGACY_PACKET_SIZE + PLAYER_STAR_POWER_COUNT_SIZE;
    private const int FOG_DURATION_SIZE = sizeof(ushort);
    private const int CURRENT_FIXED_PACKET_SIZE = (int)ByteIndexName.PlayerStarPowerCount + PLAYER_STAR_POWER_COUNT_SIZE;
    public event Action<byte[]> PacketProcessed;
    private MainWindowViewModel? _mainViewModel;
    private readonly List<PlayerStarPowerPacketMembers> _playerStarPowerMembers = new();
    private CancellationTokenSource? _fogDurationCts;
    private bool _rawFogState;
    private bool _effectiveFogState;
    private LightingSafetyController? _safetyController;
    public static int FogDurationPercent { get; set; } = 100;

    public interface IDatapacketMember
    {
        string Name { get; }
        int Index { get; }
        string ValueDescription { get; }
        object Value { get; }
    }


    public class DatapacketMember<T> : IDatapacketMember, INotifyPropertyChanged
    {
        private T _value;
        private readonly Func<T, string> _descriptionFunc;
        private readonly Action<T> _onValueChangedAction;

        public DatapacketMember(string name, int byteNumber, Func<T, string> descriptionFunc)
        {
            Name = name;
            Index = byteNumber;
            _descriptionFunc = descriptionFunc;
            ValueDescription = _descriptionFunc(default(T)); // Initialize with default description
        }

        public string Name { get; set; }
        public int Index { get; set; }
        public string ValueDescription { get; private set; }

        public T Value
        {
            get => _value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_value, value)) return;
                _value = value;
                ValueDescription = _descriptionFunc(value);
                OnPropertyChanged(nameof(Value));
                OnPropertyChanged(nameof(ValueDescription));
            }
        }

        object IDatapacketMember.Value => Value;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private sealed class PlayerStarPowerPacketMembers
    {
        public PlayerStarPowerPacketMembers(
            DatapacketMember<byte> amount,
            DatapacketMember<bool> isActive)
        {
            Amount = amount;
            IsActive = isActive;
        }

        public DatapacketMember<byte> Amount { get; }
        public DatapacketMember<bool> IsActive { get; }
    }

    public DatapacketMember<uint> Header { get; private set; } = new ("Header", (byte)ByteIndexName.Header, GetHeaderByteDescription);
    public DatapacketMember<byte> DatagramVersion { get; private set; } = new ("Datagram Version", (byte)ByteIndexName.DatagramVersion, GetDatagramVersionByteDescription);
    public DatapacketMember<byte> Platform { get; private set; } = new ("Platform", (byte)ByteIndexName.Platform, GetPlatformByteDescription);
    public DatapacketMember<byte> CurrentScene { get; private set; } = new ("scene", (byte)ByteIndexName.CurrentScene, GetSceneIndexByteDescription);
    public DatapacketMember<byte> Paused { get; private set; } = new ("Paused", (byte)ByteIndexName.PauseState, GetPauseByteDescription);
    public static DatapacketMember<byte> Venue { get; private set; } = new ("Venue", (byte)ByteIndexName.VenueSize, GetVenueSizeByteDescription);
    public static DatapacketMember<float> BeatsPerMinute { get; private set; } = new ("Beats per minute", (byte)ByteIndexName.BeatsPerMinute, value => $"{value}");
    public DatapacketMember<byte> CurrentSongSection { get; private set; } = new ("song section", (byte)ByteIndexName.SongSection, GetSongSectionByteDescription);
    public DatapacketMember<byte> CurrentGuitarNotes { get; private set; } = new ("Guitar notes", (byte)ByteIndexName.GuitarNotes, GetInstrumentByteDescription);
    public DatapacketMember<byte> CurrentBassNotes { get; private set; } = new ("Bass notes", (byte)ByteIndexName.BassNotes, GetInstrumentByteDescription);
    public DatapacketMember<byte> CurrentDrumNotes { get; private set; } = new ("Drum notes", (byte)ByteIndexName.DrumsNotes, GetDrumsByteDescription);
    public DatapacketMember<byte> CurrentKeysNotes { get; private set; } = new ("Keys notes", (byte)ByteIndexName.KeysNotes, GetInstrumentByteDescription);
    public DatapacketMember<float> CurrentVocalNote { get; private set; } = new ("Vocal note", (byte)ByteIndexName.VocalsNote, GetVocalHarmonyByteDescription);
    public DatapacketMember<float> CurrentHarmony0Note { get; private set; } = new ("Harmony 0 note", (byte)ByteIndexName.Harmony0Note, GetVocalHarmonyByteDescription);
    public DatapacketMember<float> CurrentHarmony1Note { get; private set; } = new ("Harmony 1 note", (byte)ByteIndexName.Harmony1Note, GetVocalHarmonyByteDescription);
    public DatapacketMember<float> CurrentHarmony2Note { get; private set; } = new ("Harmony 2 note", (byte)ByteIndexName.Harmony2Note, GetVocalHarmonyByteDescription);
    public DatapacketMember<byte> LightingCue { get; private set; } = new ("Lighting cue", (byte)ByteIndexName.LightingCue, GetCueByteDescription);
    public DatapacketMember<byte> PostProcessing { get; private set; } = new ("Post processing", (byte)ByteIndexName.PostProcessing, GetPostProcessingByteDescription);
    public DatapacketMember<bool> FogState { get; private set; } = new ("Fog state", (byte)ByteIndexName.FogState, GetFogStateByteDescription);
    public DatapacketMember<ushort> FogRemainingCentiseconds { get; private set; } = new ("Fog remaining", (byte)ByteIndexName.FogRemainingCentiseconds, GetFogRemainingCentisecondsDescription);
    public DatapacketMember<byte> StrobeState { get; private set; } = new ("Strobe state", (byte)ByteIndexName.StrobeState, GetStrobeByteDescription);
    public DatapacketMember<byte> Beat { get; private set; } = new ("Beat", (byte)ByteIndexName.Beat, GetBeatlineByteDescription);
    public DatapacketMember<byte> Keyframe { get; private set; } = new ("Keyframe", (byte)ByteIndexName.Keyframe, GetKeyFrameDescription);
    public DatapacketMember<bool> BonusEffect { get; private set; } = new ("Bonus effect", (byte)ByteIndexName.BonusEffect, GetBonusEffectByteDescription);
    public DatapacketMember<bool> AutoGen { get; private set; } = new ("AutoGen track", (byte)ByteIndexName.AutoGen, GetAutoGenByteDescription);
    public DatapacketMember<byte> Spotlight { get; private set; } = new ("Spotlight", (byte)ByteIndexName.Spotlight, GetPerformerDescription);
    public DatapacketMember<byte> Singalong { get; private set; } = new ("Singalong", (byte)ByteIndexName.Singalong, GetPerformerDescription);

    public DatapacketMember<byte> CameraCutConstraint { get; private set; } = new("Camera cut constraint",
        (byte)ByteIndexName.CameraCutConstraint, GetCameraCutConstraintDescription);

    public DatapacketMember<byte> CameraCutPriority { get; private set; } = new("Camera cut Priority",
        (byte)ByteIndexName.CameraCutPriority, GetCameraCutPriorityDescription);


    public DatapacketMember<byte> CameraCutSubject { get; private set; } = new("Camera cut subject",
        (byte)ByteIndexName.CameraCutSubject, GetCameraCutSubjectDescription);

    public DatapacketMember<ushort> PlayerStarPowerCount { get; private set; } = new("Player count",
        (byte)ByteIndexName.PlayerStarPowerCount, GetPlayerStarPowerCountDescription);


    private static UdpClient? _udpClient;
    private static CancellationTokenSource? _cancellationTokenSource;
    private static Timer? _healthCheckTimer;
    private const int HEALTH_CHECK_INTERVAL_MS = 1000; // Check once per second

    internal void SetSafetyController(LightingSafetyController safetyController)
    {
        _safetyController = safetyController;
    }

    public async Task EnableUdpIntake(bool isEnabled, MainWindowViewModel? viewModel = null)
    {
        if (viewModel != null)
        {
            _mainViewModel = viewModel;
        }

        if (_mainViewModel == null)
        {
            Console.WriteLine("UdpIntake: No ViewModel provided and none cached.");
            return;
        }

        if (isEnabled)
        {
            StatusFooter.UpdateStatus("UDP", IntegrationStatus.Connecting);
            if (_udpClient != null)
            {
                Console.WriteLine("UDP client already running.");
                return;
            }

            try
            {
                Console.WriteLine($"Starting UDP client on port {_mainViewModel.UdpListenPort}");
                _udpClient = new UdpClient(_mainViewModel.UdpListenPort);
                _udpClient.Client.ReceiveBufferSize = 8192; // Increase buffer size
                
                _healthCheckTimer = new Timer(HealthCheckCallback, this, HEALTH_CHECK_INTERVAL_MS, HEALTH_CHECK_INTERVAL_MS);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing UDP client: {ex.Message}");
                StatusFooter.UpdateStatus("UDP", IntegrationStatus.Error);
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            await Task.Run(async () =>
            {
                try
                {
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        var result = await _udpClient.ReceiveAsync().ConfigureAwait(false);

                        DeserializePacket(result.Buffer);
                    }
                }
                catch (ObjectDisposedException)
                {
                    Console.WriteLine("UdpClient has been disposed.");
                    StatusFooter.UpdateStatus("UDP", IntegrationStatus.Error);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
                {
                    Console.WriteLine("UdpClient operation aborted.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error receiving UDP data: {ex.Message}");
                    StatusFooter.UpdateStatus("UDP", IntegrationStatus.Error);
                }
            }, _cancellationTokenSource.Token);
        }
        else
        {
            StopUdpClient();
        }
    }

    public void DeserializePacket(byte[] data)
    {
        TryDeserializePacket(data);
    }

    internal bool TryDeserializePacket(byte[] data)
    {
        if (!TryValidatePacket(data, out var validationError))
        {
            Console.WriteLine($"Invalid UDP packet: {validationError}");
            Console.WriteLine($"Bad UDP packet details: {DescribePacket(data)}");
            return false;
        }

        try
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(ms))
            {
            Header.Value = reader.ReadUInt32(); // byte count: 4
            DatagramVersion.Value = reader.ReadByte(); // 5
            UpdateVersionedMemberIndexes(DatagramVersion.Value >= (byte)DatagramVersionByte.FogRemainingDuration);
            Platform.Value = reader.ReadByte(); // 6
            CurrentScene.Value = reader.ReadByte(); // 7
            Paused.Value = reader.ReadByte(); // 8
            Venue.Value = reader.ReadByte(); // 9
            BeatsPerMinute.Value = reader.ReadSingle(); // 10-13
            CurrentSongSection.Value = reader.ReadByte(); // 14

            CurrentGuitarNotes.Value = reader.ReadByte(); // 15
            CurrentBassNotes.Value = reader.ReadByte(); // 16
            CurrentDrumNotes.Value = reader.ReadByte(); // 17
            CurrentKeysNotes.Value = reader.ReadByte(); // 18

            CurrentVocalNote.Value = reader.ReadSingle(); // 19-22
            CurrentHarmony0Note.Value = reader.ReadSingle(); // 23-26
            CurrentHarmony1Note.Value = reader.ReadSingle(); // 27-30
            CurrentHarmony2Note.Value = reader.ReadSingle(); // 31-34

            LightingCue.Value = reader.ReadByte(); // 35
            PostProcessing.Value = reader.ReadByte(); // 36
            var fogState = reader.ReadBoolean(); // Offset 36
            if (DatagramVersion.Value >= (byte)DatagramVersionByte.FogRemainingDuration)
            {
                FogRemainingCentiseconds.Value = reader.ReadUInt16(); // Offsets 37-38 in v5+
            }
            else
            {
                FogRemainingCentiseconds.Value = ushort.MaxValue;
            }
            FogState.Value = fogState;
            UpdateEffectiveFogState(fogState, FogRemainingCentiseconds.Value);
            StrobeState.Value = reader.ReadByte();
            Beat.Value = reader.ReadByte(); // 39
            Keyframe.Value = reader.ReadByte(); // 40
            BonusEffect.Value = reader.ReadBoolean(); // 41

            AutoGen.Value = reader.ReadBoolean(); // 42
            Spotlight.Value = reader.ReadByte(); // 43
            Singalong.Value = reader.ReadByte(); // 44
            CameraCutConstraint.Value = reader.ReadByte(); //45
            CameraCutPriority.Value = reader.ReadByte(); //46
            CameraCutSubject.Value = reader.ReadByte(); //47

            if (DatagramVersion.Value >= (byte)DatagramVersionByte.PlayerStarPower)
            {
                var fixedPacketSize = DatagramVersion.Value >= (byte)DatagramVersionByte.FogRemainingDuration
                    ? CURRENT_FIXED_PACKET_SIZE
                    : V4_FIXED_PACKET_SIZE;
                PlayerStarPowerCount.Value = reader.ReadUInt16();
                EnsurePlayerStarPowerMemberCount(PlayerStarPowerCount.Value, fixedPacketSize);

                for (var i = 0; i < PlayerStarPowerCount.Value; i++)
                {
                    var playerStarPower = _playerStarPowerMembers[i];
                    playerStarPower.Amount.Value = reader.ReadByte();
                    playerStarPower.IsActive.Value = reader.ReadByte() != 0;
                }
            }
            else
            {
                PlayerStarPowerCount.Value = 0;
                EnsurePlayerStarPowerMemberCount(0);
            }

            }

            PacketProcessed?.Invoke(data);
            _safetyController?.NotifyValidPacket();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading UDP data: {ex.Message}");
            Console.WriteLine($"Bad UDP packet details: {DescribePacket(data)}");
            return false;
        }
    }

    internal static bool TryValidatePacket(ReadOnlySpan<byte> data, out string validationError)
    {
        if (data.Length < sizeof(uint) + sizeof(byte))
        {
            validationError = $"packet is too short ({data.Length} bytes)";
            return false;
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(data) != PACKET_HEADER)
        {
            validationError = "packet header is not YARG";
            return false;
        }

        var version = data[(int)ByteIndexName.DatagramVersion];
        var fixedPacketSize = version switch
        {
            >= (byte)DatagramVersionByte.FogRemainingDuration => CURRENT_FIXED_PACKET_SIZE,
            >= (byte)DatagramVersionByte.PlayerStarPower => V4_FIXED_PACKET_SIZE,
            _ => LEGACY_PACKET_SIZE
        };

        if (data.Length < fixedPacketSize)
        {
            validationError = $"packet is incomplete ({data.Length} bytes, expected at least {fixedPacketSize})";
            return false;
        }

        var expectedPacketSize = fixedPacketSize;
        if (version >= (byte)DatagramVersionByte.PlayerStarPower)
        {
            var playerCountOffset = fixedPacketSize - PLAYER_STAR_POWER_COUNT_SIZE;
            var playerCount = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(playerCountOffset, PLAYER_STAR_POWER_COUNT_SIZE));
            expectedPacketSize = GetExpectedPacketSize(playerCount, fixedPacketSize);
        }

        if (data.Length != expectedPacketSize)
        {
            validationError = $"packet size is {data.Length} bytes; expected exactly {expectedPacketSize}";
            return false;
        }

        validationError = string.Empty;
        return true;
    }

    private static string DescribePacket(byte[] data)
    {
        var header = data.Length >= sizeof(uint)
            ? $"0x{BitConverter.ToUInt32(data, 0):X8}"
            : "n/a";
        var datagramVersion = data.Length > (int)ByteIndexName.DatagramVersion
            ? data[(int)ByteIndexName.DatagramVersion].ToString()
            : "n/a";
        var playerStarPowerCount = data.Length >= CURRENT_FIXED_PACKET_SIZE
            ? BitConverter.ToUInt16(data, (int)ByteIndexName.PlayerStarPowerCount).ToString()
            : "n/a";
        var previewLength = Math.Min(data.Length, 16);
        var preview = previewLength > 0
            ? BitConverter.ToString(data, 0, previewLength)
            : "<empty>";

        return $"length={data.Length}, minAccepted={MIN_PACKET_SIZE}, fixedParserBytes={CURRENT_FIXED_PACKET_SIZE}, header={header}, expectedHeader=0x{PACKET_HEADER:X8}, datagramVersion={datagramVersion}, playerStarPowerCount={playerStarPowerCount}, preview={preview}";
    }

    private static int GetExpectedPacketSize(ushort playerStarPowerCount, int fixedPacketSize)
    {
        return fixedPacketSize + (playerStarPowerCount * PLAYER_STAR_POWER_ENTRY_SIZE);
    }

    private void UpdateVersionedMemberIndexes(bool hasFogDuration)
    {
        var offset = hasFogDuration ? 0 : -FOG_DURATION_SIZE;
        StrobeState.Index = (int)ByteIndexName.StrobeState + offset;
        Beat.Index = (int)ByteIndexName.Beat + offset;
        Keyframe.Index = (int)ByteIndexName.Keyframe + offset;
        BonusEffect.Index = (int)ByteIndexName.BonusEffect + offset;
        AutoGen.Index = (int)ByteIndexName.AutoGen + offset;
        Spotlight.Index = (int)ByteIndexName.Spotlight + offset;
        Singalong.Index = (int)ByteIndexName.Singalong + offset;
        CameraCutConstraint.Index = (int)ByteIndexName.CameraCutConstraint + offset;
        CameraCutPriority.Index = (int)ByteIndexName.CameraCutPriority + offset;
        CameraCutSubject.Index = (int)ByteIndexName.CameraCutSubject + offset;
        PlayerStarPowerCount.Index = (int)ByteIndexName.PlayerStarPowerCount + offset;
    }

    private void EnsurePlayerStarPowerMemberCount(ushort playerStarPowerCount, int fixedPacketSize = V4_FIXED_PACKET_SIZE)
    {
        for (var i = 0; i < _playerStarPowerMembers.Count; i++)
        {
            var packetIndex = fixedPacketSize + (i * PLAYER_STAR_POWER_ENTRY_SIZE);
            _playerStarPowerMembers[i].Amount.Index = packetIndex;
            _playerStarPowerMembers[i].IsActive.Index = packetIndex + 1;
        }

        while (_playerStarPowerMembers.Count < playerStarPowerCount)
        {
            var playerIndex = _playerStarPowerMembers.Count;
            var packetIndex = fixedPacketSize + (playerIndex * PLAYER_STAR_POWER_ENTRY_SIZE);
            var playerNumber = playerIndex + 1;
            var members = new PlayerStarPowerPacketMembers(
                new DatapacketMember<byte>($"Player {playerNumber} star power", packetIndex, GetPlayerStarPowerAmountDescription),
                new DatapacketMember<bool>($"Player {playerNumber} star power active", packetIndex + 1, GetPlayerStarPowerActiveDescription));

            _playerStarPowerMembers.Add(members);
            AddPlayerStarPowerMemberToView(members.Amount);
            AddPlayerStarPowerMemberToView(members.IsActive);
        }

        while (_playerStarPowerMembers.Count > playerStarPowerCount)
        {
            var memberIndex = _playerStarPowerMembers.Count - 1;
            var members = _playerStarPowerMembers[memberIndex];
            _playerStarPowerMembers.RemoveAt(memberIndex);
            RemovePlayerStarPowerMemberFromView(members.Amount);
            RemovePlayerStarPowerMemberFromView(members.IsActive);
        }
    }

    private void AddPlayerStarPowerMemberToView(IDatapacketMember member)
    {
        if (_mainViewModel == null)
        {
            return;
        }

        void Add()
        {
            if (!_mainViewModel.CombinedCollection.Contains(member))
            {
                _mainViewModel.CombinedCollection.Add(member);
            }
        }

        if (Application.Current?.ApplicationLifetime == null)
        {
            Add();
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Add();
        }
        else
        {
            Dispatcher.UIThread.Post(Add);
        }
    }

    private void UpdateEffectiveFogState(bool fogState, ushort remainingCentiseconds)
    {
        if (!fogState)
        {
            _rawFogState = false;
            _fogDurationCts?.Cancel();
            SetEffectiveFogState(false);
            return;
        }

        if (_rawFogState)
        {
            return;
        }

        _rawFogState = true;
        if (FogDurationPercent == 0 || remainingCentiseconds == 0)
        {
            SetEffectiveFogState(false);
            return;
        }

        SetEffectiveFogState(true);
        if (remainingCentiseconds == ushort.MaxValue)
        {
            return;
        }

        _fogDurationCts?.Cancel();
        _fogDurationCts?.Dispose();
        _fogDurationCts = new CancellationTokenSource();
        var token = _fogDurationCts.Token;
        var duration = TimeSpan.FromMilliseconds(remainingCentiseconds * 10.0 * FogDurationPercent / 100.0);
        _ = TurnFogOffAfterAsync(duration, token);
    }

    private async Task TurnFogOffAfterAsync(TimeSpan duration, CancellationToken token)
    {
        try
        {
            await Task.Delay(duration, token).ConfigureAwait(false);
            if (!token.IsCancellationRequested && _rawFogState)
            {
                SetEffectiveFogState(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void SetEffectiveFogState(bool value)
    {
        if (_effectiveFogState == value)
        {
            return;
        }

        _effectiveFogState = value;
        OnFogState?.Invoke(value);
    }

    internal void EnterSafetyBlackout()
    {
        _fogDurationCts?.Cancel();
        _fogDurationCts?.Dispose();
        _fogDurationCts = null;
        _rawFogState = false;
        _effectiveFogState = false;
    }

    internal void ReplayOutputState()
    {
        OnLightingCue?.Invoke(LightingCue.Value);
        OnStrobeState?.Invoke(StrobeState.Value);
        OnFogState?.Invoke(_effectiveFogState);
    }

    private void RemovePlayerStarPowerMemberFromView(IDatapacketMember member)
    {
        if (_mainViewModel == null)
        {
            return;
        }

        void Remove()
        {
            _mainViewModel.CombinedCollection.Remove(member);
        }

        if (Application.Current?.ApplicationLifetime == null)
        {
            Remove();
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Remove();
        }
        else
        {
            Dispatcher.UIThread.Post(Remove);
        }
    }

    private static void HealthCheckCallback(object? state)
    {
        try
        {
            if (state is UdpIntake intake && _udpClient != null &&
                !_cancellationTokenSource?.Token.IsCancellationRequested == true)
            {
                intake._safetyController?.CheckForTimeout();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in UDP health check: {ex.Message}");
        }
    }

    private void StopUdpClient()
    {
        try
        {
            _safetyController?.NotifyStreamStopped();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _healthCheckTimer?.Dispose();
            _healthCheckTimer = null;
            _udpClient?.Close();
            _udpClient?.Dispose();
            _udpClient = null;
            StatusFooter.UpdateStatus("UDP", IntegrationStatus.Off);
            //        ClearByteIndexes();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping UDP client: {ex.Message}");
            StatusFooter.UpdateStatus("UDP", IntegrationStatus.Error);
        }
    }
}
