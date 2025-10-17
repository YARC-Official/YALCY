using System;
using System.Collections.Concurrent;
using System.Timers;
using Avalonia;
using Haukcode.sACN;
using YALCY.Integrations.StageKit;
using YALCY.Udp;
using YALCY.Usb;
using YALCY.ViewModels;
using YALCY.Views.Components;

namespace YALCY.Integrations.DMX;

public class DmxTalker
{
    // DMX spec says 44 updates per second is the max
    private const float TargetFps = 44f;
    private const float TimeBetweenCalls = 1f / TargetFps;

    // Each universe supports up to 512 channels
    private const int UniverseSize = 512;

    private const string AcnSourceName = "YARG";

    // A 128-bit (16 byte) UUID that translates to "KEEP PLAYING YARG!"
    private static readonly Guid AcnSourceId = new Guid("{4B454550-504C-4159-494E-475941524721}");

    private static SACNClient? _sendClient;

    private readonly byte[] _currentDataPacket = new byte[UniverseSize];
    private ConcurrentQueue<byte>[] byteQueues;

    private static Timer? _timer;

    private float _bpmLastItemAdded = 0;
    private byte _cueChangeLastItemAdded = 0;
    private byte _beatLineLastItemAdded = 0;
    private byte _bonusEffectLastItemAdded = 0;
    private byte _keyFrameLastItemAdded = 0;
    private byte _drumNoteLastItemAdded = 0;
    private byte _postProcessingLastItemAdded = 0;
    private byte _guitarNoteLastItemAdded = 0;
    private byte _bassNoteLastItemAdded = 0;
    private byte _currentSingalongLastItemAdded = 0;
    private byte _currentSpotlightLastItemAdded = 0;
    private byte _keysNoteLastItemAdded = 0;
    private float _vocalsNoteLastItemAdded = 0;
    private float _harmony0NoteLastItemAdded = 0;
    private float _harmony1NoteLastItemAdded = 0;
    private float _harmony2NoteLastItemAdded = 0;
    private byte _currentSceneLastItemAdded = 0;
    private byte _venueSizeLastItemAdded = 0;
    private byte _pauseStateLastItemAdded = 0;
    private byte _songSectionLastItemAdded = 0;
    private bool _bonusEffectLocked = false;
    App app;
    MainWindowViewModel mainViewModel;

    public void EnableDmxTalker(bool isEnabled)
    {
        if (isEnabled)
        {
            if (_sendClient != null) return;

            _sendClient = new SACNClient(
                senderId: AcnSourceId,
                senderName: AcnSourceName,
                localAddress: Haukcode.Network.Utils.GetFirstBindAddress().IPAddress
                );

            // Access the MainViewModel instance
            app = (App)Application.Current!;
            mainViewModel = app.MainViewModel;

            byteQueues = new ConcurrentQueue<byte>[UniverseSize];

            // Initialize each ConcurrentQueue in the array
            for (int i = 0; i < UniverseSize; i++)
            {
                byteQueues[i] = new ConcurrentQueue<byte>();
            }

            //The three parts of the dmx output: stage kit channels, master dimmers, and the channels read from the udp packet
            UpdateMasterDimmers();
            mainViewModel.UdpIntake.PacketProcessed += (packet) => UpdateDataPacket(packet, mainViewModel);
            UsbDeviceMonitor.OnStageKitCommand += OnStageKitEvent;
            StatusFooter.UpdateStatus("DMX", IntegrationStatus.Connected);

            _timer = new Timer(TimeBetweenCalls * 1000);
            _timer.Elapsed += (sender, e) => Sender();
            _timer.Start();
        }
        else
        {
            if (_sendClient == null) return;

            UsbDeviceMonitor.OnStageKitCommand -= OnStageKitEvent;
            StatusFooter.UpdateStatus("DMX", IntegrationStatus.Off);

            _timer?.Stop();
            _timer?.Dispose();

            // Turn everything off directly
            Array.Clear(_currentDataPacket, 0, _currentDataPacket.Length);

            // Access the MainViewModel instance, can't assume it was set in enable
            var app = (App)Application.Current!;
            var mainViewModel = app.MainViewModel;

            // Force send final packet.
            _sendClient.SendDmxData(null,(ushort)mainViewModel.BroadcastUniverseSetting.Value, _currentDataPacket);

            _sendClient.Dispose();
            _sendClient = null;
        }
    }

