using System;
using System.Timers;
using Avalonia;
using Haukcode.sACN;
using YALCY.Integrations.StageKit;
using YALCY.Udp;
using YALCY.Usb;
using YALCY.ViewModels;
using YALCY.Views.Components;
using Timer = System.Timers.Timer;

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

    private SACNClient? _sendClient;

    private readonly byte[] _currentDataPacket = new byte[UniverseSize];

    private Timer? _timer;

    private volatile byte _latestBpm;
    private volatile byte _latestCueChange;
    private volatile byte _latestBeatLine;
    private volatile byte _latestBonusEffect;
    private volatile byte _latestKeyFrame;
    private volatile byte _latestDrumNote;
    private volatile byte _latestPostProcessing;
    private volatile byte _latestGuitarNote;
    private volatile byte _latestBassNote;
    private volatile byte _latestSingalong;
    private volatile byte _latestSpotlight;
    private volatile byte _latestKeysNote;
    private volatile byte _latestVocalsNote;
    private volatile byte _latestHarmony0Note;
    private volatile byte _latestHarmony1Note;
    private volatile byte _latestHarmony2Note;
    private volatile byte _latestCurrentScene;
    private volatile byte _latestVenueSize;
    private volatile byte _latestPauseState;
    private volatile byte _latestSongSection;

    private float _bpmLastItemAdded = 0;
    private byte _cueChangeLastItemAdded = 0;
    private byte _beatLineLastItemAdded = 0;
    private byte _bonusEffectLastItemAdded = 0;
    private byte _keyFrameLastItemAdded = 0;
    private byte _postProcessingLastItemAdded = 0;
    private byte _currentSingalongLastItemAdded = 0;
    private byte _currentSpotlightLastItemAdded = 0;
    private float _vocalsNoteLastItemAdded = 0;
    private float _harmony0NoteLastItemAdded = 0;
    private float _harmony1NoteLastItemAdded = 0;
    private float _harmony2NoteLastItemAdded = 0;
    private byte _currentSceneLastItemAdded = 0;
    private byte _venueSizeLastItemAdded = 0;
    private byte _pauseStateLastItemAdded = 0;
    private byte _songSectionLastItemAdded = 0;
    private bool _bonusEffectLocked = false;

    //so we know what to unsubscribe later
    private Action<byte[]>? _packetProcessedHandler;
    private UdpIntake? _udpIntake;
    MainWindowViewModel? mainViewModel;

    public bool IsEnabled => _sendClient != null;

    private readonly object _sendLock = new();
    private readonly object _stateLock = new();
    private volatile bool _isStopping;

    private readonly long[] _guitarHoldUntil = new long[8];
    private readonly long[] _bassHoldUntil   = new long[8];
    private readonly long[] _drumsHoldUntil  = new long[8];
    private readonly long[] _keysHoldUntil   = new long[8];

    private byte _lastGuitarRaw;
    private byte _lastBassRaw;
    private byte _lastDrumsRaw;
    private byte _lastKeysRaw;


    private void OnTimerElapsed(object? sender, ElapsedEventArgs e) => Sender();

    public void EnableDmxTalker(bool isEnabled, bool sendBlackoutOnDisable = true)
    {
        if (isEnabled)
        {
            lock (_stateLock)
            {


                if (_sendClient != null) return;

                // Access the MainViewModel instance
                var app = (App)Application.Current!;
                mainViewModel = app.MainViewModel;

                var bindAddress = ResolveBindAddress(mainViewModel.SelectedSacnAdapter);

                _sendClient = new SACNClient(
                    senderId: AcnSourceId,
                    senderName: AcnSourceName,
                    localAddress: bindAddress
                );

                //The three parts of the dmx output: stage kit channels, master dimmers, and the channels read from the udp packet
                UpdateMasterDimmers();
                _udpIntake = mainViewModel.UdpIntake;
                _packetProcessedHandler = packet => UpdateDataPacket(packet, mainViewModel);
                _udpIntake.PacketProcessed += _packetProcessedHandler;
                UsbDeviceMonitor.OnStageKitCommand += OnStageKitEvent;
                StatusFooter.UpdateStatus("DMX", IntegrationStatus.Connected);

                _isStopping = false;

                _timer = new Timer(TimeBetweenCalls * 1000);
                _timer.AutoReset = true;
                _timer.Elapsed += OnTimerElapsed;
                _timer.Start();
            }
        }
        else
        {
            SACNClient? clientToDispose;
            lock (_stateLock)
            {
                if (_sendClient == null) return;

                _isStopping = true;

                if (_timer != null)
                {
                    _timer.Elapsed -= OnTimerElapsed;
                    _timer.Stop();
                    _timer.Dispose();
                    _timer = null;
                }

                lock (_sendLock)
                {
                    // unsubscribe events, clear packet, final send, dispose
                    UsbDeviceMonitor.OnStageKitCommand -= OnStageKitEvent;

                    // Unsubscribe safely
                    if (_udpIntake != null && _packetProcessedHandler != null)
                    {
                        _udpIntake.PacketProcessed -= _packetProcessedHandler;
                    }

                    _packetProcessedHandler = null;
                    _udpIntake = null;

                    StatusFooter.UpdateStatus("DMX", IntegrationStatus.Off);

                    //if just changing adapter, don't blackout
                    if (sendBlackoutOnDisable)
                    {
                        // Turn everything off directly
                        Array.Clear(_currentDataPacket, 0, _currentDataPacket.Length);

                        var app = (App)Application.Current!;
                        var mainViewModel = app.MainViewModel;

                        // Force send a final packet.
                        _sendClient?.SendDmxData(null, (ushort)mainViewModel.BroadcastUniverseSetting.Value,
                            _currentDataPacket);
                    }

                    clientToDispose = _sendClient;
                    _sendClient = null;
                }
            }
            try
            {
                clientToDispose?.Dispose();
            }
            catch (System.Threading.Channels.ChannelClosedException)
            {
                // Treat as already-closed: safe to ignore
            }
            catch (InvalidOperationException)
            {
                // Same idea: library is already shutting down
            }
            finally
            {
                _isStopping = false;
            }
        }
    }

    private static System.Net.IPAddress ResolveBindAddress(SacnAdapterOption? selectedAdapter)
    {
        if (selectedAdapter != null &&
            !string.IsNullOrWhiteSpace(selectedAdapter.IpAddress) &&
            System.Net.IPAddress.TryParse(selectedAdapter.IpAddress, out var address))
        {
            return address;
        }

        return Haukcode.Network.Utils.GetFirstBindAddress().IPAddress;
    }

    private void OnStageKitEvent(StageKitTalker.CommandId commandId, byte parameter)
    {
        // Helper: set value to a whole channel group
        void SetChannels(DmxChannelSetting group, byte value)
        {
            if (group.Channel == null) return;

            lock (_sendLock)
            {
                for (int i = 0; i < 8; i++)
                {
                    int ch = group.Channel[i];
                    if (ch > 0)
                        SetChannelToValue(ch, value);
                }
            }
        }

        // Helper: set LED pattern based on parameter bitmask
        void SetLeds(DmxChannelSetting group)
        {
            if (group.Channel == null) return;

            lock (_sendLock)
            {
                for (int i = 0; i < 8; i++)
                {
                    int ch = group.Channel[i];
                    if (ch > 0)
                        SetChannelToValue(ch, (parameter & (1 << i)) != 0 ? (byte)255 : (byte)0);
                }
            }
        }

        // Helper: handle all strobe speed modes
        void HandleStrobe(int multiplier)
        {
            var bpm = UdpIntake.BeatsPerMinute.Value;
            byte value = StrobeDmxFromBpm(bpm, multiplier);
            SetChannels(mainViewModel.StrobeChannels, value);
        }

        switch (commandId)
        {
            case StageKitTalker.CommandId.FogOn:
                SetChannels(mainViewModel.FogChannels, 255);
                break;

            case StageKitTalker.CommandId.FogOff:
                SetChannels(mainViewModel.FogChannels, 0);
                break;

            case StageKitTalker.CommandId.DisableAll:
                SetChannels(mainViewModel.StrobeChannels, 0);
                SetChannels(mainViewModel.FogChannels, 0);
                SetChannels(mainViewModel.BlueChannels, 0);
                SetChannels(mainViewModel.GreenChannels, 0);
                SetChannels(mainViewModel.YellowChannels, 0);
                SetChannels(mainViewModel.RedChannels, 0);
                break;

            case StageKitTalker.CommandId.StrobeOff:
                SetChannels(mainViewModel.StrobeChannels, 0);
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
                SetLeds(mainViewModel.BlueChannels);
                break;

            case StageKitTalker.CommandId.GreenLeds:
                SetLeds(mainViewModel.GreenChannels);
                break;

            case StageKitTalker.CommandId.YellowLeds:
                SetLeds(mainViewModel.YellowChannels);
                break;

            case StageKitTalker.CommandId.RedLeds:
                SetLeds(mainViewModel.RedChannels);
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
        if (_isStopping) return;

        lock (_sendLock)
        {
            if (_isStopping) return;
            if (_sendClient == null) return;

            if (mainViewModel != null)
            {
                UpdateStateChannels(mainViewModel);
            }

            // Sacn spec says multicast is the correct default way to go but singlecast can be used if needed.
            _sendClient?.SendDmxData(null, (ushort)mainViewModel.BroadcastUniverseSetting.Value, _currentDataPacket);
        }
    }

    private static double ComputePulseMs(float bpm)
    {
        if (bpm <= 0) return 60; // fallback if bpm missing

        double quarterMs = 60000.0 / bpm;

        // Target a 1/32 note, but clamp to [1/64, 1/16]
        double target = quarterMs / 8.0;   // 1/32
        double min    = quarterMs / 16.0;  // 1/64
        double max    = quarterMs / 4.0;   // 1/16

        return Math.Clamp(target, min, max);
    }

    private static void ApplyRisingEdgeHolds(byte newVal, ref byte lastVal, long[] holdUntil, long now, long holdMs)
    {
        byte rising = (byte)((newVal ^ lastVal) & newVal);
        if (rising != 0)
        {
            for (int bit = 0; bit < 8; bit++)
            {
                if ((rising & (1 << bit)) != 0)
                    holdUntil[bit] = now + holdMs;
            }
        }
        lastVal = newVal;
    }

    private static byte BuildHeldMask(long[] holdUntil, long now)
    {
        byte mask = 0;
        for (int bit = 0; bit < 8; bit++)
        {
            if (now < holdUntil[bit])
                mask |= (byte)(1 << bit);
        }
        return mask;
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

    private void UpdateStateChannels(MainWindowViewModel viewModel)
    {
        SetChannelIfValid(viewModel.BpmChannelSetting.Value, _latestBpm);
        SetChannelIfValid(viewModel.VocalsNoteChannelSetting.Value, _latestVocalsNote);
        SetChannelIfValid(viewModel.Harmony0NoteChannelSetting.Value, _latestHarmony0Note);
        SetChannelIfValid(viewModel.Harmony1NoteChannelSetting.Value, _latestHarmony1Note);
        SetChannelIfValid(viewModel.Harmony2NoteChannelSetting.Value, _latestHarmony2Note);
        SetChannelIfValid(viewModel.CueChangeChannelSetting.Value, _latestCueChange);
        SetChannelIfValid(viewModel.BeatLineChannelSetting.Value, _latestBeatLine);
        SetChannelIfValid(viewModel.BonusEffectChannelSetting.Value, _latestBonusEffect);
        SetChannelIfValid(viewModel.KeyFrameChannelSetting.Value, _latestKeyFrame);
        SetChannelIfValid(viewModel.DrumNoteChannelSetting.Value, _latestDrumNote);
        SetChannelIfValid(viewModel.PostProcessingChannelSetting.Value, _latestPostProcessing);
        SetChannelIfValid(viewModel.GuitarNoteChannelSetting.Value, _latestGuitarNote);
        SetChannelIfValid(viewModel.BassNoteChannelSetting.Value, _latestBassNote);
        SetChannelIfValid(viewModel.CurrentSingalongSetting.Value, _latestSingalong);
        SetChannelIfValid(viewModel.CurrentSpotlightSetting.Value, _latestSpotlight);
        SetChannelIfValid(viewModel.KeysNoteChannelSetting.Value, _latestKeysNote);
        SetChannelIfValid(viewModel.CurrentSceneSetting.Value, _latestCurrentScene);
        SetChannelIfValid(viewModel.VenueSizeSetting.Value, _latestVenueSize);
        SetChannelIfValid(viewModel.PauseStateSetting.Value, _latestPauseState);
        SetChannelIfValid(viewModel.SongSectionSetting.Value, _latestSongSection);
    }

    private void SetChannelIfValid(int dmxChannel, byte value)
    {
        if (dmxChannel <= 0 || dmxChannel > UniverseSize) return;
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
            _latestBpm = (byte)bpm;
            _bpmLastItemAdded = bpm;
        }

        long now = Environment.TickCount64;
        long holdMs = (long)ComputePulseMs(bpm);

        if (vocalsNote != _vocalsNoteLastItemAdded)
        {
            _latestVocalsNote = (byte)vocalsNote;
            _vocalsNoteLastItemAdded = vocalsNote;
        }

        if (harmony0Note != _harmony0NoteLastItemAdded)
        {
            _latestHarmony0Note = (byte)harmony0Note;
            _harmony0NoteLastItemAdded = harmony0Note;
        }

        if (harmony1Note != _harmony1NoteLastItemAdded)
        {
            _latestHarmony1Note = (byte)harmony1Note;
            _harmony1NoteLastItemAdded = harmony1Note;
        }

        if (harmony2Note != _harmony2NoteLastItemAdded)
        {
            _latestHarmony2Note = (byte)harmony2Note;
            _harmony2NoteLastItemAdded = harmony2Note;
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.LightingCue] != _cueChangeLastItemAdded)
        {
            _latestCueChange = udpBuffer[(int)UdpIntake.ByteIndexName.LightingCue];
            _cueChangeLastItemAdded = _latestCueChange;
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.Beat] != _beatLineLastItemAdded)
        {
            _latestBeatLine = udpBuffer[(int)UdpIntake.ByteIndexName.Beat];
            _beatLineLastItemAdded = _latestBeatLine;
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.Keyframe] != _keyFrameLastItemAdded)
        {
            _latestKeyFrame = udpBuffer[(int)UdpIntake.ByteIndexName.Keyframe];
            _keyFrameLastItemAdded = _latestKeyFrame;
        }
        
        if (!_bonusEffectLocked && udpBuffer[(int)UdpIntake.ByteIndexName.BonusEffect] != _bonusEffectLastItemAdded)
        {
            var newValue = udpBuffer[(int)UdpIntake.ByteIndexName.BonusEffect];
            _bonusEffectLastItemAdded = newValue;

            if (newValue == 1)
            {
                _bonusEffectLocked = true;

                // Immediately turn on
                _latestBonusEffect = 255;

                // Compute beat duration in milliseconds
                float curbpm = BitConverter.ToSingle(udpBuffer, (int)UdpIntake.ByteIndexName.BeatsPerMinute);
                double beatDurationMs = (curbpm > 0 ? 60000.0 / curbpm : 500) * 4;

                // Schedule turn-off and unlock
                var timer = new Timer(beatDurationMs);
                timer.AutoReset = false;
                timer.Elapsed += (s, e) =>
                {
                    if (_isStopping) { timer.Dispose(); return; }
                    _latestBonusEffect = 0;
                    _bonusEffectLocked = false;
                    timer.Dispose();
                };
                timer.Start();
            }
            else
            {
                // If explicitly off and not locked (e.g., at song start or stop)
                _latestBonusEffect = 0;
            }
        }

        // Guitar
        byte guitarRaw = udpBuffer[(int)UdpIntake.ByteIndexName.GuitarNotes];
        ApplyRisingEdgeHolds(guitarRaw, ref _lastGuitarRaw, _guitarHoldUntil, now, holdMs);
        byte guitarHeld = (byte)(guitarRaw | BuildHeldMask(_guitarHoldUntil, now));
        _latestGuitarNote = guitarHeld;

        // Bass
        byte bassRaw = udpBuffer[(int)UdpIntake.ByteIndexName.BassNotes];
        ApplyRisingEdgeHolds(bassRaw, ref _lastBassRaw, _bassHoldUntil, now, holdMs);
        byte bassHeld = (byte)(bassRaw | BuildHeldMask(_bassHoldUntil, now));
        _latestBassNote = bassHeld;

        // Drums
        byte drumsRaw = udpBuffer[(int)UdpIntake.ByteIndexName.DrumsNotes];
        ApplyRisingEdgeHolds(drumsRaw, ref _lastDrumsRaw, _drumsHoldUntil, now, holdMs);
        byte drumsHeld = (byte)(drumsRaw | BuildHeldMask(_drumsHoldUntil, now));
        _latestDrumNote = drumsHeld;

        // Keys
        byte keysRaw = udpBuffer[(int)UdpIntake.ByteIndexName.KeysNotes];
        ApplyRisingEdgeHolds(keysRaw, ref _lastKeysRaw, _keysHoldUntil, now, holdMs);
        byte keysHeld = (byte)(keysRaw | BuildHeldMask(_keysHoldUntil, now));
        _latestKeysNote = keysHeld;

        if (udpBuffer[(int)UdpIntake.ByteIndexName.PostProcessing] != _postProcessingLastItemAdded)
        {
            _latestPostProcessing = udpBuffer[(int)UdpIntake.ByteIndexName.PostProcessing];
            _postProcessingLastItemAdded = _latestPostProcessing;
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.Singalong] != _currentSingalongLastItemAdded)
        {
            _latestSingalong = udpBuffer[(int)UdpIntake.ByteIndexName.Singalong];
            _currentSingalongLastItemAdded = _latestSingalong;
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.Spotlight] != _currentSpotlightLastItemAdded)
        {
            _latestSpotlight = udpBuffer[(int)UdpIntake.ByteIndexName.Spotlight];
            _currentSpotlightLastItemAdded = _latestSpotlight;
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.CurrentScene] != _currentSceneLastItemAdded)
        {
            _latestCurrentScene = udpBuffer[(int)UdpIntake.ByteIndexName.CurrentScene];
            _currentSceneLastItemAdded = _latestCurrentScene;
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.VenueSize] != _venueSizeLastItemAdded)
        {
            _latestVenueSize = udpBuffer[(int)UdpIntake.ByteIndexName.VenueSize];
            _venueSizeLastItemAdded = _latestVenueSize;
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.PauseState] != _pauseStateLastItemAdded)
        {
            _latestPauseState = udpBuffer[(int)UdpIntake.ByteIndexName.PauseState];
            _pauseStateLastItemAdded = _latestPauseState;
        }

        if (udpBuffer[(int)UdpIntake.ByteIndexName.SongSection] != _songSectionLastItemAdded)
        {
            _latestSongSection = udpBuffer[(int)UdpIntake.ByteIndexName.SongSection];
            _songSectionLastItemAdded = _latestSongSection;
        }
    }
}
