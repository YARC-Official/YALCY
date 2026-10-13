using System;
using System.Buffers.Binary;
using System.Collections.Generic;
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

    // Shared HTTP client for lightweight controller probing and hardware brightness state synchronization
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMilliseconds(2000) };

    // UDP transmission engine
    private readonly WledSender _sender = new();

    // Strobe and flash controllers
    private readonly ManualStrobeFlasher _manualStrobeFlasher =
        new(ex => Console.WriteLine($"WLED manual strobe error: {ex.Message}"));

    // Virtual StageKit 32-LED state buffer (8 Blue [0..7], 8 Red [8..15], 8 Green [16..23], 8 Yellow [24..31])
    private readonly WledRgbwColor[] _stageKitVirtualLeds = new WledRgbwColor[StageKitLedCount];

    // Reference calibration virtual LEDs buffer (4 pods: Blue, Red, Green, Yellow + dedicated White test diodes)
    private readonly WledRgbwColor[] _calibrationVirtualLeds = new WledRgbwColor[StageKitLedCount];
    private volatile bool _isCalibrationPreviewEnabled;
    private volatile bool _isAutoPreviewActive;
    private Timer? _autoPreviewTimer;
    private const int AutoPreviewDurationMs = 4000;

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

    // Multi-Strip Routing & Segment Management
    private WledMultiStripMode _multiStripMode = WledMultiStripMode.Contiguous;
    private readonly List<WledSegment> _activeSegments = new();

    public IReadOnlyList<WledSegment> ActiveSegments
    {
        get
        {
            lock (Lock)
            {
                return _activeSegments.ToArray();
            }
        }
    }

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

    // Heartbeat keep-alive timer (~10 Hz) to maintain the active lighting state on WLED.
    // WLED controllers drop realtime/DDP mode if no packets arrive within 2–2.5 seconds.
    // This heartbeat ensures static stage colors persist seamlessly without blacking out.
    private Timer? _heartbeatTimer;
    private const int HeartbeatIntervalMs = 100;
    private int _heartbeatTelemetryCounter;

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
        _heartbeatTimer = new Timer(OnHeartbeatTimerTick, null, Timeout.Infinite, Timeout.Infinite);
        _autoPreviewTimer = new Timer(OnAutoPreviewTimerTick, null, Timeout.Infinite, Timeout.Infinite);
        RefreshCalibrationVirtualLeds();
        ApplyConfiguration(WledSettingsManager.Current);
    }

    #region Real-Time Diode Calibration & Live Preview Engine

    /// <summary>
    /// Checks whether any virtual StageKit lights are actively illuminated by incoming cues.
    /// </summary>
    public bool HasActiveStageKitCues()
    {
        lock (Lock)
        {
            for (int i = 0; i < StageKitLedCount; i++)
            {
                if (_stageKitVirtualLeds[i] != WledRgbwColor.Black)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Temporarily activates calibration preview mode for 4 seconds during slider manipulation,
    /// rendering live reference pods (Blue, Red, Green, Yellow) and dedicated White diodes
    /// so brightness and white balance adjustments are visible in real time.
    /// </summary>
    public void TriggerCalibrationPreview()
    {
        _isAutoPreviewActive = true;
        _autoPreviewTimer?.Change(AutoPreviewDurationMs, Timeout.Infinite);
        RefreshCalibrationVirtualLeds();
        RenderAndFlushFrame(forceVisualizerOnly: true);
    }

    /// <summary>
    /// Toggles persistent calibration preview mode on/off from the UI.
    /// </summary>
    public void SetCalibrationPreview(bool enabled)
    {
        _isCalibrationPreviewEnabled = enabled;
        RefreshCalibrationVirtualLeds();
        RenderAndFlushFrame(forceVisualizerOnly: true);
    }

    /// <summary>
    /// Cancels active calibration preview and restores stage/blackout output.
    /// </summary>
    public void ClearCalibrationPreview()
    {
        _autoPreviewTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _isAutoPreviewActive = false;
        _isCalibrationPreviewEnabled = false;
        if (!HasActiveStageKitCues())
        {
            RenderAndFlushFrame(forceVisualizerOnly: true);
        }
    }

    private void OnAutoPreviewTimerTick(object? state)
    {
        _isAutoPreviewActive = false;
        if (!_isCalibrationPreviewEnabled && !HasActiveStageKitCues())
        {
            RenderAndFlushFrame(forceVisualizerOnly: true);
        }
    }

    /// <summary>
    /// Populates calibration reference buffer with 4 distinct StageKit pods and dedicated White diodes.
    /// </summary>
    private void RefreshCalibrationVirtualLeds()
    {
        lock (Lock)
        {
            bool isRgbw = _protocol is WledProtocol.DdpRgbw or WledProtocol.RealtimeDrgbw;

            for (int pod = 0; pod < 4; pod++)
            {
                int baseIndex = pod * 8;
                WledRgbwColor podColor = pod switch
                {
                    0 => WledRgbwColor.StageKitBlue,
                    1 => WledRgbwColor.StageKitRed,
                    2 => WledRgbwColor.StageKitGreen,
                    _ => WledRgbwColor.StageKitYellow
                };

                for (int i = 0; i < 6; i++)
                {
                    _calibrationVirtualLeds[baseIndex + i] = podColor;
                }

                // 2 Dedicated White test diodes per pod for independent white channel calibration
                WledRgbwColor whiteColor = isRgbw ? WledRgbwColor.PureWhite : WledRgbwColor.RgbWhite;
                _calibrationVirtualLeds[baseIndex + 6] = whiteColor;
                _calibrationVirtualLeds[baseIndex + 7] = whiteColor;
            }
        }
    }

    /// <summary>
    /// Determines which virtual LED buffer to render: active gameplay cues, calibration pattern, or idle black.
    /// </summary>
    private WledRgbwColor[] GetActiveSourceLeds()
    {
        if (HasActiveStageKitCues())
        {
            return _stageKitVirtualLeds;
        }

        if (_isCalibrationPreviewEnabled || _isAutoPreviewActive)
        {
            return _calibrationVirtualLeds;
        }

        return _stageKitVirtualLeds;
    }

    #endregion

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

        UpdateMultiStripMode((WledMultiStripMode)setting.MultiStripMode);
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

            _heartbeatTimer?.Change(HeartbeatIntervalMs, HeartbeatIntervalMs);

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
        _heartbeatTimer?.Change(Timeout.Infinite, Timeout.Infinite);

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
            _whiteBrightness = Math.Min(whiteBrightness / 255.0f, maxMaster);
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

        if (_isEnabled || _isCalibrationPreviewEnabled || _isAutoPreviewActive)
        {
            RenderAndFlushFrame(forceVisualizerOnly: true);
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

        if (_isEnabled || _isCalibrationPreviewEnabled || _isAutoPreviewActive)
        {
            RenderAndFlushFrame(forceVisualizerOnly: true);
        }
    }

    /// <summary>
    /// Configures multi-strip routing strategy across multiple physical strips / WLED segments.
    /// </summary>
    public void UpdateMultiStripMode(WledMultiStripMode mode, IEnumerable<WledSegment>? segments = null)
    {
        lock (Lock)
        {
            _multiStripMode = mode;
            if (segments != null)
            {
                _activeSegments.Clear();
                _activeSegments.AddRange(segments);
            }
        }

        if (_isEnabled || _isCalibrationPreviewEnabled || _isAutoPreviewActive)
        {
            RenderAndFlushFrame(forceVisualizerOnly: true);
        }
    }

    /// <summary>
    /// Updates secondary / additional WLED controller endpoints for simultaneous network broadcasting.
    /// </summary>
    public void UpdateAdditionalControllers(IEnumerable<(string ip, int port)> controllers)
    {
        _sender.UpdateAdditionalEndpoints(controllers);
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
            _heartbeatTimer?.Change(HeartbeatIntervalMs, HeartbeatIntervalMs);
            RenderAndFlushFrame();
        }

        return success;
    }

    /// <summary>
    /// Explicitly closes the WLED UDP endpoint, clears physical LED strip, and marks talker as idle.
    /// </summary>
    public void Disconnect()
    {
        _heartbeatTimer?.Change(Timeout.Infinite, Timeout.Infinite);
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

    /// <summary>
    /// Periodic heartbeat timer tick (~10 FPS) to maintain the active lighting state on WLED.
    /// WLED controllers have a firmware live-data timeout (typically 2–2.5 seconds); if packets cease
    /// during static color states without animation, WLED times out and turns black.
    /// This heartbeat ensures steady keep-alive frame transmissions so static colors persist seamlessly,
    /// matching OpenRGB persistent behavior.
    /// </summary>
    private void OnHeartbeatTimerTick(object? state)
    {
        if (!_isEnabled || !_sender.IsActive) return;

        long now = Environment.TickCount64;
        long elapsed = now - _sender.LastPacketSentTicks;

        // Retransmit current frame buffer if no frame was sent recently (>= HeartbeatIntervalMs)
        if (elapsed >= HeartbeatIntervalMs)
        {
            lock (Lock)
            {
                if (!_isEnabled || !_sender.IsActive) return;
                _sender.SendFrame(_outputBuffer.AsSpan(0, _ledCount), _protocol, _timeoutSeconds);
            }
        }

        // Periodically refresh UI telemetry (WledFps & WledPacketsSent) every ~500ms (5 ticks)
        if (++_heartbeatTelemetryCounter >= 5)
        {
            _heartbeatTelemetryCounter = 0;
            if (_mainViewModel != null)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(
                    _mainViewModel.RefreshWledTelemetry,
                    Avalonia.Threading.DispatcherPriority.Background);
            }
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
    public void RenderAndFlushFrame(bool forceVisualizerOnly = false)
    {
        if (!_isEnabled && !forceVisualizerOnly) return;

        lock (Lock)
        {
            if (_outputBuffer.Length != _ledCount)
            {
                Array.Resize(ref _outputBuffer, _ledCount);
            }

            WledRgbwColor[] virtualLeds = GetActiveSourceLeds();

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

            // 3. Map virtual StageKit LEDs to output buffer based on multi-strip routing & layout mode
            if (_activeSegments.Count > 1 && _multiStripMode != WledMultiStripMode.Contiguous)
            {
                RenderMultiStripSegments(virtualLeds, strobeColor, whiteAccent);
            }
            else
            {
                RenderLayoutToSlice(_outputBuffer.AsSpan(0, _ledCount), _layoutMode, virtualLeds, strobeColor, whiteAccent);
            }

            // 4. Apply physical strip geometry transforms (reverse direction & start offset)
            ApplyGeometryTransforms();

            // 5. Send frame to physical WLED controller (if enabled and connected)
            if (_isEnabled)
            {
                _sender.SendFrame(_outputBuffer.AsSpan(0, _ledCount), _protocol, _timeoutSeconds);
            }

            // 6. Broadcast telemetry to live visualizer
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

    private void RenderMultiStripSegments(WledRgbwColor[] virtualLeds, WledRgbwColor strobeColor, byte whiteAccent)
    {
        for (int segIdx = 0; segIdx < _activeSegments.Count; segIdx++)
        {
            var seg = _activeSegments[segIdx];
            int start = Math.Clamp(seg.Start, 0, _ledCount);
            int length = Math.Clamp(seg.Length, 0, _ledCount - start);
            if (length <= 0) continue;

            var slice = _outputBuffer.AsSpan(start, length);

            switch (_multiStripMode)
            {
                case WledMultiStripMode.MirrorPerSegment:
                    RenderLayoutToSlice(slice, _layoutMode, virtualLeds, strobeColor, whiteAccent);
                    if (seg.IsReversed)
                    {
                        slice.Reverse();
                    }
                    break;

                case WledMultiStripMode.AlignedTilePerSegment:
                    RenderTiledRepeat32(slice, virtualLeds, strobeColor, whiteAccent);
                    if (seg.IsReversed)
                    {
                        slice.Reverse();
                    }
                    break;

                case WledMultiStripMode.RoleBased:
                    if (segIdx == 0)
                    {
                        // Primary Strip: Main StageKit lights
                        RenderLayoutToSlice(slice, _layoutMode, virtualLeds, strobeColor, whiteAccent);
                    }
                    else if (segIdx == 1)
                    {
                        // Secondary Strip: Dedicated Strobe & Rhythm Blinder
                        if (strobeColor != WledRgbwColor.Black)
                        {
                            var c = strobeColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
                            slice.Fill(c);
                        }
                        else if (whiteAccent > 0)
                        {
                            var accentColor = new WledRgbwColor(0, 0, 0, whiteAccent).ApplyBrightness(_masterBrightness, _whiteBrightness);
                            slice.Fill(accentColor);
                        }
                        else
                        {
                            var dimAmbient = new WledRgbwColor(10, 10, 20, 0).ApplyBrightness(_masterBrightness, _whiteBrightness);
                            slice.Fill(dimAmbient);
                        }
                    }
                    else
                    {
                        // Tertiary Strip+: Star Power / Fog Haze / Ambient Wash
                        if (_isStarPowerActive)
                        {
                            var starColor = new WledRgbwColor(255, 215, 0, 200).ApplyBrightness(_masterBrightness, _whiteBrightness);
                            slice.Fill(starColor);
                        }
                        else if (_isFogBreathingActive)
                        {
                            var fogColor = new WledRgbwColor(0, 120, 255, _fogHazeBoost).ApplyBrightness(_masterBrightness, _whiteBrightness);
                            slice.Fill(fogColor);
                        }
                        else
                        {
                            RenderFullWash(slice, virtualLeds, strobeColor, whiteAccent);
                        }
                    }

                    if (seg.IsReversed)
                    {
                        slice.Reverse();
                    }
                    break;

                case WledMultiStripMode.Contiguous:
                default:
                    RenderLayoutToSlice(slice, _layoutMode, virtualLeds, strobeColor, whiteAccent);
                    break;
            }
        }
    }

    private void RenderLayoutToSlice(
        Span<WledRgbwColor> destination,
        WledLayoutMode layoutMode,
        WledRgbwColor[] virtualLeds,
        WledRgbwColor strobeColor,
        byte whiteAccent)
    {
        switch (layoutMode)
        {
            case WledLayoutMode.TiledRepeat32:
                RenderTiledRepeat32(destination, virtualLeds, strobeColor, whiteAccent);
                break;
            case WledLayoutMode.ProportionalStretch:
                RenderProportionalStretch(destination, virtualLeds, strobeColor, whiteAccent);
                break;
            case WledLayoutMode.FourQuadrantPods:
                RenderFourQuadrantPods(destination, virtualLeds, strobeColor, whiteAccent);
                break;
            case WledLayoutMode.CenterSplit:
                RenderCenterSplit(destination, virtualLeds, strobeColor, whiteAccent);
                break;
            case WledLayoutMode.Direct1to1:
                RenderDirect1to1(destination, virtualLeds, strobeColor, whiteAccent);
                break;
            case WledLayoutMode.FullWash:
                RenderFullWash(destination, virtualLeds, strobeColor, whiteAccent);
                break;
            case WledLayoutMode.SmoothGradient:
                RenderSmoothGradient(destination, virtualLeds, strobeColor, whiteAccent);
                break;
        }
    }

    private void RenderTiledRepeat32(Span<WledRgbwColor> destination, WledRgbwColor[] virtualLeds, WledRgbwColor strobeColor, byte whiteAccent)
    {
        int count = destination.Length;
        bool hasStrobe = strobeColor != WledRgbwColor.Black;
        int tileSize = Math.Clamp(_repeatTileSize > 0 ? _repeatTileSize : StageKitLedCount, 4, 128);
        int gap = Math.Max(0, _layoutGapLeds);
        int period = tileSize + gap;

        for (int i = 0; i < count; i++)
        {
            if (hasStrobe)
            {
                destination[i] = strobeColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
                continue;
            }

            int tileIndex = i / period;
            int posInPeriod = i % period;

            // In gap between repeating blocks
            if (posInPeriod >= tileSize)
            {
                destination[i] = WledRgbwColor.Black;
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
                    {
                        int podIndex = mappedOffset / 4;   // 0..7 (Pod 1 to 8)
                        int colorInPod = mappedOffset % 4; // 0=Blue, 1=Red, 2=Green, 3=Yellow
                        virtualIndex = (colorInPod * 8) + podIndex;
                    }
                    break;

                case WledRepeatStyle.InterleavedPingPong:
                    {
                        int podIndex = mappedOffset / 4;
                        if (tileIndex % 2 == 1) podIndex = 7 - podIndex;
                        int colorInPod = mappedOffset % 4;
                        virtualIndex = (colorInPod * 8) + podIndex;
                    }
                    break;

                case WledRepeatStyle.PairedInterleaved:
                    {
                        int cluster = mappedOffset / 8;     // 0..3
                        int posInCluster = mappedOffset % 8; // 0..7
                        int color = posInCluster / 2;       // 0..3 (B, R, G, Y)
                        int pairOffset = posInCluster % 2;  // 0..1
                        virtualIndex = (color * 8) + (cluster * 2) + pairOffset;
                    }
                    break;

                case WledRepeatStyle.CrossCoolWarm:
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

            var baseColor = virtualLeds[virtualIndex];
            baseColor = ApplyDynamicLayers(baseColor, whiteAccent);
            destination[i] = baseColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
        }
    }

    private void RenderProportionalStretch(Span<WledRgbwColor> destination, WledRgbwColor[] virtualLeds, WledRgbwColor strobeColor, byte whiteAccent)
    {
        int count = destination.Length;
        bool hasStrobe = strobeColor != WledRgbwColor.Black;
        int pad = Math.Clamp(_stretchPadding, 0, Math.Max(0, (count - 4) / 2));
        int activeLength = Math.Max(1, count - (pad * 2));

        for (int i = 0; i < count; i++)
        {
            if (hasStrobe)
            {
                destination[i] = strobeColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
                continue;
            }

            if (i < pad || i >= count - pad)
            {
                destination[i] = WledRgbwColor.Black;
                continue;
            }

            int activeIdx = i - pad;
            if (_stretchInterpolation == WledStretchInterpolation.SmoothBlend && activeLength > 1)
            {
                float ratio = (float)activeIdx * (StageKitLedCount - 1) / (activeLength - 1);
                int v0 = Math.Clamp((int)Math.Floor(ratio), 0, StageKitLedCount - 1);
                int v1 = Math.Clamp(v0 + 1, 0, StageKitLedCount - 1);
                float t = ratio - v0;

                var c0 = ApplyDynamicLayers(virtualLeds[v0], whiteAccent);
                var c1 = ApplyDynamicLayers(virtualLeds[v1], whiteAccent);
                var blended = WledRgbwColor.Lerp(c0, c1, t);
                destination[i] = blended.ApplyBrightness(_masterBrightness, _whiteBrightness);
            }
            else
            {
                int virtualIndex = (int)((long)activeIdx * StageKitLedCount / activeLength);
                virtualIndex = Math.Clamp(virtualIndex, 0, StageKitLedCount - 1);

                var baseColor = virtualLeds[virtualIndex];
                baseColor = ApplyDynamicLayers(baseColor, whiteAccent);
                destination[i] = baseColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
            }
        }
    }

    private void RenderFourQuadrantPods(Span<WledRgbwColor> destination, WledRgbwColor[] virtualLeds, WledRgbwColor strobeColor, byte whiteAccent)
    {
        int count = destination.Length;
        bool hasStrobe = strobeColor != WledRgbwColor.Black;
        int gap = Math.Max(0, _podGapLeds);
        int totalGaps = 3 * gap;
        int usableLeds = Math.Max(4, count - totalGaps);
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

        for (int i = 0; i < count; i++)
        {
            if (hasStrobe)
            {
                destination[i] = strobeColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
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
                destination[i] = WledRgbwColor.Black;
                continue;
            }

            int thisPodSize = podEnds[podIndex] - podStarts[podIndex];
            int offsetInPod = i - podStarts[podIndex];
            int virtualBase = GetPodVirtualBase(podIndex, _podColorOrder);
            int subIndex = thisPodSize > 0 ? (offsetInPod * 8 / thisPodSize) : 0;
            int virtualIndex = Math.Clamp(virtualBase + subIndex, 0, StageKitLedCount - 1);

            var baseColor = virtualLeds[virtualIndex];
            baseColor = ApplyDynamicLayers(baseColor, whiteAccent);
            destination[i] = baseColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
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

    private void RenderCenterSplit(Span<WledRgbwColor> destination, WledRgbwColor[] virtualLeds, WledRgbwColor strobeColor, byte whiteAccent)
    {
        int count = destination.Length;
        bool hasStrobe = strobeColor != WledRgbwColor.Black;
        int center = Math.Clamp((count / 2) + _centerSplitOffset, 1, Math.Max(1, count - 1));
        int leftSpan = center;
        int rightSpan = count - center;

        for (int i = 0; i < count; i++)
        {
            if (hasStrobe)
            {
                destination[i] = strobeColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
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

            var baseColor = virtualLeds[virtualIndex];
            baseColor = ApplyDynamicLayers(baseColor, whiteAccent);
            destination[i] = baseColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
        }
    }

    private void RenderDirect1to1(Span<WledRgbwColor> destination, WledRgbwColor[] virtualLeds, WledRgbwColor strobeColor, byte whiteAccent)
    {
        int count = destination.Length;
        bool hasStrobe = strobeColor != WledRgbwColor.Black;

        WledRgbwColor ambientWash = WledRgbwColor.Black;
        if (_directExcessMode == WledDirectExcessMode.AmbientWash && !hasStrobe)
        {
            int r = 0, g = 0, b = 0, actCount = 0;
            for (int k = 0; k < StageKitLedCount; k++)
            {
                if (virtualLeds[k].R > 0 || virtualLeds[k].G > 0 || virtualLeds[k].B > 0)
                {
                    r += virtualLeds[k].R;
                    g += virtualLeds[k].G;
                    b += virtualLeds[k].B;
                    actCount++;
                }
            }
            if (actCount > 0)
            {
                ambientWash = new WledRgbwColor((byte)(r / (actCount * 4)), (byte)(g / (actCount * 4)), (byte)(b / (actCount * 4)), (byte)(whiteAccent / 4));
            }
        }

        for (int i = 0; i < count; i++)
        {
            if (hasStrobe)
            {
                destination[i] = strobeColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
                continue;
            }

            WledRgbwColor baseColor;
            if (i < StageKitLedCount)
            {
                baseColor = virtualLeds[i];
            }
            else
            {
                baseColor = _directExcessMode switch
                {
                    WledDirectExcessMode.RepeatPattern => virtualLeds[i % StageKitLedCount],
                    WledDirectExcessMode.AmbientWash => ambientWash,
                    _ => WledRgbwColor.Black
                };
            }

            baseColor = ApplyDynamicLayers(baseColor, whiteAccent);
            destination[i] = baseColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
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

    private void RenderFullWash(Span<WledRgbwColor> destination, WledRgbwColor[] virtualLeds, WledRgbwColor strobeColor, byte whiteAccent)
    {
        int count = destination.Length;
        bool hasStrobe = strobeColor != WledRgbwColor.Black;

        if (hasStrobe)
        {
            var strobed = strobeColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
            for (int i = 0; i < count; i++) destination[i] = strobed;
            return;
        }

        WledRgbwColor washColor = WledRgbwColor.Black;

        if (_washBlendMode == WledWashBlendMode.DominantColor)
        {
            // Determine which pod has the highest count of active LEDs
            int bCount = 0, rCount = 0, gCount = 0, yCount = 0;
            for (int k = 0; k < 8; k++) if (virtualLeds[k].B > 0) bCount++;
            for (int k = 8; k < 16; k++) if (virtualLeds[k].R > 0) rCount++;
            for (int k = 16; k < 24; k++) if (virtualLeds[k].G > 0) gCount++;
            for (int k = 24; k < 32; k++) if (virtualLeds[k].R > 0 && virtualLeds[k].G > 0) yCount++;

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
                var c = virtualLeds[i];
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
        for (int i = 0; i < count; i++)
        {
            destination[i] = finalWash;
        }
    }

    /// <summary>
    /// Interpolates smooth color gradients from StageKit Pod 1 to 8 across the strip LEDs.
    /// Rotations flow like waves across the entire physical strip without jarring discrete steps.
    /// </summary>
    private void RenderSmoothGradient(Span<WledRgbwColor> destination, WledRgbwColor[] virtualLeds, WledRgbwColor strobeColor, byte whiteAccent)
    {
        int n = destination.Length;
        bool hasStrobe = strobeColor != WledRgbwColor.Black;

        if (hasStrobe)
        {
            var strobed = strobeColor.ApplyBrightness(_masterBrightness, _whiteBrightness);
            for (int i = 0; i < n; i++) destination[i] = strobed;
            return;
        }

        // Calculate the 8 StageKit Pod blended colors (Pod 1 to Pod 8) from active B, R, G, Y parameters
        Span<WledRgbwColor> podColors = stackalloc WledRgbwColor[8];
        int activePodCount = 0;

        if (HasActiveStageKitCues())
        {
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
        }
        else if (_isCalibrationPreviewEnabled || _isAutoPreviewActive)
        {
            // Rainbow wave preview across 8 pod samples
            podColors[0] = virtualLeds[0];  // Blue
            podColors[1] = new WledRgbwColor(160, 0, 200, 0); // Indigo / Violet
            podColors[2] = virtualLeds[8];  // Red
            podColors[3] = new WledRgbwColor(255, 120, 0, 0); // Orange
            podColors[4] = virtualLeds[24]; // Yellow
            podColors[5] = virtualLeds[16]; // Green
            podColors[6] = new WledRgbwColor(0, 200, 255, 0); // Cyan
            podColors[7] = virtualLeds[6];  // White reference diode
            activePodCount = 8;
        }

        if (activePodCount == 0 && whiteAccent == 0 && !_isStarPowerActive && !_isFogBreathingActive)
        {
            for (int i = 0; i < n; i++)
            {
                destination[i] = WledRgbwColor.Black;
            }
            return;
        }

        if (n == 1)
        {
            var single = ApplyDynamicLayers(podColors[0], whiteAccent);
            destination[0] = single.ApplyBrightness(_masterBrightness, _whiteBrightness);
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
            destination[i] = interpolated.ApplyBrightness(_masterBrightness, _whiteBrightness);
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
    /// Immediately blacks out all LEDs on the strip and cancels active calibration preview.
    /// </summary>
    public void Blackout()
    {
        _autoPreviewTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _isAutoPreviewActive = false;
        _isCalibrationPreviewEnabled = false;
        ClearAllLeds();
        if (_isEnabled)
        {
            RenderAndFlushFrame();
        }
        else
        {
            RenderAndFlushFrame(forceVisualizerOnly: true);
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
    /// Asynchronously synchronizes controller power state ("on": true) and global hardware brightness
    /// ("bri": 0..255) via WLED HTTP JSON API (/json/state) to ensure physical PSU safety scaling.
    /// </summary>
    public static async Task<bool> SetControllerStateAsync(string host, bool on, byte brightness, int timeoutMs = 1500)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;

        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            string trimmed = host.Trim();
            string url = $"http://{trimmed}/json/state";
            string json = $"{{\"on\":{(on ? "true" : "false")},\"bri\":{brightness}}}";
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(url, content, cts.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
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
            using var cts = new CancellationTokenSource(timeoutMs);
            string url = $"http://{host}/json";
            var response = await HttpClient.GetAsync(url, cts.Token).ConfigureAwait(false);
            bool queriedCombined = response.IsSuccessStatusCode;
            if (!queriedCombined)
            {
                url = $"http://{host}/json/info";
                response = await HttpClient.GetAsync(url, cts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return null;
            }

            string json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            JsonElement infoElement = (queriedCombined && root.TryGetProperty("info", out var ip)) ? ip : root;
            JsonElement stateElement = (queriedCombined && root.TryGetProperty("state", out var sp)) ? sp : default;

            string name = infoElement.TryGetProperty("name", out var n) ? n.GetString() ?? "WLED" : "WLED";
            string ver = infoElement.TryGetProperty("ver", out var v) ? v.GetString() ?? "" : "";
            int count = 0;
            bool isRgbw = false;
            int maxPwr = 0;
            int curPwr = 0;

            if (infoElement.TryGetProperty("leds", out var leds))
            {
                if (leds.TryGetProperty("count", out var c)) count = c.GetInt32();
                if (leds.TryGetProperty("rgbw", out var rw)) isRgbw = rw.GetBoolean();
                if (leds.TryGetProperty("maxpwr", out var mp)) maxPwr = mp.GetInt32();
                if (leds.TryGetProperty("pwr", out var cp)) curPwr = cp.GetInt32();
            }

            var segments = new List<WledSegment>();
            if (stateElement.ValueKind == JsonValueKind.Object && stateElement.TryGetProperty("seg", out var segArray) && segArray.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (var seg in segArray.EnumerateArray())
                {
                    int id = seg.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : index;
                    int start = seg.TryGetProperty("start", out var sProp) ? sProp.GetInt32() : 0;
                    int stop = seg.TryGetProperty("stop", out var stopProp) ? stopProp.GetInt32() : count;
                    string segName = seg.TryGetProperty("n", out var nameProp) ? nameProp.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(segName))
                    {
                        segName = $"Strip {id + 1}";
                    }
                    bool on = !seg.TryGetProperty("on", out var onProp) || onProp.GetBoolean();
                    byte bri = seg.TryGetProperty("bri", out var briProp) ? (byte)briProp.GetInt32() : (byte)255;
                    bool rev = seg.TryGetProperty("rev", out var revProp) && revProp.GetBoolean();

                    if (stop > start)
                    {
                        segments.Add(new WledSegment(id, segName, start, stop, on, bri, rev));
                    }
                    index++;
                }
            }

            if (segments.Count == 0 && count > 0)
            {
                segments.Add(new WledSegment(0, "Main Strip", 0, count));
            }

            return new WledDeviceInfo(name, count, isRgbw, ver, segments, maxPwr, curPwr);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;

        _decayTimer?.Dispose();
        _decayTimer = null;

        _autoPreviewTimer?.Dispose();
        _autoPreviewTimer = null;

        UnsubscribeFromEvents();
        _sender.Dispose();
    }
}
