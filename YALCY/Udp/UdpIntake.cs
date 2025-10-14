using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using ReactiveUI;
using YALCY.Views.Components;

namespace YALCY.Udp;

public partial class UdpIntake : ReactiveObject
{
    public Action<byte[]> PacketProcessed;

    public interface IDatapacketMember
    {
        string Name { get; }
        byte Index { get; }
        string ValueDescription { get; }
        object Value { get; }
    }


    public class DatapacketMember<T> : IDatapacketMember, INotifyPropertyChanged
    {
        private T _value;
        private readonly Func<T, string> _descriptionFunc;
        private readonly Action<T> _onValueChangedAction;

        public DatapacketMember(string name, byte byteNumber, Func<T, string> descriptionFunc)
        {
            Name = name;
            Index = byteNumber;
            _descriptionFunc = descriptionFunc;
            ValueDescription = _descriptionFunc(default(T)); // Initialize with default description
        }

        public string Name { get; set; }
        public byte Index { get; set; }
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public DatapacketMember<uint> Header { get; private set; } = new ("Header", 0, GetHeaderByteDescription);
    public DatapacketMember<byte> DatagramVersion { get; private set; } = new ("Datagram Version", 1, GetDatagramVersionByteDescription);
    public DatapacketMember<byte> Platform { get; private set; } = new ("Platform", 2, GetPlatformByteDescription);
    public DatapacketMember<byte> CurrentScene { get; private set; } = new ("scene", 3, GetSceneIndexByteDescription);
    public DatapacketMember<byte> Paused { get; private set; } = new ("Paused", 4, GetPauseByteDescription);
    public static DatapacketMember<byte> Venue { get; private set; } = new ("Venue", 5, GetVenueSizeByteDescription);
    public static DatapacketMember<float> BeatsPerMinute { get; private set; } = new ("Beats per minute", 6, value => $"{value}");
    public DatapacketMember<byte> CurrentSongSection { get; private set; } = new ("song section", 7, GetSongSectionByteDescription);
    public DatapacketMember<byte> CurrentGuitarNotes { get; private set; } = new ("Guitar notes", 8, GetInstrumentByteDescription);
    public DatapacketMember<byte> CurrentBassNotes { get; private set; } = new ("Bass notes", 9, GetInstrumentByteDescription);
    public DatapacketMember<byte> CurrentDrumNotes { get; private set; } = new ("Drum notes", 10, GetDrumsByteDescription);
    public DatapacketMember<byte> CurrentKeysNotes { get; private set; } = new ("Keys notes", 11, GetInstrumentByteDescription);
    public DatapacketMember<float> CurrentVocalNote { get; private set; } = new ("Vocal note", 12, GetVocalHarmonyByteDescription);
    public DatapacketMember<float> CurrentHarmony0Note { get; private set; } = new ("Harmony 0 note", 13, GetVocalHarmonyByteDescription);
    public DatapacketMember<float> CurrentHarmony1Note { get; private set; } = new ("Harmony 1 note", 14, GetVocalHarmonyByteDescription);
    public DatapacketMember<float> CurrentHarmony2Note { get; private set; } = new ("Harmony 2 note", 15, GetVocalHarmonyByteDescription);
    public DatapacketMember<byte> LightingCue { get; private set; } = new ("Lighting cue", 16, GetCueByteDescription);
    public DatapacketMember<byte> PostProcessing { get; private set; } = new ("Post processing", 17, GetPostProcessingByteDescription);
    public DatapacketMember<bool> FogState { get; private set; } = new ("Fog state", 18, GetFogStateByteDescription);
    public DatapacketMember<byte> StrobeState { get; private set; } = new ("Strobe state", 19, GetStrobeByteDescription);
    public DatapacketMember<byte> Beat { get; private set; } = new ("Beat", 20, GetBeatlineByteDescription);
    public DatapacketMember<byte> Keyframe { get; private set; } = new ("Keyframe", 21, GetKeyFrameDescription);
    public DatapacketMember<bool> BonusEffect { get; private set; } = new ("Bonus effect", 22, GetBonusEffectByteDescription);
    public DatapacketMember<bool> AutoGen { get; private set; } = new ("AutoGen track", 23, GetAutoGenByteDescription);
    public DatapacketMember<byte> Spotlight { get; private set; } = new ("Spotlight", 24, GetPerformerDescription);
    public DatapacketMember<byte> Singalong { get; private set; } = new ("Singalong", 25, GetPerformerDescription);

    public static byte[] Buffer = new byte[Enum.GetValues<ByteIndexName>().Length]; // The current data buffer

    private static UdpClient? _udpClient;
    private static CancellationTokenSource? _cancellationTokenSource;
    private static DateTime _lastPacketReceived = DateTime.MinValue;
    private static Timer? _healthCheckTimer;
    private const int HEALTH_CHECK_INTERVAL_MS = 1000; // Verifica a cada 1 segundo
    private const int PACKET_TIMEOUT_MS = 3000; // Timeout de 3 segundos

