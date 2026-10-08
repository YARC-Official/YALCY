using System;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using YALCY.Integrations.WLED;
using YALCY.Views.Components;

namespace YALCY.ViewModels;

/// <summary>
/// Partial class of MainWindowViewModel providing observable properties,
/// commands, and configuration bindings for the WLED integration.
/// </summary>
public partial class MainWindowViewModel
{
    private static readonly WledSetting WledDefaults = new();

    private string _wledServerIp = WledDefaults.ServerIp;
    private ushort _wledServerPort = WledDefaults.ServerPort;
    private int _wledProtocol = WledDefaults.Protocol;
    private int _wledLedCount = WledDefaults.LedCount;
    private int _wledBrightness = WledDefaults.Brightness;
    private int _wledWhiteChannelBrightness = WledDefaults.WhiteChannelBrightness;
    private int _wledLayoutMode = WledDefaults.LayoutMode;
    private int _wledWhiteStrobeMode = WledDefaults.WhiteStrobeMode;
    private int _wledRepeatStyle = WledDefaults.RepeatStyle;
    private int _wledRepeatTileSize = WledDefaults.RepeatTileSize;
    private int _wledLayoutGapLeds = WledDefaults.LayoutGapLeds;
    private int _wledStretchInterpolation = WledDefaults.StretchInterpolation;
    private int _wledStretchPadding = WledDefaults.StretchPadding;
    private int _wledPodColorOrder = WledDefaults.PodColorOrder;
    private int _wledPodGapLeds = WledDefaults.PodGapLeds;
    private int _wledCenterSplitDirection = WledDefaults.CenterSplitDirection;
    private int _wledCenterSplitOffset = WledDefaults.CenterSplitOffset;
    private int _wledDirectExcessMode = WledDefaults.DirectExcessMode;
    private int _wledWashBlendMode = WledDefaults.WashBlendMode;
    private int _wledGradientCycles = WledDefaults.GradientCycles;
    private bool _wledRhythmicWhiteAccent = WledDefaults.RhythmicWhiteAccent;
    private bool _wledSnareFlashEnabled = WledDefaults.SnareFlashEnabled;
    private bool _wledStarPowerPulseEnabled = WledDefaults.StarPowerPulseEnabled;
    private int _wledTimeoutSeconds = WledDefaults.TimeoutSeconds;

    private bool _wledReverseDirection = WledDefaults.ReverseDirection;
    private int _wledStartOffset = WledDefaults.StartOffset;
    private int _wledSnareFlashIntensity = WledDefaults.SnareFlashIntensity;
    private int _wledBeatPulseIntensity = WledDefaults.BeatPulseIntensity;
    private int _wledFogHazeBoost = WledDefaults.FogHazeBoost;
    private int _wledMaxBrightnessLimit = WledDefaults.MaxBrightnessLimit;

    private string _wledStatus = "WLED status: Idle.";
    private string _wledMessage = string.Empty;

    public ICommand ConnectWledCommand { get; private set; } = null!;
    public ICommand DisconnectWledCommand { get; private set; } = null!;
    public ICommand TestWledWhiteBlinderCommand { get; private set; } = null!;
    public ICommand TestWledStagePodsCommand { get; private set; } = null!;
    public ICommand TestWledColorChaseCommand { get; private set; } = null!;
    public ICommand TestWledRedCommand { get; private set; } = null!;
    public ICommand TestWledGreenCommand { get; private set; } = null!;
    public ICommand TestWledBlueCommand { get; private set; } = null!;
    public ICommand TestWledYellowCommand { get; private set; } = null!;
    public ICommand TestWledPureWhiteCommand { get; private set; } = null!;
    public ICommand WledBlackoutCommand { get; private set; } = null!;
    public ICommand ResetWledDefaultsCommand { get; private set; } = null!;

    private bool _isWledConnected;
    private bool _isWledConnecting;

    public bool IsWledConnected
    {
        get => _isWledConnected;
        set
        {
            this.RaiseAndSetIfChanged(ref _isWledConnected, value);
            this.RaisePropertyChanged(nameof(WledConnectButtonText));
            this.RaisePropertyChanged(nameof(CanConnectWled));
            this.RaisePropertyChanged(nameof(CanDisconnectWled));
            this.RaisePropertyChanged(nameof(WledConnectionBadgeText));
            this.RaisePropertyChanged(nameof(WledConnectionBadgeColor));
        }
    }