    private void OnStageKitEvent(StageKitTalker.CommandId commandId, byte parameter)
    {
        // Helper: enqueue value to a whole channel group
        void EnqueueToChannels(DmxChannelSetting group, byte value)
        {
            if (group.Channel == null) return;

            for (int i = 0; i < 8; i++)
            {
                int ch = group.Channel[i];
                if (ch > 0)
                    byteQueues[ch - 1].Enqueue(value);
            }
        }

        // Helper: enqueue LED pattern based on parameter bitmask
        void EnqueueLeds(DmxChannelSetting group)
        {
            if (group.Channel == null) return;

            for (int i = 0; i < 8; i++)
            {
                int ch = group.Channel[i];
                if (ch > 0)
                    byteQueues[ch - 1].Enqueue((parameter & (1 << i)) != 0 ? (byte)255 : (byte)0);
            }
        }

        // Helper: handle all strobe speed modes
        void HandleStrobe(int multiplier)
        {
            var bpm = UdpIntake.BeatsPerMinute.Value;
            byte value = (byte)StrobeDmxFromBpm(bpm, multiplier);
            EnqueueToChannels(mainViewModel.StrobeChannels, value);
        }

        switch (commandId)
        {
            case StageKitTalker.CommandId.FogOn:
                EnqueueToChannels(mainViewModel.FogChannels, 255);
                break;

            case StageKitTalker.CommandId.FogOff:
                EnqueueToChannels(mainViewModel.FogChannels, 0);
                break;

            case StageKitTalker.CommandId.DisableAll:
                EnqueueToChannels(mainViewModel.StrobeChannels, 0);
                EnqueueToChannels(mainViewModel.FogChannels, 0);
                EnqueueToChannels(mainViewModel.BlueChannels, 0);
                EnqueueToChannels(mainViewModel.GreenChannels, 0);
                EnqueueToChannels(mainViewModel.YellowChannels, 0);
                EnqueueToChannels(mainViewModel.RedChannels, 0);
                break;

            case StageKitTalker.CommandId.StrobeOff:
                EnqueueToChannels(mainViewModel.StrobeChannels, 0);
                break;

            case StageKitTalker.CommandId.StrobeSlow:
                HandleStrobe(4);
                break;

            case StageKitTalker.CommandId.StrobeMedium:
                HandleStrobe(6);
                break;

            case StageKitTalker.CommandId.StrobeFast:
                HandleStrobe(8);
                break;

            case StageKitTalker.CommandId.StrobeFastest:
                HandleStrobe(10);
                break;

            case StageKitTalker.CommandId.BlueLeds:
                EnqueueLeds(mainViewModel.BlueChannels);
                break;

            case StageKitTalker.CommandId.GreenLeds:
                EnqueueLeds(mainViewModel.GreenChannels);
                break;

            case StageKitTalker.CommandId.YellowLeds:
                EnqueueLeds(mainViewModel.YellowChannels);
                break;

            case StageKitTalker.CommandId.RedLeds:
                EnqueueLeds(mainViewModel.RedChannels);
                break;
        }
    }
    private byte StrobeDmxFromBpm(float bpm, int speed) {
        var f = bpm * speed / 60.0;
        if (f <= 0) return 0;
        if (f > 25.0) f = 25.0;
        var dmx = (byte)Math.Round((f / 25.0) * 255.0);
        return dmx;
    }

