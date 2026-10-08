using System;
using System.Buffers.Binary;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YALCY.Integrations;
using YALCY.Integrations.StageKit;
using YALCY.Udp;
using YALCY.Usb;
using YALCY.ViewModels;
using YALCY.Views.Components;

namespace YALCY.Integrations.WLED;

/// <summary>
/// Master coordinator for direct WLED stage lighting integration.
/// Dispatches real-time StageKit cues and YARG rhythm data (notes, star power, beats)
/// directly to ESP32 / WLED controllers over UDP (via DDP or Realtime DRGBW) with sub-2ms latency.
/// 
/// Key Architectural Advantages over OpenRGB:
/// 1. Native 4th Channel (W) Control: Dedicated SK6812/WS2814 neutral white diode modulation
///    for true concert blinders, snare accents, and breakdowns with zero RGB chromatic aberration.
/// 2. Independent Layering: Ambient RGB stage color wash operates simultaneously with
///    high-priority rhythmic White (W) accents.
/// 3. Zero Middleware Overhead: Direct UDP packets bypass external PC software layers and E1.31 bridges.
/// </summary>
public sealed class WledTalker : IDisposable
{
    private const int StageKitLedCount = 32;

    // Synchronization lock for strip framebuffers and configuration
    public readonly object Lock = new();

    // UDP transmission engine
    private readonly WledSender _sender = new();

    // Strobe and flash controllers
    private readonly ManualStrobeFlasher _manualStrobeFlasher =
        new(ex => Console.WriteLine($"WLED manual strobe error: {ex.Message}"));

    // Virtual StageKit 32-LED state buffer (8 Blue [0..7], 8 Red [8..15], 8 Green [16..23], 8 Yellow [24..31])
    private readonly WledRgbwColor[] _stageKitVirtualLeds = new WledRgbwColor[StageKitLedCount];

    // Master output frame buffer sent to the physical WLED strip
    private WledRgbwColor[] _outputBuffer = new WledRgbwColor[32];

    // Configuration properties
    private string _targetIp = "wled";
    private int _targetPort = WledSender.DefaultDdpPort;
    private WledProtocol _protocol = WledProtocol.DdpRgb;
    private int _ledCount = 32;
    private float _masterBrightness = 1.0f;
    private float _whiteBrightness = 1.0f;
    private WledLayoutMode _layoutMode = WledLayoutMode.TiledRepeat32;
    private WledWhiteStrobeMode _whiteStrobeMode = WledWhiteStrobeMode.PureWhiteOnly;
    private int _strobeMode = StrobeOutputModes.ManualFlash;
    private bool _rhythmicWhiteAccentEnabled = false;
    private bool _snareFlashEnabled = false;
    private bool _starPowerPulseEnabled = false;
    private byte _timeoutSeconds = 2;
    private volatile bool _reverseDirection;
    private volatile int _startOffset;
    private volatile byte _snareFlashIntensity = 255;
    private volatile byte _beatPulseIntensity = 180;
    private volatile byte _fogHazeBoost = 30;
    private volatile byte _maxBrightnessLimit = 255;

    // Layout-specific tuning parameters
    private WledRepeatStyle _repeatStyle = WledRepeatStyle.ContinuousLoop;
    private int _repeatTileSize = 32;
    private int _layoutGapLeds;
    private WledStretchInterpolation _stretchInterpolation = WledStretchInterpolation.PixelSharp;
    private int _stretchPadding;
    private WledPodColorOrder _podColorOrder = WledPodColorOrder.BlueRedGreenYellow;
    private int _podGapLeds;
    private WledCenterSplitDirection _centerSplitDirection = WledCenterSplitDirection.InsideOut;
    private int _centerSplitOffset;
    private WledDirectExcessMode _directExcessMode = WledDirectExcessMode.Blackout;
    private WledWashBlendMode _washBlendMode = WledWashBlendMode.HarmonicAverage;
    private int _gradientCycles = 1;
    private WledRgbwColor[] _transformBuffer = new WledRgbwColor[32];

    public event Action<WledRgbwColor[], int, bool, int>? OnFrameRendered;

    // Runtime state
    private bool _isEnabled;
    private bool _isSubscribed;
    private volatile bool _isStrobeActive;
    private volatile bool _isManualStrobeFlashOn;
    private volatile bool _isFogBreathingActive;
    private volatile bool _isStarPowerActive;
    private volatile byte _snareAccentLevel; // Decays over time (0 to 255)
    private volatile byte _beatAccentLevel;  // Decays over time (0 to 255)

    // Background decay timer for smooth rhythm impulses (snare flash, beat pulse)
    private Timer? _decayTimer;
    private MainWindowViewModel? _mainViewModel;

    // Raw StageKit parameters for state preservation
    private byte _stageKitBlueParam;
    private byte _stageKitRedParam;
    private byte _stageKitGreenParam;
    private byte _stageKitYellowParam;
    private byte _lastDrumsRaw;

    public WledSender Sender => _sender;
    public bool IsEnabled => _isEnabled;

    public WledTalker()
    {
        // Initialize decay timer (runs every ~16ms for ~60 FPS decay interpolation)
        _decayTimer = new Timer(OnDecayTimerTick, null, Timeout.Infinite, Timeout.Infinite);
        ApplyConfiguration(WledSettingsManager.Current);
    }

    /// <summary>
    /// Applies configuration directly from a WledSetting object.
    /// </summary>
    public void ApplyConfiguration(WledSetting setting)
    {
        UpdateConfiguration(
            setting.ServerIp,
            setting.ServerPort,
            (WledProtocol)setting.Protocol,
            setting.LedCount,
            (byte)setting.Brightness,
            (byte)setting.WhiteChannelBrightness,
            (WledLayoutMode)setting.LayoutMode,
            (WledWhiteStrobeMode)setting.WhiteStrobeMode,
            setting.StrobeMode,
            setting.RhythmicWhiteAccent,
            setting.SnareFlashEnabled,
            setting.StarPowerPulseEnabled,
            (byte)setting.TimeoutSeconds,
            setting.ReverseDirection,
            setting.StartOffset,
            (byte)setting.SnareFlashIntensity,
            (byte)setting.BeatPulseIntensity,
            (byte)setting.FogHazeBoost,
            (byte)setting.MaxBrightnessLimit
        );

        UpdateLayoutOptions(
            (WledRepeatStyle)setting.RepeatStyle,
            setting.RepeatTileSize,
            setting.LayoutGapLeds,
            (WledStretchInterpolation)setting.StretchInterpolation,
            setting.StretchPadding,
            (WledPodColorOrder)setting.PodColorOrder,
            setting.PodGapLeds,
            (WledCenterSplitDirection)setting.CenterSplitDirection,
            setting.CenterSplitOffset,
            (WledDirectExcessMode)setting.DirectExcessMode,
            (WledWashBlendMode)setting.WashBlendMode,
            setting.GradientCycles
        );
    }

    #region Enable / Disable & Lifecycle

    /// <summary>
    /// Binds the main view model reference for two-way state synchronization.
    /// </summary>
    public void SetViewModel(MainWindowViewModel viewModel)
    {
        _mainViewModel = viewModel;
    }