    public bool IsWledConnecting
    {
        get => _isWledConnecting;
        set
        {
            this.RaiseAndSetIfChanged(ref _isWledConnecting, value);
            this.RaisePropertyChanged(nameof(WledConnectButtonText));
            this.RaisePropertyChanged(nameof(CanConnectWled));
            this.RaisePropertyChanged(nameof(CanDisconnectWled));
            this.RaisePropertyChanged(nameof(WledConnectionBadgeText));
            this.RaisePropertyChanged(nameof(WledConnectionBadgeColor));
        }
    }

    public string WledConnectButtonText => _isWledConnecting
        ? "⏳ Connecting..."
        : "🔗 Connect";

    public bool CanConnectWled => !_isWledConnecting && !_isWledConnected;
    public bool CanDisconnectWled => !_isWledConnecting && _isWledConnected;

    public string WledConnectionBadgeText => _isWledConnecting
        ? "CONNECTING..."
        : (_isWledConnected ? "CONNECTED" : "DISCONNECTED");

    public string WledConnectionBadgeColor => _isWledConnecting
        ? "#FFA726"
        : (_isWledConnected ? "#4CAF50" : "#757575");

    public string? WledServerIp
    {
        get => _wledServerIp;
        set
        {
            // Pure in-memory update on keystroke: never block UI thread with network or DNS calls
            this.RaiseAndSetIfChanged(ref _wledServerIp, value ?? string.Empty);
            if (_isWledConnected)
            {
                IsWledConnected = false;
                if (WledEnabledSetting != null && WledEnabledSetting.IsEnabled)
                {
                    WledEnabledSetting.IsEnabled = false;
                }
            }
        }
    }

    public ushort WledServerPort
    {
        get => _wledServerPort;
        set
        {
            this.RaiseAndSetIfChanged(ref _wledServerPort, value);
        }
    }

    public bool IsRgbwProtocol => _wledProtocol == (int)Integrations.WLED.WledProtocol.DdpRgbw ||
                                  _wledProtocol == (int)Integrations.WLED.WledProtocol.RealtimeDrgbw;

    public string WledProtocolTitle => (Integrations.WLED.WledProtocol)_wledProtocol switch
    {
        Integrations.WLED.WledProtocol.DdpRgb => "📦 DDP RGB (WS2812B / WS2811) — Recommended for RGB",
        Integrations.WLED.WledProtocol.DdpRgbw => "✨ DDP RGBW (SK6812 / WS2814) — Recommended for RGBW",
        Integrations.WLED.WledProtocol.RealtimeDrgb => "⚡ Realtime DRGB (WLED UDP v2 • Legacy)",
        Integrations.WLED.WledProtocol.RealtimeDrgbw => "⚡ Realtime DRGBW (WLED UDP v3 • Legacy)",
        _ => "Streaming Protocol"
    };

    public string WledProtocolDescription => (Integrations.WLED.WledProtocol)_wledProtocol switch
    {
        Integrations.WLED.WledProtocol.DdpRgb => "Standard Distributed Display Protocol on UDP port 4048. Sends 3 channels per pixel (R, G, B). Recommended for standard RGB strips (WS2812B, WS2811). Features packet sequencing and low overhead with zero tearing.",
        Integrations.WLED.WledProtocol.DdpRgbw => "4-channel Distributed Display Protocol on UDP port 4048. Sends 4 channels per pixel (R, G, B, W). Designed for SK6812 RGBW and WS2814 strips with dedicated physical White diodes, enabling independent high-intensity strobes and snare flashes.",
        Integrations.WLED.WledProtocol.RealtimeDrgb => "Classic direct WLED Realtime UDP protocol on port 21324. Transmits 3 channels per pixel (R, G, B) with a compact 2-byte header. Lightweight legacy option for standard RGB setups.",
        Integrations.WLED.WledProtocol.RealtimeDrgbw => "Classic direct WLED Realtime UDP protocol on port 21324. Transmits 4 channels per pixel (R, G, B, W) with a compact 2-byte header. Lightweight legacy option for 4-channel SK6812 RGBW setups.",
        _ => string.Empty
    };

