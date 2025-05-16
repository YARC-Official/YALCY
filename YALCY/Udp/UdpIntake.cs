using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using ReactiveUI;

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
        private readonly Action<T> _onValueChangedAction; // Added action for value change

        public DatapacketMember(string name, byte byteNumber, Func<T, string> descriptionFunc)
        {
            Name = name;
            Index = byteNumber;
            _descriptionFunc = descriptionFunc;
            ValueDescription = _descriptionFunc(default(T)); // Initialize with default description
        }

        public string Name { get; set; }
        public byte Index { get; set; }
        public string ValueDescription { get; private set; } // Made setter private to prevent external modification

        public T Value
        {
            get => _value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_value, value)) return;
                _value = value;
                ValueDescription = _descriptionFunc(value);
                OnPropertyChanged(nameof(Value));
                OnPropertyChanged(nameof(ValueDescription)); // Notify for ValueDescription as well
            }
        }

        object IDatapacketMember.Value => Value; // Explicit implementation for the interface

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
    public DatapacketMember<byte> Performer { get; private set; } = new ("Performer", 20, value => $"{value}");
    public DatapacketMember<byte> Beat { get; private set; } = new ("Beat", 21, GetBeatlineByteDescription);
    public DatapacketMember<byte> Keyframe { get; private set; } = new ("Keyframe", 22, GetKeyFrameDescription);
    public DatapacketMember<bool> BonusEffect { get; private set; } = new ("Bonus effect", 23, GetBonusEffectByteDescription);

    public static byte[] Buffer = new byte[Enum.GetValues<ByteIndexName>().Length]; // The current data buffer

    private static UdpClient? _udpClient;
    private static CancellationTokenSource? _cancellationTokenSource;

    public async Task EnableUdpIntake(bool isEnabled)
    {
        var app = (App)Application.Current!;
        var mainViewModel = app.MainViewModel;

        if (isEnabled)
        {
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing UDP client: {ex.Message}");
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

                        // Process packets in a separate task
                        //ProcessPacket(result.Buffer);
                        DeserializePacket(result.Buffer);
                    }
                }
                catch (ObjectDisposedException)
                {
                    Console.WriteLine("UdpClient has been disposed.");
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
                {
                    Console.WriteLine("UdpClient operation aborted.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error receiving UDP data: {ex.Message}");
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
        using (MemoryStream ms = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(ms))
        {
            Header.Value = reader.ReadUInt32();

            // Check if the header is correct (replace `EXPECTED_HEADER_VALUE` with the actual expected value)
            if (Header.Value != PACKET_HEADER) // Y A R G
            {
                // If the header is incorrect, stop reading the packet and return null or handle it accordingly
                Console.WriteLine("Invalid packet header.");
                return;
            }
            DatagramVersion.Value = reader.ReadByte();
            Platform.Value = reader.ReadByte();
            CurrentScene.Value = reader.ReadByte();
            Paused.Value = reader.ReadByte();
            Venue.Value = reader.ReadByte();
            BeatsPerMinute.Value = reader.ReadSingle();
            CurrentSongSection.Value = reader.ReadByte();

            CurrentGuitarNotes.Value = reader.ReadByte();
            CurrentBassNotes.Value = reader.ReadByte();
            CurrentDrumNotes.Value = reader.ReadByte();
            CurrentKeysNotes.Value = reader.ReadByte();

            CurrentVocalNote.Value = reader.ReadSingle();
            CurrentHarmony0Note.Value = reader.ReadSingle();
            CurrentHarmony1Note.Value = reader.ReadSingle();
            CurrentHarmony2Note.Value = reader.ReadSingle();

            LightingCue.Value = reader.ReadByte();
            PostProcessing.Value = reader.ReadByte();
            FogState.Value = reader.ReadBoolean();
            StrobeState.Value = reader.ReadByte();
            Performer.Value = reader.ReadByte();
            Beat.Value = reader.ReadByte();
            Keyframe.Value = reader.ReadByte();
            BonusEffect.Value = reader.ReadBoolean();
        }

        PacketProcessed?.Invoke(Buffer);
    }

    private void StopUdpClient()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            _udpClient?.Close();
            _udpClient?.Dispose();
            _udpClient = null;

            //        ClearByteIndexes();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping UDP client: {ex.Message}");
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