    /// <summary>
    /// Enables or disables direct WLED output.
    /// </summary>
    public async Task EnableWled(bool isEnabled, MainWindowViewModel? viewModel = null)
    {
        if (viewModel != null)
        {
            _mainViewModel = viewModel;
        }

        if (isEnabled)
        {
            _isEnabled = true;
            ApplyConfigurationFromViewModel();
            _sender.ConfigureEndpoint(_targetIp, _targetPort);
            SubscribeToEvents();

            // If not verified connected yet, initiate real probe & connection check via ConnectWledAsync
            if (_mainViewModel != null && !_mainViewModel.IsWledConnected)
            {
                _ = _mainViewModel.ConnectWledAsync();
                return;
            }

            StatusFooter.UpdateStatus("WLED", IntegrationStatus.Connected);
            UpdateStatusMessage($"WLED connected to {_targetIp}:{_targetPort} ({_protocol}, {_ledCount} LEDs).");
            return;
        }

        _isEnabled = false;
        _manualStrobeFlasher.Stop(SetManualStrobeFlashStateAsync);
        _decayTimer?.Change(Timeout.Infinite, Timeout.Infinite);

        UnsubscribeFromEvents();

        // Send closing blackout frame before disconnecting
        ClearAllLeds();
        RenderAndFlushFrame();

        StatusFooter.UpdateStatus("WLED", IntegrationStatus.Off);
        UpdateStatusMessage("WLED disabled.");

        if (_mainViewModel != null)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _mainViewModel.IsWledConnected = false;
            });
        }
    }

    /// <summary>
    /// Updates in-memory rendering, timing, and layout settings (e.g., brightness, layout, strobe behavior).
    /// Purely updates local state and buffer sizing without any blocking network or DNS calls.
    /// </summary>
    public void UpdateConfiguration(
        string ip,
        int port,
        WledProtocol protocol,
        int ledCount,
        byte brightness,
        byte whiteBrightness,
        WledLayoutMode layoutMode,
        WledWhiteStrobeMode whiteStrobeMode,
        int strobeMode,
        bool rhythmicAccent,
        bool snareFlash,
        bool starPowerPulse,
        byte timeoutSeconds,
        bool reverseDirection = false,
        int startOffset = 0,
        byte snareFlashIntensity = 255,
        byte beatPulseIntensity = 180,
        byte fogHazeBoost = 30,
        byte maxBrightnessLimit = 255)
    {
        lock (Lock)
        {
            _targetIp = string.IsNullOrWhiteSpace(ip) ? WledSettingsManager.Current.ServerIp : ip.Trim();
            _targetPort = port > 0 ? port : (_protocol == WledProtocol.RealtimeDrgbw || _protocol == WledProtocol.RealtimeDrgb ? WledSender.DefaultRealtimePort : WledSender.DefaultDdpPort);
            _protocol = protocol;
            _ledCount = Math.Max(1, Math.Min(1200, ledCount));
            _maxBrightnessLimit = maxBrightnessLimit;
            float rawMaster = brightness / 255.0f;
            float maxMaster = maxBrightnessLimit / 255.0f;
            _masterBrightness = Math.Min(rawMaster, maxMaster);
            _whiteBrightness = whiteBrightness / 255.0f;
            _layoutMode = layoutMode;
            _whiteStrobeMode = whiteStrobeMode;
            _strobeMode = strobeMode;
            _rhythmicWhiteAccentEnabled = rhythmicAccent;
            _snareFlashEnabled = snareFlash;
            _starPowerPulseEnabled = starPowerPulse;
            _timeoutSeconds = timeoutSeconds;
            _reverseDirection = reverseDirection;
            _startOffset = Math.Max(0, startOffset % _ledCount);
            _snareFlashIntensity = snareFlashIntensity;
            _beatPulseIntensity = beatPulseIntensity;
            _fogHazeBoost = fogHazeBoost;

            if (_outputBuffer.Length != _ledCount)
            {
                Array.Resize(ref _outputBuffer, _ledCount);
            }
            if (_transformBuffer.Length != _ledCount)
            {
                Array.Resize(ref _transformBuffer, _ledCount);
            }
        }

        if (_isEnabled)
        {
            RenderAndFlushFrame();
        }
    }

    /// <summary>
    /// Configures layout-specific fine-tuning options.
    /// </summary>
    public void UpdateLayoutOptions(
        WledRepeatStyle repeatStyle,
        int repeatTileSize,
        int layoutGapLeds,
        WledStretchInterpolation stretchInterpolation,
        int stretchPadding,
        WledPodColorOrder podColorOrder,
        int podGapLeds,
        WledCenterSplitDirection centerSplitDirection,
        int centerSplitOffset,
        WledDirectExcessMode directExcessMode,
        WledWashBlendMode washBlendMode,
        int gradientCycles)
    {
        lock (Lock)
        {
            _repeatStyle = repeatStyle;
            _repeatTileSize = Math.Clamp(repeatTileSize, 4, 128);
            _layoutGapLeds = Math.Clamp(layoutGapLeds, 0, 50);
            _stretchInterpolation = stretchInterpolation;
            _stretchPadding = Math.Clamp(stretchPadding, 0, Math.Max(0, (_ledCount - 4) / 2));
            _podColorOrder = podColorOrder;
            _podGapLeds = Math.Clamp(podGapLeds, 0, 50);
            _centerSplitDirection = centerSplitDirection;
            _centerSplitOffset = Math.Clamp(centerSplitOffset, -100, 100);
            _directExcessMode = directExcessMode;
            _washBlendMode = washBlendMode;
            _gradientCycles = Math.Clamp(gradientCycles, 1, 4);
        }

        if (_isEnabled)
        {
            RenderAndFlushFrame();
        }
    }

    /// <summary>
    /// Asynchronously configures the WLED target endpoint (including async DNS resolution for hostnames)
    /// without blocking the UI thread, and sends a sync frame if active.
    /// </summary>
    public async Task<bool> ConnectAsync(
        string ip,
        int port,
        WledProtocol protocol,
        int ledCount,
        byte brightness,
        byte whiteBrightness,
        WledLayoutMode layoutMode,
        WledWhiteStrobeMode whiteStrobeMode,
        int strobeMode,
        bool rhythmicAccent,
        bool snareFlash,
        bool starPowerPulse,
        byte timeoutSeconds,
        bool reverseDirection = false,
        int startOffset = 0,
        byte snareFlashIntensity = 255,
        byte beatPulseIntensity = 180,
        byte fogHazeBoost = 30,
        byte maxBrightnessLimit = 255)
    {
        UpdateConfiguration(
            ip, port, protocol, ledCount,
            brightness, whiteBrightness, layoutMode,
            whiteStrobeMode, strobeMode, rhythmicAccent,
            snareFlash, starPowerPulse, timeoutSeconds,
            reverseDirection, startOffset, snareFlashIntensity,
            beatPulseIntensity, fogHazeBoost, maxBrightnessLimit);

        bool success = await _sender.ConfigureEndpointAsync(_targetIp, _targetPort).ConfigureAwait(false);
        if (success && _isEnabled)
        {
            RenderAndFlushFrame();
        }

        return success;
    }

    /// <summary>
    /// Explicitly closes the WLED UDP endpoint, clears physical LED strip, and marks talker as idle.
    /// </summary>
    public void Disconnect()
    {
        lock (Lock)
        {
            ClearAllLeds();
            RenderAndFlushFrame();
            _sender.Dispose();
        }

        if (_mainViewModel != null)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _mainViewModel.IsWledConnected = false;
            });
        }
    }

    private void ApplyConfigurationFromViewModel()
    {
        if (_mainViewModel == null)
        {
            ApplyConfiguration(WledSettingsManager.Current);
            return;
        }

        UpdateConfiguration(
            _mainViewModel.WledServerIp ?? WledSettingsManager.Current.ServerIp,
            _mainViewModel.WledServerPort,
            (WledProtocol)_mainViewModel.WledProtocol,
            _mainViewModel.WledLedCount,
            (byte)_mainViewModel.WledBrightness,
            (byte)_mainViewModel.WledWhiteChannelBrightness,
            (WledLayoutMode)_mainViewModel.WledLayoutMode,
            (WledWhiteStrobeMode)_mainViewModel.WledWhiteStrobeMode,
            _mainViewModel.WledStrobeMode,
            _mainViewModel.WledRhythmicWhiteAccent,
            _mainViewModel.WledSnareFlashEnabled,
            _mainViewModel.WledStarPowerPulseEnabled,
            (byte)_mainViewModel.WledTimeoutSeconds,
            _mainViewModel.WledReverseDirection,
            _mainViewModel.WledStartOffset,
            (byte)_mainViewModel.WledSnareFlashIntensity,
            (byte)_mainViewModel.WledBeatPulseIntensity,
            (byte)_mainViewModel.WledFogHazeBoost,
            (byte)_mainViewModel.WledMaxBrightnessLimit
        );

        UpdateLayoutOptions(
            (WledRepeatStyle)_mainViewModel.WledRepeatStyle,
            _mainViewModel.WledRepeatTileSize,
            _mainViewModel.WledLayoutGapLeds,
            (WledStretchInterpolation)_mainViewModel.WledStretchInterpolation,
            _mainViewModel.WledStretchPadding,
            (WledPodColorOrder)_mainViewModel.WledPodColorOrder,
            _mainViewModel.WledPodGapLeds,
            (WledCenterSplitDirection)_mainViewModel.WledCenterSplitDirection,
            _mainViewModel.WledCenterSplitOffset,
            (WledDirectExcessMode)_mainViewModel.WledDirectExcessMode,
            (WledWashBlendMode)_mainViewModel.WledWashBlendMode,
            _mainViewModel.WledGradientCycles
        );
    }

    #endregion

    #region Event Subscription

    private void SubscribeToEvents()
    {
        if (_isSubscribed) return;
        _isSubscribed = true;

        UsbDeviceMonitor.OnStageKitCommand += OnStageKitEvent;

        if (_mainViewModel?.UdpIntake != null)
        {
            _mainViewModel.UdpIntake.PacketProcessed += OnUdpPacketProcessed;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (!_isSubscribed) return;
        _isSubscribed = false;

        UsbDeviceMonitor.OnStageKitCommand -= OnStageKitEvent;

        if (_mainViewModel?.UdpIntake != null)
        {
            _mainViewModel.UdpIntake.PacketProcessed -= OnUdpPacketProcessed;
        }
    }

    #endregion

    #region StageKit Event Dispatching

    private void OnStageKitEvent(StageKitTalker.CommandId commandId, byte parameter)
    {
        if (!_isEnabled) return;

        try
        {
            switch (commandId)
            {
                case StageKitTalker.CommandId.BlueLeds:
                    _stageKitBlueParam = parameter;
                    UpdateStageKitVirtualPod(0, parameter, WledRgbwColor.StageKitBlue);
                    break;

                case StageKitTalker.CommandId.RedLeds:
                    _stageKitRedParam = parameter;
                    UpdateStageKitVirtualPod(8, parameter, WledRgbwColor.StageKitRed);
                    break;

                case StageKitTalker.CommandId.GreenLeds:
                    _stageKitGreenParam = parameter;
                    UpdateStageKitVirtualPod(16, parameter, WledRgbwColor.StageKitGreen);
                    break;

                case StageKitTalker.CommandId.YellowLeds:
                    _stageKitYellowParam = parameter;
                    UpdateStageKitVirtualPod(24, parameter, WledRgbwColor.StageKitYellow);
                    break;

                case StageKitTalker.CommandId.StrobeOff:
                    StopStrobe();
                    break;

                case StageKitTalker.CommandId.StrobeSlow:
                case StageKitTalker.CommandId.StrobeMedium:
                case StageKitTalker.CommandId.StrobeFast:
                case StageKitTalker.CommandId.StrobeFastest:
                    StartStrobe(commandId, UdpIntake.BeatsPerMinute.Value);
                    break;

                case StageKitTalker.CommandId.FogOn:
                    _isFogBreathingActive = true;
                    break;

                case StageKitTalker.CommandId.FogOff:
                    _isFogBreathingActive = false;
                    break;

                case StageKitTalker.CommandId.DisableAll:
                    StopStrobe();
                    _isFogBreathingActive = false;
                    _isStarPowerActive = false;
                    _snareAccentLevel = 0;
                    _beatAccentLevel = 0;
                    _stageKitBlueParam = 0;
                    _stageKitRedParam = 0;
                    _stageKitGreenParam = 0;
                    _stageKitYellowParam = 0;
                    ClearAllLeds();
                    break;
            }

            RenderAndFlushFrame();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WLED OnStageKitEvent error: {ex.Message}");
        }
    }

    private void UpdateStageKitVirtualPod(int startIndex, byte parameter, WledRgbwColor color)
    {
        lock (Lock)
        {
            for (int i = 0; i < 8; i++)
            {
                bool isOn = (parameter & (1 << i)) != 0;
                _stageKitVirtualLeds[startIndex + i] = isOn ? color : WledRgbwColor.Black;
            }
        }
    }

    #endregion

    #region YARG Rhythm & Note Hit Processing

    /// <summary>
    /// Processes live UDP packets directly from YARG to trigger millisecond-accurate
    /// drum accents, snare flashes, beat pulses, and Star Power overdrive effects.
    /// </summary>
    private void OnUdpPacketProcessed(byte[] packet)
    {
        if (!_isEnabled || packet == null || packet.Length < UdpIntake.MIN_PACKET_SIZE) return;

        try
        {
            // 1. Drum / Snare Hit Detection
            if (_snareFlashEnabled && _rhythmicWhiteAccentEnabled)
            {
                byte drumsRaw = packet[(int)UdpIntake.ByteIndexName.DrumsNotes];
                // Check rising edge on drum bits (bit 1 is typically snare drum in Rock Band / YARG mapping)
                byte newlyPressedDrums = (byte)(drumsRaw & ~_lastDrumsRaw);
                _lastDrumsRaw = drumsRaw;

                if ((newlyPressedDrums & 0x02) != 0) // Snare hit!
                {
                    // Trigger instant high-intensity White pulse
                    _snareAccentLevel = _snareFlashIntensity;
                    _decayTimer?.Change(16, 16);
                    RenderAndFlushFrame();
                }
                else if ((newlyPressedDrums & 0x01) != 0) // Kick drum hit!
                {
                    // Subtle punch on kick
                    _snareAccentLevel = (byte)Math.Max((int)_snareAccentLevel, (int)(_snareFlashIntensity * 0.65f));
                    _decayTimer?.Change(16, 16);
                    RenderAndFlushFrame();
                }
            }

            // 2. Beat Line Pulse
            byte beat = packet[(int)UdpIntake.ByteIndexName.Beat];
            if (beat == 1 && _rhythmicWhiteAccentEnabled)
            {
                _beatAccentLevel = _beatPulseIntensity;
                _decayTimer?.Change(16, 16);
            }

            // 3. Star Power / Bonus Effect
            if (_starPowerPulseEnabled)
            {
                byte bonus = packet[(int)UdpIntake.ByteIndexName.BonusEffect];
                _isStarPowerActive = bonus == 1;
            }
            else
            {
                _isStarPowerActive = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WLED OnUdpPacketProcessed error: {ex.Message}");
        }
    }

    /// <summary>
    /// Smooth exponential decay timer tick for dynamic rhythm impulses (snare flash, beat pulses).
    /// Dormant when idle to consume 0% CPU resources.
    /// </summary>
    private void OnDecayTimerTick(object? state)
    {
        if (!_isEnabled) return;

        bool stateChanged = false;

        // Snare decay (~40ms quick flash)
        if (_snareAccentLevel > 0)
        {
            int next = _snareAccentLevel - 35;
            _snareAccentLevel = next <= 0 ? (byte)0 : (byte)next;
            stateChanged = true;
        }

        // Beat line pulse decay
        if (_beatAccentLevel > 0)
        {
            int next = _beatAccentLevel - 20;
            _beatAccentLevel = next <= 0 ? (byte)0 : (byte)next;
            stateChanged = true;
        }

        if (stateChanged && !_isStrobeActive)
        {
            RenderAndFlushFrame();
        }
        else if (!stateChanged)
        {
            // Accents reached zero: pause the background decay timer to consume 0 CPU cycles
            _decayTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    #endregion

    #region Strobe Control

    private void StartStrobe(StageKitTalker.CommandId commandId, float bpm)
    {
        _isStrobeActive = true;

        if (_strobeMode == StrobeOutputModes.ManualFlash)
        {
            _manualStrobeFlasher.Start(commandId, bpm, SetManualStrobeFlashStateAsync);
        }
        else
        {
            // Direct command mode: constantly on while strobe active
            _isManualStrobeFlashOn = true;
        }
    }

    private void StopStrobe()
    {
        _isStrobeActive = false;
        _isManualStrobeFlashOn = false;
        _manualStrobeFlasher.Stop(SetManualStrobeFlashStateAsync);
    }

    private Task SetManualStrobeFlashStateAsync(bool isOn, CancellationToken token)
    {
        if (!_isEnabled) return Task.CompletedTask;

        bool allowFlash = isOn && !UsbDeviceMonitor.IsOutputSuppressed;
        _isManualStrobeFlashOn = allowFlash;
        RenderAndFlushFrame();

        return Task.CompletedTask;
    }

    #endregion

    #region Frame Rendering & Mapping Engine

    /// <summary>
    /// Core rendering pipeline: combines ambient StageKit RGB colors, White strobe layers,
    /// and rhythmic White accents, mapping them onto the physical WLED strip.
    /// </summary>
    public void RenderAndFlushFrame()
    {
        if (!_isEnabled) return;

        lock (Lock)
        {
            if (_outputBuffer.Length != _ledCount)
            {
                Array.Resize(ref _outputBuffer, _ledCount);
            }

            // 1. Determine active strobe color if strobe is firing
            WledRgbwColor strobeColor = WledRgbwColor.Black;
            if (_isStrobeActive && _isManualStrobeFlashOn)
            {
                strobeColor = _whiteStrobeMode switch
                {
                    WledWhiteStrobeMode.FullRhythmAndStrobe => WledRgbwColor.PureWhite, // 100% W dedicated blinder
                    WledWhiteStrobeMode.StrobeOnly => WledRgbwColor.PureWhite,          // 100% W dedicated blinder
                    WledWhiteStrobeMode.Disabled => WledRgbwColor.RgbWhite,             // White diode off: traditional RGB white
                    _ => WledRgbwColor.PureWhite
                };
            }

            // 2. Compute rhythmic white accent boost (only in Full / Rhythm + Strobe mode)
            byte whiteAccent = 0;
            if (_rhythmicWhiteAccentEnabled && _whiteStrobeMode == WledWhiteStrobeMode.FullRhythmAndStrobe)
            {
                whiteAccent = Math.Max(_snareAccentLevel, _beatAccentLevel);
            }

            // 3. Map virtual StageKit LEDs to output buffer based on layout mode
            switch (_layoutMode)
            {
                case WledLayoutMode.TiledRepeat32:
                    RenderTiledRepeat32(strobeColor, whiteAccent);
                    break;

                case WledLayoutMode.ProportionalStretch:
                    RenderProportionalStretch(strobeColor, whiteAccent);
                    break;

                case WledLayoutMode.FourQuadrantPods:
                    RenderFourQuadrantPods(strobeColor, whiteAccent);
                    break;

                case WledLayoutMode.CenterSplit:
                    RenderCenterSplit(strobeColor, whiteAccent);
                    break;

                case WledLayoutMode.Direct1to1:
                    RenderDirect1to1(strobeColor, whiteAccent);
                    break;

                case WledLayoutMode.FullWash:
                    RenderFullWash(strobeColor, whiteAccent);
                    break;

                case WledLayoutMode.SmoothGradient:
                    RenderSmoothGradient(strobeColor, whiteAccent);
                    break;
            }

            // 4. Apply physical strip geometry transforms (reverse direction & start offset)
            ApplyGeometryTransforms();

            // 5. Send frame to WLED and broadcast telemetry to live visualizer
            _sender.SendFrame(_outputBuffer.AsSpan(0, _ledCount), _protocol, _timeoutSeconds);
            OnFrameRendered?.Invoke(_outputBuffer, _ledCount, _reverseDirection, _startOffset);
        }
    }

    private void ApplyGeometryTransforms()
    {
        if (!_reverseDirection && _startOffset == 0) return;

        if (_transformBuffer.Length != _ledCount)
        {
            Array.Resize(ref _transformBuffer, _ledCount);
        }

        Array.Copy(_outputBuffer, _transformBuffer, _ledCount);

        for (int i = 0; i < _ledCount; i++)
        {
            int srcIndex = _reverseDirection ? (_ledCount - 1 - i) : i;
            int destIndex = (_startOffset == 0) ? i : ((i + _startOffset) % _ledCount);
            _outputBuffer[destIndex] = _transformBuffer[srcIndex];
        }
    }

    private void RenderTiledRepeat32(WledRgbwColor strobeColor, byte whiteAccent)
    {
        bool hasStrobe = strobeColor != WledRgbwColor.Black;
        int tileSize = Math.Clamp(_repeatTileSize > 0 ? _repeatTileSize : StageKitLedCount, 4, 128);
        int gap = Math.Max(0, _layoutGapLeds);
        int period = tileSize + gap;

        for (int i = 0; i < _ledCount; i++)
        {
            if (hasStrobe)
            {
                _outputBuffer[i] = strobeColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
                continue;
            }

            int tileIndex = i / period;
            int posInPeriod = i % period;

            // In gap between repeating blocks
            if (posInPeriod >= tileSize)
            {
                _outputBuffer[i] = WledRgbwColor.Black;
                continue;
            }

            int offsetInTile = posInPeriod;

            // Scale offsetInTile to StageKitLedCount (0..31)
            int mappedOffset = (tileSize == StageKitLedCount)
                ? offsetInTile
                : (offsetInTile * StageKitLedCount / tileSize);
            mappedOffset = Math.Clamp(mappedOffset, 0, StageKitLedCount - 1);

            int virtualIndex;
            switch (_repeatStyle)
            {
                case WledRepeatStyle.PingPong:
                    virtualIndex = (tileIndex % 2 == 1)
                        ? (StageKitLedCount - 1 - mappedOffset)
                        : mappedOffset;
                    break;

                case WledRepeatStyle.InterleavedPodByPod:
                    // B1, R1, G1, Y1, B2, R2, G2, Y2... up to B8, R8, G8, Y8
                    // Clusters of 4 LEDs forming each of the 8 StageKit 4-color pods
                    {
                        int podIndex = mappedOffset / 4;   // 0..7 (Pod 1 to 8)
                        int colorInPod = mappedOffset % 4; // 0=Blue, 1=Red, 2=Green, 3=Yellow
                        virtualIndex = (colorInPod * 8) + podIndex;
                    }
                    break;

                case WledRepeatStyle.InterleavedPingPong:
                    // Interleaved pods in ping-pong reversal (Pod 1→8 then Pod 8→1)
                    {
                        int podIndex = mappedOffset / 4;
                        if (tileIndex % 2 == 1) podIndex = 7 - podIndex;
                        int colorInPod = mappedOffset % 4;
                        virtualIndex = (colorInPod * 8) + podIndex;
                    }
                    break;

                case WledRepeatStyle.PairedInterleaved:
                    // B1,B2, R1,R2, G1,G2, Y1,Y2...
                    // 2 LEDs per color grouped in 8-LED fixture clusters
                    {
                        int cluster = mappedOffset / 8;     // 0..3
                        int posInCluster = mappedOffset % 8; // 0..7
                        int color = posInCluster / 2;       // 0..3 (B, R, G, Y)
                        int pairOffset = posInCluster % 2;  // 0..1
                        virtualIndex = (color * 8) + (cluster * 2) + pairOffset;
                    }
                    break;

                case WledRepeatStyle.CrossCoolWarm:
                    // B1, G1, R1, Y1, B2, G2, R2, Y2...
                    // Alternates cold (Blue, Green) and warm (Red, Yellow) colors
                    {
                        int podIndex = mappedOffset / 4;
                        int step = mappedOffset % 4;
                        int color = step switch { 0 => 0, 1 => 2, 2 => 1, _ => 3 }; // B, G, R, Y
                        virtualIndex = (color * 8) + podIndex;
                    }
                    break;

                case WledRepeatStyle.ContinuousLoop:
                default:
                    virtualIndex = mappedOffset;
                    break;
            }

            virtualIndex = Math.Clamp(virtualIndex, 0, StageKitLedCount - 1);

            var baseColor = _stageKitVirtualLeds[virtualIndex];
            baseColor = ApplyDynamicLayers(baseColor, whiteAccent);
            _outputBuffer[i] = baseColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
        }
    }

    private void RenderProportionalStretch(WledRgbwColor strobeColor, byte whiteAccent)
    {
        bool hasStrobe = strobeColor != WledRgbwColor.Black;
        int pad = Math.Clamp(_stretchPadding, 0, Math.Max(0, (_ledCount - 4) / 2));
        int activeLength = Math.Max(1, _ledCount - (pad * 2));

        for (int i = 0; i < _ledCount; i++)
        {
            if (hasStrobe)
            {
                _outputBuffer[i] = strobeColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
                continue;
            }

            if (i < pad || i >= _ledCount - pad)
            {
                _outputBuffer[i] = WledRgbwColor.Black;
                continue;
            }

            int activeIdx = i - pad;
            if (_stretchInterpolation == WledStretchInterpolation.SmoothBlend && activeLength > 1)
            {
                float ratio = (float)activeIdx * (StageKitLedCount - 1) / (activeLength - 1);
                int v0 = Math.Clamp((int)Math.Floor(ratio), 0, StageKitLedCount - 1);
                int v1 = Math.Clamp(v0 + 1, 0, StageKitLedCount - 1);
                float t = ratio - v0;

                var c0 = ApplyDynamicLayers(_stageKitVirtualLeds[v0], whiteAccent);
                var c1 = ApplyDynamicLayers(_stageKitVirtualLeds[v1], whiteAccent);
                var blended = WledRgbwColor.Lerp(c0, c1, t);
                _outputBuffer[i] = blended.ApplyBrightness(_masterBrightness, _whiteBrightness);
            }
            else
            {
                int virtualIndex = (int)((long)activeIdx * StageKitLedCount / activeLength);
                virtualIndex = Math.Clamp(virtualIndex, 0, StageKitLedCount - 1);

                var baseColor = _stageKitVirtualLeds[virtualIndex];
                baseColor = ApplyDynamicLayers(baseColor, whiteAccent);
                _outputBuffer[i] = baseColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
            }
        }
    }

    private void RenderFourQuadrantPods(WledRgbwColor strobeColor, byte whiteAccent)
    {
        bool hasStrobe = strobeColor != WledRgbwColor.Black;
        int gap = Math.Max(0, _podGapLeds);
        int totalGaps = 3 * gap;
        int usableLeds = Math.Max(4, _ledCount - totalGaps);
        int podSize = usableLeds / 4;
        int remainder = usableLeds % 4;

        Span<int> podStarts = stackalloc int[4];
        Span<int> podEnds = stackalloc int[4];
        int currentPos = 0;
        for (int q = 0; q < 4; q++)
        {
            podStarts[q] = currentPos;
            int thisPodSize = podSize + (q < remainder ? 1 : 0);
            podEnds[q] = currentPos + thisPodSize;
            currentPos = podEnds[q] + gap;
        }

        for (int i = 0; i < _ledCount; i++)
        {
            if (hasStrobe)
            {
                _outputBuffer[i] = strobeColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
                continue;
            }

            int podIndex = -1;
            for (int q = 0; q < 4; q++)
            {
                if (i >= podStarts[q] && i < podEnds[q])
                {
                    podIndex = q;
                    break;
                }
            }

            if (podIndex == -1)
            {
                // In gap between pods
                _outputBuffer[i] = WledRgbwColor.Black;
                continue;
            }

            int thisPodSize = podEnds[podIndex] - podStarts[podIndex];
            int offsetInPod = i - podStarts[podIndex];
            int virtualBase = GetPodVirtualBase(podIndex, _podColorOrder);
            int subIndex = thisPodSize > 0 ? (offsetInPod * 8 / thisPodSize) : 0;
            int virtualIndex = Math.Clamp(virtualBase + subIndex, 0, StageKitLedCount - 1);

            var baseColor = _stageKitVirtualLeds[virtualIndex];
            baseColor = ApplyDynamicLayers(baseColor, whiteAccent);
            _outputBuffer[i] = baseColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
        }
    }

    private static int GetPodVirtualBase(int podIndex, WledPodColorOrder order) => order switch
    {
        WledPodColorOrder.BlueRedGreenYellow => podIndex * 8,
        WledPodColorOrder.BlueGreenRedYellow => podIndex switch { 0 => 0, 1 => 16, 2 => 8, _ => 24 },
        WledPodColorOrder.RedBlueYellowGreen => podIndex switch { 0 => 8, 1 => 0, 2 => 24, _ => 16 },
        WledPodColorOrder.SymmetricalMirror => podIndex switch { 0 => 0, 1 => 8, 2 => 8, _ => 0 },
        _ => podIndex * 8
    };

    private void RenderCenterSplit(WledRgbwColor strobeColor, byte whiteAccent)
    {
        bool hasStrobe = strobeColor != WledRgbwColor.Black;
        int center = Math.Clamp((_ledCount / 2) + _centerSplitOffset, 1, _ledCount - 1);
        int leftSpan = center;
        int rightSpan = _ledCount - center;

        for (int i = 0; i < _ledCount; i++)
        {
            if (hasStrobe)
            {
                _outputBuffer[i] = strobeColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
                continue;
            }

            float normalizedDist = (i < center)
                ? (float)(center - 1 - i) / Math.Max(1, leftSpan)
                : (float)(i - center) / Math.Max(1, rightSpan);
            normalizedDist = Math.Clamp(normalizedDist, 0f, 1f);

            float factor = (_centerSplitDirection == WledCenterSplitDirection.InsideOut)
                ? normalizedDist
                : (1.0f - normalizedDist);

            int virtualIndex = Math.Clamp((int)(factor * StageKitLedCount), 0, StageKitLedCount - 1);

            var baseColor = _stageKitVirtualLeds[virtualIndex];
            baseColor = ApplyDynamicLayers(baseColor, whiteAccent);
            _outputBuffer[i] = baseColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
        }
    }

    private void RenderDirect1to1(WledRgbwColor strobeColor, byte whiteAccent)
    {
        bool hasStrobe = strobeColor != WledRgbwColor.Black;

        WledRgbwColor ambientWash = WledRgbwColor.Black;
        if (_directExcessMode == WledDirectExcessMode.AmbientWash && !hasStrobe)
        {
            int r = 0, g = 0, b = 0, count = 0;
            for (int k = 0; k < StageKitLedCount; k++)
            {
                if (_stageKitVirtualLeds[k].R > 0 || _stageKitVirtualLeds[k].G > 0 || _stageKitVirtualLeds[k].B > 0)
                {
                    r += _stageKitVirtualLeds[k].R;
                    g += _stageKitVirtualLeds[k].G;
                    b += _stageKitVirtualLeds[k].B;
                    count++;
                }
            }
            if (count > 0)
            {
                ambientWash = new WledRgbwColor((byte)(r / (count * 4)), (byte)(g / (count * 4)), (byte)(b / (count * 4)), (byte)(whiteAccent / 4));
            }
        }

        for (int i = 0; i < _ledCount; i++)
        {
            if (hasStrobe)
            {
                _outputBuffer[i] = strobeColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
                continue;
            }

            WledRgbwColor baseColor;
            if (i < StageKitLedCount)
            {
                baseColor = _stageKitVirtualLeds[i];
            }
            else
            {
                baseColor = _directExcessMode switch
                {
                    WledDirectExcessMode.RepeatPattern => _stageKitVirtualLeds[i % StageKitLedCount],
                    WledDirectExcessMode.AmbientWash => ambientWash,
                    _ => WledRgbwColor.Black
                };
            }

            baseColor = ApplyDynamicLayers(baseColor, whiteAccent);
            _outputBuffer[i] = baseColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
        }
    }

    private WledRgbwColor ApplyDynamicLayers(WledRgbwColor baseColor, byte whiteAccent)
    {
        // 1. Blend Fog ambient haze if fog machine is active
        if (_isFogBreathingActive && _fogHazeBoost > 0)
        {
            byte h = _fogHazeBoost;
            baseColor = WledRgbwColor.BlendMax(baseColor, new WledRgbwColor((byte)(h / 3), (byte)(h / 3), (byte)(h * 2 / 3), h));
        }

        // 2. Blend Star Power overdrive (subtle golden/white shimmer)
        if (_isStarPowerActive && _starPowerPulseEnabled)
        {
            if (_whiteStrobeMode == WledWhiteStrobeMode.FullRhythmAndStrobe)
            {
                baseColor = WledRgbwColor.BlendMax(baseColor, new WledRgbwColor(80, 80, 20, 120));
            }
            else
            {
                // When W diode is reserved for Strobe only or disabled, Star Power uses golden RGB without W
                baseColor = WledRgbwColor.BlendMax(baseColor, new WledRgbwColor(80, 80, 20, 0));
            }
        }

        // 3. Modulate dedicated 4th (W) channel for rhythmic accents (snare hits, beat pulses)
        if (whiteAccent > 0 && _whiteStrobeMode == WledWhiteStrobeMode.FullRhythmAndStrobe)
        {
            baseColor = baseColor.WithWhite((byte)Math.Max(baseColor.W, whiteAccent));
        }
        else if (_whiteStrobeMode == WledWhiteStrobeMode.Disabled || _whiteStrobeMode == WledWhiteStrobeMode.StrobeOnly)
        {
            // In StrobeOnly and Disabled modes, regular gameplay keeps White diode at 0
            baseColor = baseColor.WithWhite(0);
        }

        return baseColor;
    }

    private void RenderFullWash(WledRgbwColor strobeColor, byte whiteAccent)
    {
        bool hasStrobe = strobeColor != WledRgbwColor.Black;

        if (hasStrobe)
        {
            var strobed = strobeColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
            for (int i = 0; i < _ledCount; i++) _outputBuffer[i] = strobed;
            return;
        }

        WledRgbwColor washColor = WledRgbwColor.Black;

        if (_washBlendMode == WledWashBlendMode.DominantColor)
        {
            // Determine which pod has the highest count of active LEDs
            int bCount = 0, rCount = 0, gCount = 0, yCount = 0;
            for (int k = 0; k < 8; k++) if (_stageKitVirtualLeds[k].B > 0) bCount++;
            for (int k = 8; k < 16; k++) if (_stageKitVirtualLeds[k].R > 0) rCount++;
            for (int k = 16; k < 24; k++) if (_stageKitVirtualLeds[k].G > 0) gCount++;
            for (int k = 24; k < 32; k++) if (_stageKitVirtualLeds[k].R > 0 && _stageKitVirtualLeds[k].G > 0) yCount++;

            int maxCount = Math.Max(Math.Max(bCount, rCount), Math.Max(gCount, yCount));
            if (maxCount > 0)
            {
                if (maxCount == bCount) washColor = WledRgbwColor.StageKitBlue;
                else if (maxCount == rCount) washColor = WledRgbwColor.StageKitRed;
                else if (maxCount == gCount) washColor = WledRgbwColor.StageKitGreen;
                else washColor = WledRgbwColor.StageKitYellow;
            }
        }
        else
        {
            // Compute average active stage color
            int sumR = 0, sumG = 0, sumB = 0;
            int activeCount = 0;

            for (int i = 0; i < StageKitLedCount; i++)
            {
                var c = _stageKitVirtualLeds[i];
                if (c.R > 0 || c.G > 0 || c.B > 0)
                {
                    sumR += c.R;
                    sumG += c.G;
                    sumB += c.B;
                    activeCount++;
                }
            }

            if (activeCount > 0)
            {
                washColor = new WledRgbwColor(
                    (byte)(sumR / activeCount),
                    (byte)(sumG / activeCount),
                    (byte)(sumB / activeCount),
                    whiteAccent
                );
            }
        }

        if (whiteAccent > 0 && washColor.W == 0 && _whiteStrobeMode == WledWhiteStrobeMode.FullRhythmAndStrobe)
        {
            washColor = washColor.WithWhite(whiteAccent);
        }

        if (_isStarPowerActive)
        {
            washColor = WledRgbwColor.BlendMax(washColor, new WledRgbwColor(80, 80, 20, 120));
        }

        var finalWash = washColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
        for (int i = 0; i < _ledCount; i++)
        {
            _outputBuffer[i] = finalWash;
        }
    }

    /// <summary>
    /// Interpolates smooth color gradients from StageKit Pod 1 to 8 across the strip LEDs.
    /// Rotations flow like waves across the entire physical strip without jarring discrete steps.
    /// </summary>
    private void RenderSmoothGradient(WledRgbwColor strobeColor, byte whiteAccent)
    {
        bool hasStrobe = strobeColor != WledRgbwColor.Black;

        if (hasStrobe)
        {
            var strobed = strobeColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
            for (int i = 0; i < _ledCount; i++) _outputBuffer[i] = strobed;
            return;
        }

        // Calculate the 8 StageKit Pod blended colors (Pod 1 to Pod 8) from active B, R, G, Y parameters
        Span<WledRgbwColor> podColors = stackalloc WledRgbwColor[8];
        int activePodCount = 0;

        for (int k = 0; k < 8; k++)
        {
            bool isB = (_stageKitBlueParam & (1 << k)) != 0;
            bool isR = (_stageKitRedParam & (1 << k)) != 0;
            bool isG = (_stageKitGreenParam & (1 << k)) != 0;
            bool isY = (_stageKitYellowParam & (1 << k)) != 0;

            var (r, g, b, isActive) = StageKitColorBlender.BlendPod(isB, isR, isG, isY);
            if (isActive)
            {
                podColors[k] = new WledRgbwColor(r, g, b, 0);
                activePodCount++;
            }
            else
            {
                podColors[k] = WledRgbwColor.Black;
            }
        }

        if (activePodCount == 0 && whiteAccent == 0 && !_isStarPowerActive && !_isFogBreathingActive)
        {
            for (int i = 0; i < _ledCount; i++)
            {
                _outputBuffer[i] = WledRgbwColor.Black;
            }
            return;
        }

        int n = _ledCount;
        if (n == 1)
        {
            var single = ApplyDynamicLayers(podColors[0], whiteAccent);
            _outputBuffer[0] = single.ApplyBrightness(_masterBrightness, _whiteBrightness);
            return;
        }

        int cycles = Math.Clamp(_gradientCycles, 1, 4);
        int cycleLength = Math.Max(1, n / cycles);

        // Distribute the 8 Pod Colors across strip LEDs with smooth linear interpolation
        for (int i = 0; i < n; i++)
        {
            int posInCycle = i % cycleLength;
            double pos = (double)posInCycle / Math.Max(1, cycleLength - 1) * 7.0; // Continuous range [0.0 .. 7.0]
            int k0 = (int)pos;
            int k1 = Math.Min(7, k0 + 1);
            double t = pos - k0;

            var c0 = podColors[k0];
            var c1 = podColors[k1];

            byte r = (byte)Math.Clamp(c0.R + (c1.R - c0.R) * t, 0, 255);
            byte g = (byte)Math.Clamp(c0.G + (c1.G - c0.G) * t, 0, 255);
            byte b = (byte)Math.Clamp(c0.B + (c1.B - c0.B) * t, 0, 255);

            var interpolated = new WledRgbwColor(r, g, b, 0);
            interpolated = ApplyDynamicLayers(interpolated, whiteAccent);
            _outputBuffer[i] = interpolated.ApplyBrightness(_masterBrightness, _whiteBrightness);
        }
    }

    private void ClearAllLeds()
    {
        lock (Lock)
        {
            Array.Fill(_stageKitVirtualLeds, WledRgbwColor.Black);
            Array.Fill(_outputBuffer, WledRgbwColor.Black);
        }
    }

    #endregion

    #region Diagnostics & Test Actions

    /// <summary>
    /// Test action: fires an authentic high-intensity concert blinder using the dedicated White diode (W=255) for 500ms.
    /// </summary>
    public async Task TestWhiteBlinderAsync()
    {
        if (!_isEnabled) return;

        lock (Lock)
        {
            Array.Fill(_outputBuffer, WledRgbwColor.PureWhite.ApplyBrightness(_masterBrightness, _whiteBrightness));
            _sender.SendFrame(_outputBuffer.AsSpan(0, _ledCount), _protocol, _timeoutSeconds);
            OnFrameRendered?.Invoke(_outputBuffer, _ledCount, _reverseDirection, _startOffset);
        }

        await Task.Delay(500);

        RenderAndFlushFrame();
    }

    /// <summary>
    /// Test action: paints the entire strip with a single target color for hardware verification.
    /// </summary>
    public async Task TestColorAsync(WledRgbwColor color, int durationMs = 1000)
    {
        if (!_isEnabled) return;

        lock (Lock)
        {
            Array.Fill(_outputBuffer, color.ApplyBrightness(_masterBrightness, _whiteBrightness));
            _sender.SendFrame(_outputBuffer.AsSpan(0, _ledCount), _protocol, _timeoutSeconds);
            OnFrameRendered?.Invoke(_outputBuffer, _ledCount, _reverseDirection, _startOffset);
        }

        await Task.Delay(durationMs);

        RenderAndFlushFrame();
    }

    /// <summary>
    /// Test action: paints the 4 StageKit pods (Blue, Red, Green, Yellow) distinctly across the strip.
    /// </summary>
    public async Task TestStageKitPodsAsync()
    {
        if (!_isEnabled) return;

        lock (Lock)
        {
            int podSize = Math.Max(1, _ledCount / 4);
            for (int i = 0; i < _ledCount; i++)
            {
                int pod = Math.Min(3, i / podSize);
                var c = pod switch
                {
                    0 => WledRgbwColor.StageKitBlue,
                    1 => WledRgbwColor.StageKitRed,
                    2 => WledRgbwColor.StageKitGreen,
                    3 => WledRgbwColor.StageKitYellow,
                    _ => WledRgbwColor.Black
                };
                _outputBuffer[i] = c.ApplyBrightness(_masterBrightness, _whiteBrightness);
            }

            _sender.SendFrame(_outputBuffer.AsSpan(0, _ledCount), _protocol, _timeoutSeconds);
            OnFrameRendered?.Invoke(_outputBuffer, _ledCount, _reverseDirection, _startOffset);
        }

        await Task.Delay(1500);

        RenderAndFlushFrame();
    }

    /// <summary>
    /// Test action: runs a fast RGB chase animation to verify LED addressing and order.
    /// </summary>
    public async Task TestColorChaseAsync(CancellationToken token = default)
    {
        if (!_isEnabled) return;

        WledRgbwColor[] testColors = [
            WledRgbwColor.StageKitBlue,
            WledRgbwColor.StageKitRed,
            WledRgbwColor.StageKitGreen,
            WledRgbwColor.StageKitYellow,
            WledRgbwColor.PureWhite
        ];

        foreach (var col in testColors)
        {
            if (token.IsCancellationRequested) break;

            lock (Lock)
            {
                Array.Fill(_outputBuffer, col.ApplyBrightness(_masterBrightness, _whiteBrightness));
                _sender.SendFrame(_outputBuffer.AsSpan(0, _ledCount), _protocol, _timeoutSeconds);
                OnFrameRendered?.Invoke(_outputBuffer, _ledCount, _reverseDirection, _startOffset);
            }

            await Task.Delay(250, token);
        }

        RenderAndFlushFrame();
    }

    /// <summary>
    /// Immediately blacks out all LEDs on the strip.
    /// </summary>
    public void Blackout()
    {
        ClearAllLeds();
        if (_isEnabled)
        {
            RenderAndFlushFrame();
        }
    }

    private void UpdateStatusMessage(string message)
    {
        if (_mainViewModel != null)
        {
            _mainViewModel.WledStatus = message;
        }
    }

    #endregion

    /// <summary>
    /// Actively probes the target WLED controller to verify if it is genuinely reachable,
    /// resolving hostnames (supporting mDNS .local fallbacks) and testing HTTP JSON / Ping.
    /// </summary>
    public static async Task<(bool isReachable, WledDeviceInfo? deviceInfo, string resolvedEndpoint, string errorMessage)> ProbeControllerAsync(
        string ipOrHost, int timeoutMs = 1500)
    {
        if (string.IsNullOrWhiteSpace(ipOrHost))
        {
            return (false, null, string.Empty, "Controller IP address or hostname cannot be empty.");
        }

        string raw = ipOrHost.Trim();
        string targetHost = raw;
        IPAddress? targetIp = null;

        // 1. Direct IP address
        if (IPAddress.TryParse(raw, out var parsedIp))
        {
            targetIp = parsedIp;
            targetHost = parsedIp.ToString();
        }
        else
        {
            // 2. Hostname resolution (with automatic .local fallback for mDNS on Windows)
            try
            {
                var entry = await Dns.GetHostEntryAsync(raw).ConfigureAwait(false);
                if (entry.AddressList.Length > 0)
                {
                    targetIp = entry.AddressList[0];
                    targetHost = targetIp.ToString();
                }
            }
            catch
            {
                // If raw hostname has no dot (e.g. "wled"), try mDNS "wled.local"
                if (!raw.Contains('.'))
                {
                    try
                    {
                        var entry = await Dns.GetHostEntryAsync(raw + ".local").ConfigureAwait(false);
                        if (entry.AddressList.Length > 0)
                        {
                            targetIp = entry.AddressList[0];
                            targetHost = targetIp.ToString();
                        }
                    }
                    catch
                    {
                        targetIp = null;
                    }
                }
            }

            if (targetIp == null)
            {
                return (false, null, raw, $"Unable to resolve hostname '{raw}'. Device may be offline or not connected to the local network.");
            }
        }

        // 3. Probe WLED HTTP JSON API (/json/info)
        WledDeviceInfo? detectedInfo = null;
        try
        {
            detectedInfo = await QueryDeviceInfoAsync(targetHost, timeoutMs).ConfigureAwait(false);
            if (detectedInfo == null && targetHost != raw)
            {
                detectedInfo = await QueryDeviceInfoAsync(raw, timeoutMs).ConfigureAwait(false);
            }
        }
        catch
        {
            detectedInfo = null;
        }

        if (detectedInfo != null)
        {
            // Fully confirmed active WLED controller responding on the network
            return (true, detectedInfo, targetHost, string.Empty);
        }

        // 4. Fallback Probe: ICMP Ping (in case HTTP port 80 is custom or restricted)
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(targetIp, Math.Min(1000, timeoutMs)).ConfigureAwait(false);
            if (reply.Status == IPStatus.Success)
            {
                return (true, null, targetHost, string.Empty);
            }
        }
        catch
        {
            // Ping unavailable or restricted
        }

        // 5. Neither HTTP nor Ping responded
        return (false, null, targetHost, $"Controller at '{raw}' ({targetHost}) did not respond. Check power, Wi-Fi, and IP address.");
    }

    /// <summary>
    /// Asynchronously queries WLED controller hardware parameters via HTTP JSON API (/json/info).
    /// Discovers strip type (RGB vs RGBW), LED count, and device name without blocking UDP streaming.
    /// </summary>
    public static async Task<WledDeviceInfo?> QueryDeviceInfoAsync(string ipOrHost, int timeoutMs = 1500)
    {
        if (string.IsNullOrWhiteSpace(ipOrHost)) return null;

        string trimmed = ipOrHost.Trim();
        var result = await TryGetDeviceInfoAsync(trimmed, timeoutMs).ConfigureAwait(false);
        if (result == null && !trimmed.Contains('.'))
        {
            result = await TryGetDeviceInfoAsync(trimmed + ".local", timeoutMs).ConfigureAwait(false);
        }
        return result;
    }

    private static async Task<WledDeviceInfo?> TryGetDeviceInfoAsync(string host, int timeoutMs)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
            string url = $"http://{host}/json/info";
            string json = await client.GetStringAsync(url).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "WLED" : "WLED";
            string ver = root.TryGetProperty("ver", out var v) ? v.GetString() ?? "" : "";
            int count = 0;
            bool isRgbw = false;

            if (root.TryGetProperty("leds", out var leds))
            {
                if (leds.TryGetProperty("count", out var c)) count = c.GetInt32();
                if (leds.TryGetProperty("rgbw", out var rw)) isRgbw = rw.GetBoolean();
            }

            return new WledDeviceInfo(name, count, isRgbw, ver);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _decayTimer?.Dispose();
        _decayTimer = null;

        UnsubscribeFromEvents();
        _sender.Dispose();
    }
}
