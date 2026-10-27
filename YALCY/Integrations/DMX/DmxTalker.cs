using YALCY.Lighting;
using YALCY.Diagnostics;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Haukcode.sACN;
using YALCY.Integrations;
using YALCY.Udp;
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
    private ArtNetSender? _artNetClient;
    private readonly bool _useArtNet;
    private bool _sendFailed;
    private string StatusName => _useArtNet ? "Art-Net" : "DMX";

    public DmxTalker(bool useArtNet = false) => _useArtNet = useArtNet;

    private readonly byte[] _currentDataPacket = new byte[UniverseSize];
    private readonly ManualStrobeFlasher _manualStrobeFlasher = new(ex => AppLog.Write(LogLevel.Error, "DMX", $"DMX manual strobe error: {ex.Message}"));

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
    private MainWindowViewModel? _mainViewModel;

    public bool IsEnabled => _sendClient != null || _artNetClient != null;
    public bool IsSending => IsEnabled && !_sendFailed;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private int _pendingTransitions;
    public bool IsTransitioning => Volatile.Read(ref _pendingTransitions) != 0;

    public async Task EnableDmxTalkerAsync(bool isEnabled, MainWindowViewModel viewModel)
    {
        Interlocked.Increment(ref _pendingTransitions);
        viewModel.RefreshDmxOutputStatus();
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() => EnableDmxTalkerCore(isEnabled, viewModel, true)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StatusFooter.UpdateStatus(StatusName, IntegrationStatus.Error,
                $"Could not change {StatusName} output: {ex.Message}");
        }
        finally
        {
            _lifecycleGate.Release();
            Interlocked.Decrement(ref _pendingTransitions);
            viewModel.RefreshDmxOutputStatus();
        }
    }

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

    public void EnableDmxTalker(bool isEnabled, MainWindowViewModel? viewModel = null, bool sendBlackoutOnDisable = true)
    {
        _lifecycleGate.Wait();
        try { EnableDmxTalkerCore(isEnabled, viewModel, sendBlackoutOnDisable); }
        finally { _lifecycleGate.Release(); }
    }

    private void EnableDmxTalkerCore(bool isEnabled, MainWindowViewModel? viewModel, bool sendBlackoutOnDisable)
    {
        if (isEnabled)
        {
            lock (_stateLock)
            {
                if (IsEnabled) return;

                if (viewModel != null)
                {
                    _mainViewModel = viewModel;
                }

                if (_mainViewModel == null)
                {
                    AppLog.Write(LogLevel.Warning, "DMX", "DmxTalker: No ViewModel provided and none cached.");
                    return;
                }

                if (_useArtNet)
                {
                    try
                    {
                        var adapter = _mainViewModel.SelectedArtNetAdapter;
                        var bindAddress = string.IsNullOrWhiteSpace(adapter?.IpAddress)
                            ? System.Net.IPAddress.Any : System.Net.IPAddress.Parse(adapter.IpAddress);
                        _artNetClient = new ArtNetSender(_mainViewModel.ArtNetDestinationIp,
                            bindAddress, _mainViewModel.ArtNetUniverse);
                        _sendFailed = false;
                    }
                    catch (Exception ex)
                    {
                        StatusFooter.UpdateStatus(StatusName, IntegrationStatus.Error, ex.Message);
                        _mainViewModel.ArtNetStatus = ex.Message;
                        return;
                    }
                }
                else
                {
                    var bindAddress = ResolveBindAddress(_mainViewModel.SelectedSacnAdapter);
                    _sendClient = new SACNClient(
                        senderId: AcnSourceId,
                        senderName: AcnSourceName,
                        localAddress: bindAddress
                    );
                }

                //The three parts of the dmx output: stage kit channels, master dimmers, and the channels read from the udp packet
                UpdateMasterDimmers();
                _udpIntake = _mainViewModel.UdpIntake;
                _packetProcessedHandler = packet => UpdateDataPacket(packet, _mainViewModel);
                _udpIntake.PacketProcessed += _packetProcessedHandler;
                LightingOutput.CommandReceived += OnStageKitEvent;
                StatusFooter.UpdateStatus(StatusName, IntegrationStatus.Connected);
                if (_useArtNet) _mainViewModel.ArtNetStatus = "Sending Art-Net. Disable output before changing connection settings.";

                _isStopping = false;

                _timer = new Timer(TimeBetweenCalls * 1000);
                _timer.AutoReset = true;
                _timer.Elapsed += OnTimerElapsed;
                _timer.Start();
                _mainViewModel.RefreshDmxOutputStatus();
            }
        }
        else
        {
            SACNClient? clientToDispose;
            ArtNetSender? artNetToDispose;
            lock (_stateLock)
            {
                if (!IsEnabled)
                {
                    if (_useArtNet && _mainViewModel != null)
                    {
                        StatusFooter.UpdateStatus(StatusName, IntegrationStatus.Off);
                        _mainViewModel.ArtNetStatus = "Art-Net disabled.";
                    }
                    return;
                }

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
                    LightingOutput.CommandReceived -= OnStageKitEvent;

                    // Unsubscribe safely
                    if (_udpIntake != null && _packetProcessedHandler != null)
                    {
                        _udpIntake.PacketProcessed -= _packetProcessedHandler;
                    }

                    _manualStrobeFlasher.Stop(SetStrobeChannelsForManualFlashAsync);

                    _packetProcessedHandler = null;
                    _udpIntake = null;

                    //if just changing adapter, don't blackout
                    if (sendBlackoutOnDisable && _mainViewModel != null)
                    {
                        // Turn everything off directly
                        Array.Clear(_currentDataPacket, 0, _currentDataPacket.Length);

                        // Force send a final packet.
                        SendFrame();
                    }

                    clientToDispose = _sendClient;
                    _sendClient = null;
                    artNetToDispose = _artNetClient;
                    _artNetClient = null;
                    StatusFooter.UpdateStatus(StatusName, IntegrationStatus.Off);
                    if (_useArtNet && _mainViewModel != null) _mainViewModel.ArtNetStatus = "Art-Net disabled.";
                    _mainViewModel?.RefreshDmxOutputStatus();
                }
            }
            try
            {
                artNetToDispose?.Dispose();
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

    private void OnStageKitEvent(LightingCommand commandId, byte parameter)
    {
        if (_mainViewModel == null) return;

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

        switch (commandId)
        {
            case LightingCommand.FogOn:
                SetChannels(_mainViewModel.FogChannels, 255);
                break;

            case LightingCommand.FogOff:
                SetChannels(_mainViewModel.FogChannels, 0);
                break;

            case LightingCommand.DisableAll:
                _manualStrobeFlasher.Stop(SetStrobeChannelsForManualFlashAsync);
                lock (_sendLock)
                {
                    Array.Clear(_currentDataPacket, 0, _currentDataPacket.Length);
                }
                break;

            case LightingCommand.StrobeOff:
                _manualStrobeFlasher.Stop(SetStrobeChannelsForManualFlashAsync);
                SetChannels(_mainViewModel.StrobeChannels, 0);
                break;

            case LightingCommand.StrobeSlow:
                HandleStrobeCommand(commandId, 4);
                break;

            case LightingCommand.StrobeMedium:
                HandleStrobeCommand(commandId, 6);
                break;

            case LightingCommand.StrobeFast:
                HandleStrobeCommand(commandId, 8);
                break;

            case LightingCommand.StrobeFastest:
                HandleStrobeCommand(commandId, 10);
                break;

            case LightingCommand.BlueLeds:
                SetLeds(_mainViewModel.BlueChannels);
                break;

            case LightingCommand.GreenLeds:
                SetLeds(_mainViewModel.GreenChannels);
                break;

            case LightingCommand.YellowLeds:
                SetLeds(_mainViewModel.YellowChannels);
                break;

            case LightingCommand.RedLeds:
                SetLeds(_mainViewModel.RedChannels);
                break;
        }
    }

    private void HandleStrobeCommand(LightingCommand commandId, int commandMultiplier)
    {
        if (_mainViewModel == null) return;

        if (_mainViewModel.DmxStrobeMode == StrobeOutputModes.ManualFlash)
        {
            _manualStrobeFlasher.Start(
                commandId,
                UdpIntake.BeatsPerMinute.Value,
                SetStrobeChannelsForManualFlashAsync);
            return;
        }

        _manualStrobeFlasher.Stop(SetStrobeChannelsForManualFlashAsync);
        var bpm = UdpIntake.BeatsPerMinute.Value;
        var value = StrobeDmxFromBpm(bpm, commandMultiplier);
        SetChannels(_mainViewModel.StrobeChannels, value);
    }

    private Task SetStrobeChannelsForManualFlashAsync(bool isOn, CancellationToken cancellationToken)
    {
        if (_mainViewModel != null)
        {
            var allowFlash = isOn && !LightingOutput.IsOutputSuppressed;
            SetChannels(_mainViewModel.StrobeChannels, allowFlash ? (byte)255 : (byte)0);
        }

        return Task.CompletedTask;
    }

    private void SetChannels(DmxChannelSetting group, byte value)
    {
        if (group.Channel == null) return;

        lock (_sendLock)
        {
            for (int i = 0; i < 8 && i < group.Channel.Length; i++)
            {
                int ch = group.Channel[i];
                if (ch > 0)
                    SetChannelToValue(ch, value);
            }
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
            if (!IsEnabled) return;
            if (_mainViewModel == null) return;

            if (LightingOutput.IsOutputSuppressed)
            {
                Array.Clear(_currentDataPacket, 0, _currentDataPacket.Length);
            }
            else
            {
                UpdateMasterDimmers();
                UpdateStateChannels(_mainViewModel);
            }

            // Sacn spec says multicast is the correct default way to go but singlecast can be used if needed.
            SendFrame();
        }
    }

    private void SendFrame()
    {
        if (!_useArtNet)
        {
            _sendClient?.SendDmxData(null, (ushort)_mainViewModel!.BroadcastUniverseSetting.Value, _currentDataPacket);
            return;
        }

        try
        {
            _artNetClient?.Send(_currentDataPacket);
            var recovered = _sendFailed;
            _sendFailed = false;
            if (recovered && !_isStopping)
            {
                StatusFooter.UpdateStatus(StatusName, IntegrationStatus.Connected);
                _mainViewModel!.ArtNetStatus = "Sending Art-Net.";
            }
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            if (!_sendFailed)
                StatusFooter.UpdateStatus(StatusName, IntegrationStatus.Error, $"Art-Net send failed: {ex.Message}");
            _sendFailed = true;
            _mainViewModel!.ArtNetStatus = $"Art-Net send failed: {ex.Message}";
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
        if (_mainViewModel == null) return;
        if (_mainViewModel.MasterDimmerSettings.Channel == null || _mainViewModel.MasterDimmerValues.Channel == null) return;
        for (int i = 0; i < _mainViewModel.MasterDimmerSettings.Channel.Length; i++)
        {
            SetChannelToValue(_mainViewModel.MasterDimmerSettings.Channel[i],
                (byte)Math.Clamp(_mainViewModel.MasterDimmerValues.Channel[i], 0, 255));
        }
    }

    public void SetChannelToValue(int dmxChannel, byte value)
    {
        lock (_sendLock) SetChannelIfValid(dmxChannel, value);
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
            AppLog.Write(LogLevel.Warning, "DMX", $"Invalid UDP buffer length: {udpBuffer?.Length ?? 0}");
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

        if (viewModel.UdpIntake.Beat.Value != _beatLineLastItemAdded)
        {
            _latestBeatLine = viewModel.UdpIntake.Beat.Value;
            _beatLineLastItemAdded = _latestBeatLine;
        }

        if (viewModel.UdpIntake.Keyframe.Value != _keyFrameLastItemAdded)
        {
            _latestKeyFrame = viewModel.UdpIntake.Keyframe.Value;
            _keyFrameLastItemAdded = _latestKeyFrame;
        }

        if (!_bonusEffectLocked && (byte)(viewModel.UdpIntake.BonusEffect.Value ? 1 : 0) != _bonusEffectLastItemAdded)
        {
            var newValue = (byte)(viewModel.UdpIntake.BonusEffect.Value ? 1 : 0);
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

        if (viewModel.UdpIntake.Singalong.Value != _currentSingalongLastItemAdded)
        {
            _latestSingalong = viewModel.UdpIntake.Singalong.Value;
            _currentSingalongLastItemAdded = _latestSingalong;
        }

        if (viewModel.UdpIntake.Spotlight.Value != _currentSpotlightLastItemAdded)
        {
            _latestSpotlight = viewModel.UdpIntake.Spotlight.Value;
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