    public int WledProtocol
    {
        get => _wledProtocol;
        set
        {
            this.RaiseAndSetIfChanged(ref _wledProtocol, value);
            this.RaisePropertyChanged(nameof(IsRgbwProtocol));
            this.RaisePropertyChanged(nameof(IsRhythmicWhiteAllowed));
            this.RaisePropertyChanged(nameof(IsRhythmicWhiteActive));
            this.RaisePropertyChanged(nameof(WledWhiteModeDescription));
            this.RaisePropertyChanged(nameof(WledProtocolTitle));
            this.RaisePropertyChanged(nameof(WledProtocolDescription));

            // Automatically adapt default port when switching protocols if using standard ports
            if (value == (int)Integrations.WLED.WledProtocol.RealtimeDrgbw || value == (int)Integrations.WLED.WledProtocol.RealtimeDrgb)
            {
                if (_wledServerPort == WledSender.DefaultDdpPort)
                {
                    WledServerPort = WledSender.DefaultRealtimePort;
                }
            }
            else
            {
                if (_wledServerPort == WledSender.DefaultRealtimePort)
                {
                    WledServerPort = WledSender.DefaultDdpPort;
                }
            }

            // If streaming mode is RGBW, White Strobe mode defaults to Strobe Only; otherwise it becomes Disabled (Off)
            bool isRgbw = value == (int)Integrations.WLED.WledProtocol.DdpRgbw || value == (int)Integrations.WLED.WledProtocol.RealtimeDrgbw;
            if (isRgbw)
            {
                if (_wledWhiteStrobeMode == (int)Integrations.WLED.WledWhiteStrobeMode.Disabled)
                {
                    WledWhiteStrobeMode = (int)Integrations.WLED.WledWhiteStrobeMode.StrobeOnly;
                }
            }
            else
            {
                WledWhiteStrobeMode = (int)Integrations.WLED.WledWhiteStrobeMode.Disabled;
            }

            SyncWledConfiguration();
        }
    }

    public int WledLedCount
    {
        get => _wledLedCount;
        set
        {
            int clamped = Math.Clamp(value, 1, 1200);
            this.RaiseAndSetIfChanged(ref _wledLedCount, clamped);
            SyncWledConfiguration();
        }
    }

    public int WledBrightness
    {
        get => _wledBrightness;
        set
        {
            int clamped = Math.Clamp(value, 0, 255);
            this.RaiseAndSetIfChanged(ref _wledBrightness, clamped);
            SyncWledConfiguration();
        }
    }

    public int WledWhiteChannelBrightness
    {
        get => _wledWhiteChannelBrightness;
        set
        {
            int clamped = Math.Clamp(value, 0, 255);
            this.RaiseAndSetIfChanged(ref _wledWhiteChannelBrightness, clamped);
            SyncWledConfiguration();
        }
    }