    public async Task EnableUdpIntake(bool isEnabled)
    {
        var app = (App)Application.Current!;
        var mainViewModel = app.MainViewModel;

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
                Console.WriteLine($"Starting UDP client on port {mainViewModel.UdpListenPort}");
                _udpClient = new UdpClient(mainViewModel.UdpListenPort);
                _udpClient.Client.ReceiveBufferSize = 8192; // Increase buffer size
                
                // Inicializa o timer de verificação de saúde da conexão
                _lastPacketReceived = DateTime.Now;
                _healthCheckTimer = new Timer(HealthCheckCallback, null, HEALTH_CHECK_INTERVAL_MS, HEALTH_CHECK_INTERVAL_MS);
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

                        // Atualiza o timestamp do último pacote recebido
                        _lastPacketReceived = DateTime.Now;

                        // Process packets in a separate task
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
    const int MIN_PACKET_SIZE = 44;

    if (data.Length < MIN_PACKET_SIZE)
    {
        Console.WriteLine($"Invalid packet size: {data.Length} (expected at least {MIN_PACKET_SIZE})");
        return;
    }

    try
    {
        using (MemoryStream ms = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(ms))
        {
            Header.Value = reader.ReadUInt32(); // byte count: 4

            if (Header.Value != PACKET_HEADER)
            {
                Console.WriteLine($"Invalid packet header: {Header.Value}");
                return;
            }

            DatagramVersion.Value = reader.ReadByte(); //5
            Platform.Value = reader.ReadByte(); //6
            CurrentScene.Value = reader.ReadByte(); //7
            Paused.Value = reader.ReadByte(); //8
            Venue.Value = reader.ReadByte(); //9
            BeatsPerMinute.Value = reader.ReadSingle(); //10-13
            CurrentSongSection.Value = reader.ReadByte(); //14

            CurrentGuitarNotes.Value = reader.ReadByte(); //15
            CurrentBassNotes.Value = reader.ReadByte(); //16
            CurrentDrumNotes.Value = reader.ReadByte(); //17
            CurrentKeysNotes.Value = reader.ReadByte(); //18

            CurrentVocalNote.Value = reader.ReadSingle(); //19-22
            CurrentHarmony0Note.Value = reader.ReadSingle(); //23-26
            CurrentHarmony1Note.Value = reader.ReadSingle(); //27-30
            CurrentHarmony2Note.Value = reader.ReadSingle(); //31-34

            LightingCue.Value = reader.ReadByte(); //35
            PostProcessing.Value = reader.ReadByte(); //36
            FogState.Value = reader.ReadBoolean(); //37
            StrobeState.Value = reader.ReadByte(); //38
            Beat.Value = reader.ReadByte(); //39
            Keyframe.Value = reader.ReadByte(); //40
            BonusEffect.Value = reader.ReadBoolean(); //41

            AutoGen.Value = reader.ReadBoolean(); //42
            Spotlight.Value = reader.ReadByte(); //43
            Singalong.Value = reader.ReadByte(); //44
        }

        PacketProcessed?.Invoke(Buffer);
        StatusFooter.UpdateStatus("UDP", IntegrationStatus.Connected);
    }
    catch (EndOfStreamException ex)
    {
        Console.WriteLine($"Error reading UDP data (incomplete packet): {ex.Message}");
        StatusFooter.UpdateStatus("UDP", IntegrationStatus.Error);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error reading UDP data: {ex.Message}");
        StatusFooter.UpdateStatus("UDP", IntegrationStatus.Error);
    }
}

    /// <summary>
    /// Callback do timer que verifica se dados foram recebidos nos últimos 3 segundos
    /// Se não houver dados recentes, marca o status como Error
    /// </summary>
    private static void HealthCheckCallback(object? state)
    {
        try
        {
            if (_udpClient != null && !_cancellationTokenSource?.Token.IsCancellationRequested == true)
            {
                var timeSinceLastPacket = DateTime.Now - _lastPacketReceived;
                
                if (timeSinceLastPacket.TotalMilliseconds > PACKET_TIMEOUT_MS)
                {
                    // Não recebeu dados nos últimos 3 segundos - marca como erro
                    StatusFooter.UpdateStatus("UDP", IntegrationStatus.Error);
                    Console.WriteLine($"UDP health check failed: No data received for {timeSinceLastPacket.TotalMilliseconds:F0}ms");
                }
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
            _cancellationTokenSource?.Cancel();
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

    /*
    private static void ClearByteIndexes()
    {
        foreach (var byteIndex in MainWindowViewModel.ByteIndexes)
        {
            byteIndex.CurrentValue = 0;
            byteIndex.ValueDescription = string.Empty;
        }
    }
    */
}