    private void Sender()
    {
        for (int i = 0; i < UniverseSize; i++)
        {
            if (byteQueues[i].TryDequeue(out byte value))
            {
                _currentDataPacket[i] = value;
            }
        }

        // Sacn spec says multicast is the correct default way to go but singlecast can be used if needed.
        _sendClient?.SendDmxData(null,(ushort)mainViewModel.BroadcastUniverseSetting.Value, _currentDataPacket);
    }

    public void UpdateMasterDimmers()
    {
        for (int i = 0; i < mainViewModel.MasterDimmerSettings.Channel.Length; i++)
        {
            SetChannelToValue(mainViewModel.MasterDimmerSettings.Channel[i],
                (byte)mainViewModel.MasterDimmerValues.Channel[i]);
        }
    }

    public void SetChannelToValue(int dmxChannel, byte value)
    {
        _currentDataPacket[dmxChannel - 1] = value;
    }

    private void UpdateDataPacket(byte[] udpBuffer, MainWindowViewModel viewModel)
    {
        if (udpBuffer == null || udpBuffer.Length < UdpIntake.MIN_PACKET_SIZE)
        {
            Console.WriteLine($"Invalid UDP buffer length: {udpBuffer?.Length ?? 0}");
            return;
        }
        // Let's deal with floats first. They all get cast from float to byte (0-255). If more precision needed, consider adding another byte channel.
        float bpm = BitConverter.ToSingle(udpBuffer, (int)UdpIntake.ByteIndexName.BeatsPerMinute);
        float vocalsNote = BitConverter.ToSingle(udpBuffer, (int)UdpIntake.ByteIndexName.VocalsNote);
        float harmony0Note = BitConverter.ToSingle(udpBuffer, (int)UdpIntake.ByteIndexName.Harmony0Note);
        float harmony1Note = BitConverter.ToSingle(udpBuffer, (int)UdpIntake.ByteIndexName.Harmony1Note);
        float harmony2Note = BitConverter.ToSingle(udpBuffer, (int)UdpIntake.ByteIndexName.Harmony2Note);

        if (bpm != _bpmLastItemAdded)
        {
            byteQueues[viewModel.BpmChannelSetting.Value - 1].Enqueue((byte)bpm);
            _bpmLastItemAdded = bpm;
        }

        if (vocalsNote != _vocalsNoteLastItemAdded)
        {
            byteQueues[viewModel.VocalsNoteChannelSetting.Value - 1].Enqueue((byte)vocalsNote);
            _vocalsNoteLastItemAdded = vocalsNote;
        }

        if (harmony0Note != _harmony0NoteLastItemAdded)
        {
            byteQueues[viewModel.Harmony0NoteChannelSetting.Value - 1].Enqueue((byte)harmony0Note);
            _harmony0NoteLastItemAdded = harmony0Note;
        }

        if (harmony1Note != _harmony1NoteLastItemAdded)
        {
            byteQueues[viewModel.Harmony1NoteChannelSetting.Value - 1].Enqueue((byte)harmony1Note);
            _harmony1NoteLastItemAdded = harmony1Note;
        }

        if (harmony2Note != _harmony2NoteLastItemAdded)
        {
            byteQueues[viewModel.Harmony2NoteChannelSetting.Value - 1].Enqueue((byte)harmony2Note);
            _harmony2NoteLastItemAdded = harmony2Note;
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.LightingCue] != _cueChangeLastItemAdded)
        {
            byteQueues[viewModel.CueChangeChannelSetting.Value - 1].Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.LightingCue]);
            _cueChangeLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.LightingCue];
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.Beat] != _beatLineLastItemAdded)
        {
            byteQueues[viewModel.BeatLineChannelSetting.Value - 1].Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.Beat]);
            _beatLineLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.Beat];
        }

        if (!_bonusEffectLocked && udpBuffer[(int)UdpIntake.ByteIndexName.BonusEffect] != _bonusEffectLastItemAdded)
        {
            var newValue = udpBuffer[(int)UdpIntake.ByteIndexName.BonusEffect];
            _bonusEffectLastItemAdded = newValue;

            var channel = viewModel.BonusEffectChannelSetting.Value - 1;

            if (newValue == 1)
            {
                _bonusEffectLocked = true;

                // Immediately turn on
                byteQueues[channel].Enqueue(255);

                // Compute beat duration in milliseconds
                float curbpm = BitConverter.ToSingle(udpBuffer, (int)UdpIntake.ByteIndexName.BeatsPerMinute);
                double beatDurationMs = (curbpm > 0 ? 60000.0 / bpm : 500) * 4;

                // Schedule turn-off and unlock
                var timer = new Timer(beatDurationMs);
                timer.AutoReset = false;
                timer.Elapsed += (s, e) =>
                {
                    byteQueues[channel].Enqueue(0);
                    _bonusEffectLocked = false;
                    timer.Dispose();
                };
                timer.Start();
            }
            else
            {
                // If explicitly off and not locked (e.g., at song start or stop)
                byteQueues[channel].Enqueue(0);
            }
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.Keyframe] != _keyFrameLastItemAdded)
        {
            byteQueues[viewModel.KeyFrameChannelSetting.Value - 1]
                .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.Keyframe]);
            _keyFrameLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.Keyframe];
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.DrumsNotes] != _drumNoteLastItemAdded)
        {
            byteQueues[viewModel.DrumNoteChannelSetting.Value - 1]
                .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.DrumsNotes]);
            _drumNoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.DrumsNotes];
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.PostProcessing] != _postProcessingLastItemAdded)
        {
            byteQueues[viewModel.PostProcessingChannelSetting.Value - 1]
                .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.PostProcessing]);
            _postProcessingLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.PostProcessing];
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.GuitarNotes] != _guitarNoteLastItemAdded)
        {
            byteQueues[viewModel.GuitarNoteChannelSetting.Value - 1]
                .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.GuitarNotes]);
            _guitarNoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.GuitarNotes];
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.BassNotes] != _bassNoteLastItemAdded)
        {
            byteQueues[viewModel.BassNoteChannelSetting.Value - 1]
                .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.BassNotes]);
            _bassNoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.BassNotes];
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.Singalong] != _currentSingalongLastItemAdded)
        {
            byteQueues[viewModel.CurrentSingalongSetting.Value - 1]
                .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.Singalong]);
            _currentSingalongLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.Singalong];
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.Spotlight] != _currentSpotlightLastItemAdded)
        {
            byteQueues[viewModel.CurrentSpotlightSetting.Value - 1]
                .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.Spotlight]);
            _currentSpotlightLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.Spotlight];
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.KeysNotes] != _keysNoteLastItemAdded)
        {
            byteQueues[viewModel.KeysNoteChannelSetting.Value - 1]
                .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.KeysNotes]);
            _keysNoteLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.KeysNotes];
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.CurrentScene] != _currentSceneLastItemAdded)
        {
            byteQueues[viewModel.CurrentSceneSetting.Value - 1]
                .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.CurrentScene]);
            _currentSceneLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.CurrentScene];
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.VenueSize] != _venueSizeLastItemAdded)
        {
            byteQueues[viewModel.VenueSizeSetting.Value - 1]
                .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.VenueSize]);
            _venueSizeLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.VenueSize];
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.PauseState] != _pauseStateLastItemAdded)
        {
            byteQueues[viewModel.PauseStateSetting.Value - 1]
                .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.PauseState]);
            _pauseStateLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.PauseState];
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.SongSection] != _songSectionLastItemAdded)
        {
            byteQueues[viewModel.SongSectionSetting.Value - 1]
                .Enqueue(udpBuffer[(int)UdpIntake.ByteIndexName.SongSection]);
            _songSectionLastItemAdded = udpBuffer[(int)UdpIntake.ByteIndexName.SongSection];
        }
    }
}