    public int WledLayoutMode
    {
        get => _wledLayoutMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _wledLayoutMode, value);
            this.RaisePropertyChanged(nameof(WledLayoutTitle));
            this.RaisePropertyChanged(nameof(WledLayoutDescription));
            this.RaisePropertyChanged(nameof(IsTiledRepeatActive));
            this.RaisePropertyChanged(nameof(IsProportionalStretchActive));
            this.RaisePropertyChanged(nameof(IsFourQuadrantPodsActive));
            this.RaisePropertyChanged(nameof(IsCenterSplitActive));
            this.RaisePropertyChanged(nameof(IsDirect1to1Active));
            this.RaisePropertyChanged(nameof(IsFullWashActive));
            this.RaisePropertyChanged(nameof(IsSmoothGradientActive));
            SyncWledConfiguration();
        }
    }

    public string WledLayoutTitle => (Integrations.WLED.WledLayoutMode)_wledLayoutMode switch
    {
        Integrations.WLED.WledLayoutMode.TiledRepeat32 => "🔁 Tiled Continuous Repeat (1..32)",
        Integrations.WLED.WledLayoutMode.ProportionalStretch => "🎯 Proportional Stretch",
        Integrations.WLED.WledLayoutMode.FourQuadrantPods => "🚥 4 Distinct Stage Pods",
        Integrations.WLED.WledLayoutMode.CenterSplit => "🪞 Symmetrical Center Split",
        Integrations.WLED.WledLayoutMode.Direct1to1 => "📍 Direct 1-to-1 Mapping",
        Integrations.WLED.WledLayoutMode.FullWash => "🌈 Full Stage Color Wash",
        Integrations.WLED.WledLayoutMode.SmoothGradient => "〰 Smooth Gradient",
        _ => "Stage Layout"
    };

    public string WledLayoutDescription => (Integrations.WLED.WledLayoutMode)_wledLayoutMode switch
    {
        Integrations.WLED.WledLayoutMode.TiledRepeat32 => "Continuously tiles the 32 virtual StageKit LEDs (1..32) in a loop or ping-pong pattern across the entire strip.",
        Integrations.WLED.WledLayoutMode.ProportionalStretch => "Proportionally stretches the 32 StageKit LEDs to cover the entire strip length with configurable interpolation.",
        Integrations.WLED.WledLayoutMode.FourQuadrantPods => "Divides the strip into 4 physical pods (Blue, Red, Green, Yellow) with customizable order and gap intervals.",
        Integrations.WLED.WledLayoutMode.CenterSplit => "Mirrors lighting from the center outward to edges (or vice-versa), ideal for trusses and stage risers.",
        Integrations.WLED.WledLayoutMode.Direct1to1 => "Maps LEDs 0..31 directly 1-to-1 to the first 32 physical LEDs of the strip.",
        Integrations.WLED.WledLayoutMode.FullWash => "Projects a uniform stage color wash across the entire strip (harmonic average or dominant color).",
        Integrations.WLED.WledLayoutMode.SmoothGradient => "Interpolates continuous smooth transitions across pods 1 to 8 creating a fluid color wave.",
        _ => string.Empty
    };

    public int WledRepeatStyle
    {
        get => _wledRepeatStyle;
        set
        {
            this.RaiseAndSetIfChanged(ref _wledRepeatStyle, value);
            SyncWledConfiguration();
        }
    }

    public int WledRepeatTileSize
    {
        get => _wledRepeatTileSize;
        set
        {
            this.RaiseAndSetIfChanged(ref _wledRepeatTileSize, value);
            SyncWledConfiguration();
        }
    }

    public int WledLayoutGapLeds
    {
        get => _wledLayoutGapLeds;
        set
        {
            this.RaiseAndSetIfChanged(ref _wledLayoutGapLeds, value);
            SyncWledConfiguration();
        }
    }

    public int WledStretchInterpolation
    {
        get => _wledStretchInterpolation;
        set
        {
            this.RaiseAndSetIfChanged(ref _wledStretchInterpolation, value);
            SyncWledConfiguration();
        }
    }

    public int WledStretchPadding
    {
        get => _wledStretchPadding;
        set
        {
            this.RaiseAndSetIfChanged(ref _wledStretchPadding, value);
            SyncWledConfiguration();
        }
    }

    public int WledPodColorOrder
    {
        get => _wledPodColorOrder;
        set
        {
            this.RaiseAndSetIfChanged(ref _wledPodColorOrder, value);
            SyncWledConfiguration();
        }
    }

    public int WledPodGapLeds
    {
        get => _wledPodGapLeds;
        set
        {
            this.RaiseAndSetIfChanged(ref _wledPodGapLeds, value);
            SyncWledConfiguration();
        }
    }

    public int WledCenterSplitDirection
    {
        get => _wledCenterSplitDirection;
        set
        {
            this.RaiseAndSetIfChanged(ref _wledCenterSplitDirection, value);
            SyncWledConfiguration();
        }
    }

    public int WledCenterSplitOffset
    {
        get => _wledCenterSplitOffset;
        set
        {
            this.RaiseAndSetIfChanged(ref _wledCenterSplitOffset, value);
            SyncWledConfiguration();
        }
    }

    public int WledDirectExcessMode
    {
        get => _wledDirectExcessMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _wledDirectExcessMode, value);
            SyncWledConfiguration();
        }
    }

    public int WledWashBlendMode
    {
        get => _wledWashBlendMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _wledWashBlendMode, value);
            SyncWledConfiguration();
        }
    }

    public int WledGradientCycles
    {
        get => _wledGradientCycles;
        set
        {
            this.RaiseAndSetIfChanged(ref _wledGradientCycles, value);
            SyncWledConfiguration();
        }
    }

    public bool IsTiledRepeatActive => _wledLayoutMode == (int)Integrations.WLED.WledLayoutMode.TiledRepeat32;
    public bool IsProportionalStretchActive => _wledLayoutMode == (int)Integrations.WLED.WledLayoutMode.ProportionalStretch;
    public bool IsFourQuadrantPodsActive => _wledLayoutMode == (int)Integrations.WLED.WledLayoutMode.FourQuadrantPods;
    public bool IsCenterSplitActive => _wledLayoutMode == (int)Integrations.WLED.WledLayoutMode.CenterSplit;
    public bool IsDirect1to1Active => _wledLayoutMode == (int)Integrations.WLED.WledLayoutMode.Direct1to1;
    public bool IsFullWashActive => _wledLayoutMode == (int)Integrations.WLED.WledLayoutMode.FullWash;
    public bool IsSmoothGradientActive => _wledLayoutMode == (int)Integrations.WLED.WledLayoutMode.SmoothGradient;

    public int WledWhiteStrobeMode
    {
        get => _wledWhiteStrobeMode;
        set
        {
            if (!IsRgbwProtocol && value != (int)Integrations.WLED.WledWhiteStrobeMode.Disabled)
            {
                value = (int)Integrations.WLED.WledWhiteStrobeMode.Disabled;
            }
            this.RaiseAndSetIfChanged(ref _wledWhiteStrobeMode, value);
            this.RaisePropertyChanged(nameof(WledWhiteModeDescription));
            this.RaisePropertyChanged(nameof(IsRhythmicWhiteAllowed));
            this.RaisePropertyChanged(nameof(IsRhythmicWhiteActive));
            if (!IsRhythmicWhiteAllowed && _wledRhythmicWhiteAccent)
            {
                WledRhythmicWhiteAccent = false;
            }
            SyncWledConfiguration();
        }
    }

    public bool IsRhythmicWhiteAllowed => IsRgbwProtocol && _wledWhiteStrobeMode == (int)Integrations.WLED.WledWhiteStrobeMode.FullRhythmAndStrobe;
    public bool IsRhythmicWhiteActive => IsRhythmicWhiteAllowed && _wledRhythmicWhiteAccent;

    public string WledWhiteModeDescription => (Integrations.WLED.WledWhiteStrobeMode)_wledWhiteStrobeMode switch
    {
        Integrations.WLED.WledWhiteStrobeMode.FullRhythmAndStrobe => "⚡ Rhythm + Strobe: Dedicated White (W) diodes fire on snare hits, beat pulses, and strobe blinders.",
        Integrations.WLED.WledWhiteStrobeMode.StrobeOnly => "🚨 Strobe Only: White (W) channel is reserved exclusively for strobe blinders. Rhythm and drums use RGB only.",
        Integrations.WLED.WledWhiteStrobeMode.Disabled => IsRgbwProtocol
            ? "⬛ Disabled (Off): White (W) channel is powered down (0). All effects and strobes operate strictly in RGB."
            : "⬛ Disabled (Off): Standard RGB protocol (WS2812B) has no dedicated White diode. All effects and strobes operate strictly in RGB.",
        _ => string.Empty
    };

    public bool WledRhythmicWhiteAccent
    {
        get => _wledRhythmicWhiteAccent;
        set
        {
            if (!IsRhythmicWhiteAllowed && value)
            {
                value = false;
            }
            this.RaiseAndSetIfChanged(ref _wledRhythmicWhiteAccent, value);
            this.RaisePropertyChanged(nameof(IsRhythmicWhiteActive));
            SyncWledConfiguration();
        }
    }

    public bool WledSnareFlashEnabled
    {
        get => _wledSnareFlashEnabled;
        set
        {
            this.RaiseAndSetIfChanged(ref _wledSnareFlashEnabled, value);
            SyncWledConfiguration();
        }
    }

    public bool WledStarPowerPulseEnabled
    {
        get => _wledStarPowerPulseEnabled;
        set
        {
            this.RaiseAndSetIfChanged(ref _wledStarPowerPulseEnabled, value);
            SyncWledConfiguration();
        }
    }

    public int WledTimeoutSeconds
    {
        get => _wledTimeoutSeconds;
        set
        {
            int clamped = Math.Clamp(value, 1, 30);
            this.RaiseAndSetIfChanged(ref _wledTimeoutSeconds, clamped);
            SyncWledConfiguration();
        }
    }

    public bool WledReverseDirection
    {
        get => _wledReverseDirection;
        set
        {
            this.RaiseAndSetIfChanged(ref _wledReverseDirection, value);
            SyncWledConfiguration();
        }
    }

    public int WledStartOffset
    {
        get => _wledStartOffset;
        set
        {
            int clamped = Math.Clamp(value, 0, 1199);
            this.RaiseAndSetIfChanged(ref _wledStartOffset, clamped);
            SyncWledConfiguration();
        }
    }

    public int WledSnareFlashIntensity
    {
        get => _wledSnareFlashIntensity;
        set
        {
            int clamped = Math.Clamp(value, 0, 255);
            this.RaiseAndSetIfChanged(ref _wledSnareFlashIntensity, clamped);
            SyncWledConfiguration();
        }
    }

    public int WledBeatPulseIntensity
    {
        get => _wledBeatPulseIntensity;
        set
        {
            int clamped = Math.Clamp(value, 0, 255);
            this.RaiseAndSetIfChanged(ref _wledBeatPulseIntensity, clamped);
            SyncWledConfiguration();
        }
    }

    public int WledFogHazeBoost
    {
        get => _wledFogHazeBoost;
        set
        {
            int clamped = Math.Clamp(value, 0, 255);
            this.RaiseAndSetIfChanged(ref _wledFogHazeBoost, clamped);
            SyncWledConfiguration();
        }
    }

    public int WledMaxBrightnessLimit
    {
        get => _wledMaxBrightnessLimit;
        set
        {
            int clamped = Math.Clamp(value, 10, 255);
            this.RaiseAndSetIfChanged(ref _wledMaxBrightnessLimit, clamped);
            SyncWledConfiguration();
        }
    }

    public string WledStatus
    {
        get => _wledStatus;
        set => this.RaiseAndSetIfChanged(ref _wledStatus, value);
    }

    public string WledMessage
    {
        get => _wledMessage;
        set => this.RaiseAndSetIfChanged(ref _wledMessage, value);
    }

    public long WledPacketsSent => WledTalker.Sender.TotalPacketsSent;
    public double WledFps => WledTalker.Sender.PacketsPerSecond;

    private void ApplyWledSettings(WledSetting s)
    {
        WledServerIp = s.ServerIp;
        WledServerPort = s.ServerPort;
        WledProtocol = s.Protocol;
        WledLedCount = s.LedCount;
        WledBrightness = s.Brightness;
        WledWhiteChannelBrightness = s.WhiteChannelBrightness;
        WledLayoutMode = s.LayoutMode;

        bool isRgbw = s.Protocol == (int)Integrations.WLED.WledProtocol.DdpRgbw || s.Protocol == (int)Integrations.WLED.WledProtocol.RealtimeDrgbw;
        WledWhiteStrobeMode = isRgbw
            ? (s.WhiteStrobeMode == (int)Integrations.WLED.WledWhiteStrobeMode.Disabled ? (int)Integrations.WLED.WledWhiteStrobeMode.StrobeOnly : s.WhiteStrobeMode)
            : (int)Integrations.WLED.WledWhiteStrobeMode.Disabled;

        WledStrobeMode = s.StrobeMode;
        WledRhythmicWhiteAccent = IsRhythmicWhiteAllowed && s.RhythmicWhiteAccent;
        WledSnareFlashEnabled = s.SnareFlashEnabled;
        WledStarPowerPulseEnabled = s.StarPowerPulseEnabled;
        WledTimeoutSeconds = s.TimeoutSeconds;
        WledReverseDirection = s.ReverseDirection;
        WledStartOffset = s.StartOffset;
        WledSnareFlashIntensity = s.SnareFlashIntensity;
        WledBeatPulseIntensity = s.BeatPulseIntensity;
        WledFogHazeBoost = s.FogHazeBoost;
        WledMaxBrightnessLimit = s.MaxBrightnessLimit;
        WledRepeatStyle = s.RepeatStyle;
        WledRepeatTileSize = s.RepeatTileSize;
        WledLayoutGapLeds = s.LayoutGapLeds;
        WledStretchInterpolation = s.StretchInterpolation;
        WledStretchPadding = s.StretchPadding;
        WledPodColorOrder = s.PodColorOrder;
        WledPodGapLeds = s.PodGapLeds;
        WledCenterSplitDirection = s.CenterSplitDirection;
        WledCenterSplitOffset = s.CenterSplitOffset;
        WledDirectExcessMode = s.DirectExcessMode;
        WledWashBlendMode = s.WashBlendMode;
        WledGradientCycles = s.GradientCycles;
    }

    private void FeedInWledSettings()
    {
        ApplyWledSettings(SettingsManager.WledSettings);

        WledTalker?.SetViewModel(this);

        WledStatus = "WLED status: Ready.";
        WledMessage = string.Empty;

        InitializeWledCommands();
        SyncWledConfiguration();
    }

    private void InitializeWledCommands()
    {
        var canConnect = this.WhenAnyValue(
            x => x.IsWledConnecting,
            x => x.IsWledConnected,
            (connecting, connected) => !connecting && !connected);

        var canDisconnect = this.WhenAnyValue(
            x => x.IsWledConnecting,
            x => x.IsWledConnected,
            (connecting, connected) => !connecting && connected);

        ConnectWledCommand = ReactiveCommand.CreateFromTask(ConnectWledAsync, canConnect);
        DisconnectWledCommand = ReactiveCommand.Create(DisconnectWled, canDisconnect);
        TestWledWhiteBlinderCommand = ReactiveCommand.CreateFromTask(() => WledTalker.TestWhiteBlinderAsync());
        TestWledStagePodsCommand = ReactiveCommand.CreateFromTask(() => WledTalker.TestStageKitPodsAsync());
        TestWledColorChaseCommand = ReactiveCommand.CreateFromTask(() => WledTalker.TestColorChaseAsync());
        TestWledRedCommand = ReactiveCommand.CreateFromTask(() => TestWledColorAsync(WledRgbwColor.StageKitRed));
        TestWledGreenCommand = ReactiveCommand.CreateFromTask(() => TestWledColorAsync(WledRgbwColor.StageKitGreen));
        TestWledBlueCommand = ReactiveCommand.CreateFromTask(() => TestWledColorAsync(WledRgbwColor.StageKitBlue));
        TestWledYellowCommand = ReactiveCommand.CreateFromTask(() => TestWledColorAsync(WledRgbwColor.StageKitYellow));
        TestWledPureWhiteCommand = ReactiveCommand.CreateFromTask(() => TestWledColorAsync(WledRgbwColor.MaxWhite));
        WledBlackoutCommand = ReactiveCommand.Create(() => WledTalker.Blackout());
        ResetWledDefaultsCommand = ReactiveCommand.Create(ResetWledDefaults);
    }

    public async Task ConnectWledAsync()
    {
        if (string.IsNullOrWhiteSpace(_wledServerIp))
        {
            IsWledConnecting = false;
            IsWledConnected = false;
            if (WledEnabledSetting != null && WledEnabledSetting.IsEnabled)
            {
                WledEnabledSetting.IsEnabled = false;
            }
            WledStatus = "WLED status: Please enter an IP address or hostname.";
            WledMessage = "Target IP address or hostname cannot be empty.";
            StatusFooter.UpdateStatus("WLED", IntegrationStatus.Error);
            return;
        }

        IsWledConnecting = true;
        WledStatus = $"WLED status: Connecting to {_wledServerIp}:{_wledServerPort}...";
        WledMessage = $"Probing controller {_wledServerIp}...";
        StatusFooter.UpdateStatus("WLED", IntegrationStatus.Connecting);

        try
        {
            // Give a brief grace period so UI transitions and loading spinners are smoothly visible
            await Task.Delay(200).ConfigureAwait(false);

            // Actively verify controller is reachable on the local network (HTTP JSON / Ping)
            var (isReachable, detectedInfo, resolvedEndpoint, errorMsg) =
                await WledTalker.ProbeControllerAsync(_wledServerIp, 1800).ConfigureAwait(false);

            if (!isReachable)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    IsWledConnected = false;
                    if (WledEnabledSetting != null && WledEnabledSetting.IsEnabled)
                    {
                        WledEnabledSetting.IsEnabled = false;
                    }
                    WledStatus = $"WLED status: Unreachable ({_wledServerIp}).";
                    WledMessage = errorMsg;
                    StatusFooter.UpdateStatus("WLED", IntegrationStatus.Error);
                });
                return;
            }

            if (detectedInfo != null)
            {
                // Align protocol with physical strip configuration (RGB vs RGBW)
                bool isCurrentlyRgbw = _wledProtocol == (int)Integrations.WLED.WledProtocol.DdpRgbw || _wledProtocol == (int)Integrations.WLED.WledProtocol.RealtimeDrgbw;
                bool isCurrentlyRgb = _wledProtocol == (int)Integrations.WLED.WledProtocol.DdpRgb || _wledProtocol == (int)Integrations.WLED.WledProtocol.RealtimeDrgb;

                if (!detectedInfo.IsRgbw && isCurrentlyRgbw)
                {
                    int newProto = _wledProtocol == (int)Integrations.WLED.WledProtocol.RealtimeDrgbw
                        ? (int)Integrations.WLED.WledProtocol.RealtimeDrgb
                        : (int)Integrations.WLED.WledProtocol.DdpRgb;
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => WledProtocol = newProto);
                }
                else if (detectedInfo.IsRgbw && isCurrentlyRgb)
                {
                    int newProto = _wledProtocol == (int)Integrations.WLED.WledProtocol.RealtimeDrgb
                        ? (int)Integrations.WLED.WledProtocol.RealtimeDrgbw
                        : (int)Integrations.WLED.WledProtocol.DdpRgbw;
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => WledProtocol = newProto);
                }

                if (detectedInfo.LedCount > 0 && _wledLedCount != detectedInfo.LedCount)
                {
                    _wledLedCount = detectedInfo.LedCount;
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => this.RaisePropertyChanged(nameof(WledLedCount)));
                }
            }

            bool success = await WledTalker.ConnectAsync(
                _wledServerIp,
                _wledServerPort,
                (WledProtocol)_wledProtocol,
                _wledLedCount,
                (byte)_wledBrightness,
                (byte)_wledWhiteChannelBrightness,
                (WledLayoutMode)_wledLayoutMode,
                (WledWhiteStrobeMode)_wledWhiteStrobeMode,
                WledStrobeMode,
                _wledRhythmicWhiteAccent,
                _wledSnareFlashEnabled,
                _wledStarPowerPulseEnabled,
                (byte)_wledTimeoutSeconds,
                _wledReverseDirection,
                _wledStartOffset,
                (byte)_wledSnareFlashIntensity,
                (byte)_wledBeatPulseIntensity,
                (byte)_wledFogHazeBoost,
                (byte)_wledMaxBrightnessLimit
            ).ConfigureAwait(false);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (success)
                {
                    IsWledConnected = true;
                    if (WledEnabledSetting != null && !WledEnabledSetting.IsEnabled)
                    {
                        WledEnabledSetting.IsEnabled = true;
                    }

                    string hwDesc = detectedInfo != null
                        ? $"{detectedInfo.Name} ({_wledLedCount} LEDs, {(detectedInfo.IsRgbw ? "SK6812 RGBW" : "WS281x RGB")})"
                        : $"{_wledServerIp}:{_wledServerPort} ({_wledLedCount} LEDs)";
                    WledStatus = $"WLED status: Connected to {hwDesc}.";
                    WledMessage = $"Streaming via {((WledProtocol)_wledProtocol)} (< 2 ms direct UDP).";
                    StatusFooter.UpdateStatus("WLED", IntegrationStatus.Connected);
                }
                else
                {
                    IsWledConnected = false;
                    if (WledEnabledSetting != null && WledEnabledSetting.IsEnabled)
                    {
                        WledEnabledSetting.IsEnabled = false;
                    }
                    string error = string.IsNullOrEmpty(WledTalker.Sender.LastError)
                        ? "Failed to bind UDP endpoint."
                        : WledTalker.Sender.LastError;
                    WledStatus = $"WLED status: Error connecting to {_wledServerIp}.";
                    WledMessage = error;
                    StatusFooter.UpdateStatus("WLED", IntegrationStatus.Error);
                }
            });
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsWledConnected = false;
                if (WledEnabledSetting != null && WledEnabledSetting.IsEnabled)
                {
                    WledEnabledSetting.IsEnabled = false;
                }
                WledStatus = "WLED status: Connection exception.";
                WledMessage = ex.Message;
                StatusFooter.UpdateStatus("WLED", IntegrationStatus.Error);
            });
        }
        finally
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsWledConnecting = false;
            });
        }
    }

    public void DisconnectWled()
    {
        WledTalker.Disconnect();
        IsWledConnected = false;
        IsWledConnecting = false;
        if (WledEnabledSetting != null && WledEnabledSetting.IsEnabled)
        {
            WledEnabledSetting.IsEnabled = false;
        }
        WledStatus = "WLED status: Disconnected.";
        WledMessage = "WLED output paused and socket closed.";
        StatusFooter.UpdateStatus("WLED", IntegrationStatus.Off);
    }

    public async Task TestWledColorAsync(WledRgbwColor color)
    {
        await WledTalker.TestColorAsync(color);
    }

    private void ResetWledDefaults()
    {
        IsWledConnected = false;
        IsWledConnecting = false;
        ApplyWledSettings(WledDefaults);
        SyncWledConfiguration();
    }

    private void SyncWledConfiguration()
    {
        WledTalker?.UpdateConfiguration(
            _wledServerIp,
            _wledServerPort,
            (WledProtocol)_wledProtocol,
            _wledLedCount,
            (byte)_wledBrightness,
            (byte)_wledWhiteChannelBrightness,
            (WledLayoutMode)_wledLayoutMode,
            (WledWhiteStrobeMode)_wledWhiteStrobeMode,
            WledStrobeMode,
            _wledRhythmicWhiteAccent,
            _wledSnareFlashEnabled,
            _wledStarPowerPulseEnabled,
            (byte)_wledTimeoutSeconds,
            _wledReverseDirection,
            _wledStartOffset,
            (byte)_wledSnareFlashIntensity,
            (byte)_wledBeatPulseIntensity,
            (byte)_wledFogHazeBoost,
            (byte)_wledMaxBrightnessLimit
        );

        WledTalker?.UpdateLayoutOptions(
            (Integrations.WLED.WledRepeatStyle)_wledRepeatStyle,
            _wledRepeatTileSize,
            _wledLayoutGapLeds,
            (Integrations.WLED.WledStretchInterpolation)_wledStretchInterpolation,
            _wledStretchPadding,
            (Integrations.WLED.WledPodColorOrder)_wledPodColorOrder,
            _wledPodGapLeds,
            (Integrations.WLED.WledCenterSplitDirection)_wledCenterSplitDirection,
            _wledCenterSplitOffset,
            (Integrations.WLED.WledDirectExcessMode)_wledDirectExcessMode,
            (Integrations.WLED.WledWashBlendMode)_wledWashBlendMode,
            _wledGradientCycles
        );
    }
}
