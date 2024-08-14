using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using ReactiveUI;
using YALCY.ViewModels;

namespace YALCY.Udp;

public partial class UdpIntake : ReactiveObject
{
    public static byte[] Buffer = new byte[Enum.GetValues<ByteIndexName>().Length]; // The current data buffer

    private static byte[]
        _previousBuffer = new byte[Enum.GetValues<ByteIndexName>().Length]; // The previous data buffer

    private static UdpClient? _udpClient;
    private static CancellationTokenSource? _cancellationTokenSource;
    public Action<byte[]> PacketProcessed;

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
                        ProcessPacket(result.Buffer);
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

    private void ProcessPacket(byte[] packet)
    {
        if (packet.Length == Enum.GetValues<ByteIndexName>().Length)
        {
            _previousBuffer = Buffer;
            Buffer = packet;
            for (int i = 0; i < Buffer.Length; i++)
            {
                switch (i)
                {
                    case (int)ByteIndexName.Beat:
                        HandleBeatByte((BeatByte)Buffer[i]);
                        break;

                    case (int)ByteIndexName.Keyframe:
                        HandleKeyFrameCueEByte((KeyFrameByte)Buffer[i]);
                        break;

                    case (int)ByteIndexName.DrumsNotes:
                        HandleDrumByte((DrumNotesByte)Buffer[i]);
                        break;

                    case (int)ByteIndexName.VocalsNote:
                        HandleVocalHarmonyByte((VocalHarmonyBytes)Buffer[i]);
                        break;

                    case (int)ByteIndexName.LightingCue:
                        HandleLightingCueByte((CueByte)Buffer[i]);
                        break;

                    case (int)ByteIndexName.FogState:
                        HandleFogByte((FogStateByte)Buffer[i]);
                        break;

                    case (int)ByteIndexName.StrobeState:
                        HandleStrobeByte((StrobeSpeedByte)Buffer[i]);
                        break;

                    default:
                        // Console.WriteLine($"Unhandled byte at index {i}: {Buffer[i]}");
                        break;
                }

                if (MainWindowViewModel.ByteIndexes == null) continue;
                MainWindowViewModel.ByteIndexes[i].CurrentValue = Buffer[i];
                MainWindowViewModel.ByteIndexes[i].ValueDescription = i switch
                {
                    (int)ByteIndexName.HeaderByte1 => GetHeaderByteDescription(Buffer[i]),
                    (int)ByteIndexName.HeaderByte2 => GetHeaderByteDescription(Buffer[i]),
                    (int)ByteIndexName.HeaderByte3 => GetHeaderByteDescription(Buffer[i]),
                    (int)ByteIndexName.HeaderByte4 => GetHeaderByteDescription(Buffer[i]),
                    (int)ByteIndexName.DatagramVersion => GetDatagramVersionByteDescription(Buffer[i]),
                    (int)ByteIndexName.Platform => GetPlatformByteDescription(Buffer[i]),
                    (int)ByteIndexName.CurrentScene => GetSceneIndexByteDescription(Buffer[i]),
                    (int)ByteIndexName.PauseState => GetPauseByteDescription(Buffer[i]),
                    (int)ByteIndexName.VenueSize => GetVenueSizeByteDescription(Buffer[i]),
                    (int)ByteIndexName.BeatsPerMinute => "Beats per minute",
                    (int)ByteIndexName.SongSection => GetSongSectionByteDescription(Buffer[i]),
                    (int)ByteIndexName.GuitarNotes => GetInstrumentByteDescription(Buffer[i]),
                    (int)ByteIndexName.BassNotes => GetInstrumentByteDescription(Buffer[i]),
                    (int)ByteIndexName.DrumsNotes => GetDrumsByteDescription(Buffer[i]),
                    (int)ByteIndexName.KeysNotes => GetInstrumentByteDescription(Buffer[i]),
                    (int)ByteIndexName.VocalsNote => GetVocalHarmonyByteDescription(Buffer[i]),
                    (int)ByteIndexName.Harmony0Note => GetVocalHarmonyByteDescription(Buffer[i]),
                    (int)ByteIndexName.Harmony1Note => GetVocalHarmonyByteDescription(Buffer[i]),
                    (int)ByteIndexName.Harmony2Note => GetVocalHarmonyByteDescription(Buffer[i]),
                    (int)ByteIndexName.LightingCue => GetCueByteDescription(Buffer[i]),
                    (int)ByteIndexName.PostProcessing => GetPostProcessingByteDescription(Buffer[i]),
                    (int)ByteIndexName.FogState => GetFogStateByteDescription(Buffer[i]),
                    (int)ByteIndexName.StrobeState => GetStrobeByteDescription(Buffer[i]),
                    (int)ByteIndexName.Performer => "Not implemented yet",
                    (int)ByteIndexName.Beat => GetBeatlineByteDescription((BeatByte)Buffer[i]),
                    (int)ByteIndexName.Keyframe => GetKeyFrameDescription(Buffer[i]),
                    (int)ByteIndexName.BonusEffect => GetBonusEffectByteDescription(Buffer[i]),
                    _ => Buffer[i].ToString()
                };
            }

            PacketProcessed?.Invoke(Buffer);
        }
        else
        {
            Console.WriteLine($"Received packet of invalid length: {packet.Length} bytes");
        }
    }

    private void StopUdpClient()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            _udpClient?.Close();
            _udpClient?.Dispose();
            _udpClient = null;

            ClearByteIndexes();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping UDP client: {ex.Message}");
        }
    }

    private static void ClearByteIndexes()
    {
        foreach (var byteIndex in MainWindowViewModel.ByteIndexes)
        {
            byteIndex.CurrentValue = 0;
            byteIndex.ValueDescription = string.Empty;
        }
    }
}
