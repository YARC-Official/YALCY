using System;
using System.Timers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using YALCY.Integrations.StageKit;
using YALCY.Udp;
using YALCY.Usb;

namespace YALCY.Views.Windows;

public partial class StageKitStatusWindow : Window
{
    private readonly bool[] _redLeds = new bool[8];
    private readonly bool[] _greenLeds = new bool[8];
    private readonly bool[] _blueLeds = new bool[8];
    private readonly bool[] _yellowLeds = new bool[8];

    private readonly Border[] _redBoxes = new Border[8];
    private readonly TextBlock[] _redLabels = new TextBlock[8];

    private readonly Border[] _greenBoxes = new Border[8];
    private readonly TextBlock[] _greenLabels = new TextBlock[8];

    private readonly Border[] _blueBoxes = new Border[8];
    private readonly TextBlock[] _blueLabels = new TextBlock[8];

    private readonly Border[] _yellowBoxes = new Border[8];
    private readonly TextBlock[] _yellowLabels = new TextBlock[8];

    private readonly Border[] _blendedBoxes = new Border[8];
    private readonly TextBlock[] _blendedLabels = new TextBlock[8];

    // Color palette aligned with LedDisplay (LED Ring)
    private static readonly IBrush RedActiveBrush = new SolidColorBrush(Color.Parse("#EF4444"));
    private static readonly IBrush RedActiveBorderBrush = new SolidColorBrush(Color.Parse("#DC2626"));
    private static readonly IBrush RedInactiveBrush = new SolidColorBrush(Color.Parse("#251515"));
    private static readonly IBrush RedInactiveBorderBrush = new SolidColorBrush(Color.Parse("#381F1F"));
    private static readonly IBrush RedInactiveTextBrush = new SolidColorBrush(Color.Parse("#753333"));

    private static readonly IBrush GreenActiveBrush = new SolidColorBrush(Color.Parse("#10B981"));
    private static readonly IBrush GreenActiveBorderBrush = new SolidColorBrush(Color.Parse("#059669"));
    private static readonly IBrush GreenInactiveBrush = new SolidColorBrush(Color.Parse("#14221A"));
    private static readonly IBrush GreenInactiveBorderBrush = new SolidColorBrush(Color.Parse("#1D3327"));
    private static readonly IBrush GreenInactiveTextBrush = new SolidColorBrush(Color.Parse("#337550"));

    private static readonly IBrush BlueActiveBrush = new SolidColorBrush(Color.Parse("#3B82F6"));
    private static readonly IBrush BlueActiveBorderBrush = new SolidColorBrush(Color.Parse("#2563EB"));
    private static readonly IBrush BlueInactiveBrush = new SolidColorBrush(Color.Parse("#141B26"));
    private static readonly IBrush BlueInactiveBorderBrush = new SolidColorBrush(Color.Parse("#1E2A3C"));
    private static readonly IBrush BlueInactiveTextBrush = new SolidColorBrush(Color.Parse("#334C75"));

    private static readonly IBrush YellowActiveBrush = new SolidColorBrush(Color.Parse("#F59E0B"));
    private static readonly IBrush YellowActiveBorderBrush = new SolidColorBrush(Color.Parse("#D97706"));
    private static readonly IBrush YellowInactiveBrush = new SolidColorBrush(Color.Parse("#252014"));
    private static readonly IBrush YellowInactiveBorderBrush = new SolidColorBrush(Color.Parse("#3A321E"));
    private static readonly IBrush YellowInactiveTextBrush = new SolidColorBrush(Color.Parse("#756233"));

    private static readonly IBrush BlendedInactiveBrush = new SolidColorBrush(Color.Parse("#161616"));
    private static readonly IBrush BlendedInactiveBorderBrush = new SolidColorBrush(Color.Parse("#2E2E32"));
    private static readonly IBrush BlendedInactiveTextBrush = new SolidColorBrush(Color.Parse("#55555B"));

    // Badge styling brushes
    private static readonly IBrush NeutralBadgeBackground = new SolidColorBrush(Color.Parse("#1E1E1E"));
    private static readonly IBrush NeutralBadgeBorder = new SolidColorBrush(Color.Parse("#333333"));
    private static readonly IBrush NeutralBadgeText = new SolidColorBrush(Color.Parse("#888888"));

    private static readonly IBrush StrobeActiveBackground = new SolidColorBrush(Color.Parse("#2D2514"));
    private static readonly IBrush StrobeActiveBorder = new SolidColorBrush(Color.Parse("#F59E0B"));
    private static readonly IBrush StrobeActiveText = new SolidColorBrush(Color.Parse("#FBBF24"));

    private static readonly IBrush FogActiveBackground = new SolidColorBrush(Color.Parse("#14232C"));
    private static readonly IBrush FogActiveBorder = new SolidColorBrush(Color.Parse("#0EA5E9"));
    private static readonly IBrush FogActiveText = new SolidColorBrush(Color.Parse("#38BDF8"));

    private static readonly IBrush BonusActiveBackground = new SolidColorBrush(Color.Parse("#2C142C"));
    private static readonly IBrush BonusActiveBorder = new SolidColorBrush(Color.Parse("#D946EF"));
    private static readonly IBrush BonusActiveText = new SolidColorBrush(Color.Parse("#F472B6"));

    // Beat Segment Chips Brushes
    private static readonly IBrush ChipInactiveBackground = new SolidColorBrush(Color.Parse("#1A1A1A"));
    private static readonly IBrush ChipInactiveBorder = new SolidColorBrush(Color.Parse("#2C2C2C"));
    private static readonly IBrush ChipInactiveTextBrush = new SolidColorBrush(Color.Parse("#666666"));

    private static readonly IBrush BeatOffBackground = new SolidColorBrush(Color.Parse("#262626"));
    private static readonly IBrush BeatOffBorder = new SolidColorBrush(Color.Parse("#404040"));
    private static readonly IBrush BeatOffTextBrush = new SolidColorBrush(Color.Parse("#A3A3A3"));

    private static readonly IBrush BeatWeakBackground = new SolidColorBrush(Color.Parse("#132338"));
    private static readonly IBrush BeatWeakBorder = new SolidColorBrush(Color.Parse("#3B82F6"));
    private static readonly IBrush BeatWeakTextBrush = new SolidColorBrush(Color.Parse("#60A5FA"));

    private static readonly IBrush BeatMeasBackground = new SolidColorBrush(Color.Parse("#2E240F"));
    private static readonly IBrush BeatMeasBorder = new SolidColorBrush(Color.Parse("#F59E0B"));
    private static readonly IBrush BeatMeasTextBrush = new SolidColorBrush(Color.Parse("#FCD34D"));

    private static readonly IBrush BeatStrongBackground = new SolidColorBrush(Color.Parse("#2B142E"));
    private static readonly IBrush BeatStrongBorder = new SolidColorBrush(Color.Parse("#D946EF"));
    private static readonly IBrush BeatStrongTextBrush = new SolidColorBrush(Color.Parse("#F472B6"));

    private static readonly IBrush WhiteTextBrush = Brushes.White;
    private static readonly IBrush BlackTextBrush = Brushes.Black;

    private readonly DispatcherTimer _beatPulseTimer;
    private byte _activePulseBeat = 0;
    private readonly DispatcherTimer _strobeOffDebounceTimer;
    private string _currentStrobeState = "OFF";
    private readonly DispatcherTimer _bonusEffectHoldTimer;
    private int _bonusTriggerCount;
    private bool _isDisposed;

    public static StageKitStatusWindow? ActiveInstance { get; private set; }

    public static void CloseActiveInstance()
    {
        if (ActiveInstance != null)
        {
            var win = ActiveInstance;
            ActiveInstance = null;
            try
            {
                win.Close();
            }
            catch (Exception) { }
        }
    }

    public StageKitStatusWindow()
    {
        ActiveInstance = this;
        InitializeComponent();
        BuildLedGrids();

        // 100ms rhythm pulse timer so beats flash crisply and return to resting state
        _beatPulseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _beatPulseTimer.Tick += (_, _) =>
        {
            _beatPulseTimer.Stop();
            _activePulseBeat = 0;
            RenderBeatSegment((byte)UdpIntake.BeatByte.Off);
        };

        // 200ms debounce timer so strobe badge stays solid during active flashes without flickering
        _strobeOffDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _strobeOffDebounceTimer.Tick += (_, _) =>
        {
            _strobeOffDebounceTimer.Stop();
            _currentStrobeState = "OFF";
            ApplyStrobeBadgeState("OFF");
        };

        // 1200ms hold timer so instantaneous Bonus Effect triggers remain clearly visible on the badge
        _bonusEffectHoldTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1200)
        };
        _bonusEffectHoldTimer.Tick += (_, _) =>
        {
            _bonusEffectHoldTimer.Stop();
            ApplyBonusEffectBadgeState(false);
        };

        // Hardware command subscriptions
        UsbDeviceMonitor.OnStageKitCommand += OnStageKitEvent;

        // UDP LightShow telemetry subscriptions
        UdpIntake.Venue.PropertyChanged += OnVenuePropertyChanged;
        UdpIntake.BeatsPerMinute.PropertyChanged += OnBpmPropertyChanged;
        UdpIntake.OnLightingCue += OnLightingCueReceived;
        UdpIntake.OnFogState += OnFogStateReceived;
        UdpIntake.OnStrobeState += OnStrobeStateReceived;
        UdpIntake.OnBonusEffect += OnBonusEffectReceived;
        UdpIntake.OnPostProcessing += OnPostProcessingReceived;
        UdpIntake.OnSpotlight += OnSpotlightReceived;
        UdpIntake.OnBeat += OnBeatReceived;

        UpdateVenueText();
        UpdateBpmText();
        RenderBeatSegment(0);
        UpdateAllVisuals();

        Closing += (_, _) => CleanupSubscriptions();
        Closed += (_, _) => CleanupSubscriptions();
    }

    private void CleanupSubscriptions()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (ActiveInstance == this)
        {
            ActiveInstance = null;
        }

        _beatPulseTimer.Stop();
        _strobeOffDebounceTimer.Stop();
        _bonusEffectHoldTimer.Stop();
        UsbDeviceMonitor.OnStageKitCommand -= OnStageKitEvent;
        UdpIntake.Venue.PropertyChanged -= OnVenuePropertyChanged;
        UdpIntake.BeatsPerMinute.PropertyChanged -= OnBpmPropertyChanged;
        UdpIntake.OnLightingCue -= OnLightingCueReceived;
        UdpIntake.OnFogState -= OnFogStateReceived;
        UdpIntake.OnStrobeState -= OnStrobeStateReceived;
        UdpIntake.OnBonusEffect -= OnBonusEffectReceived;
        UdpIntake.OnPostProcessing -= OnPostProcessingReceived;
        UdpIntake.OnSpotlight -= OnSpotlightReceived;
        UdpIntake.OnBeat -= OnBeatReceived;
    }

    private void BuildLedGrids()
    {
        for (int i = 0; i < 8; i++)
        {
            int number = i + 1;

            // Red
            CreateLedBox(number, out _redBoxes[i], out _redLabels[i]);
            RedLedGrid.Children.Add(_redBoxes[i]);

            // Green
            CreateLedBox(number, out _greenBoxes[i], out _greenLabels[i]);
            GreenLedGrid.Children.Add(_greenBoxes[i]);

            // Blue
            CreateLedBox(number, out _blueBoxes[i], out _blueLabels[i]);
            BlueLedGrid.Children.Add(_blueBoxes[i]);

            // Yellow
            CreateLedBox(number, out _yellowBoxes[i], out _yellowLabels[i]);
            YellowLedGrid.Children.Add(_yellowBoxes[i]);

            // Blended
            CreateLedBox(number, out _blendedBoxes[i], out _blendedLabels[i]);
            BlendedLedGrid.Children.Add(_blendedBoxes[i]);
        }
    }

    private static void CreateLedBox(int number, out Border box, out TextBlock label)
    {
        label = new TextBlock
        {
            Text = number.ToString(),
            FontWeight = FontWeight.Bold,
            FontSize = 12,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        box = new Border
        {
            Width = 32,
            Height = 28,
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(3, 2),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Child = label
        };
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.BeginMoveDrag(e);
        }
    }

    private void OnPinButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.Topmost = !this.Topmost;
        if (PinButton != null)
        {
            var pathIcon = new PathIcon
            {
                Width = 14,
                Height = 14
            };

            if (this.Topmost)
            {
                pathIcon.Data = Geometry.Parse("M73 39.1C63.6 29.7 48.4 29.7 39.1 39.1C29.8 48.5 29.7 63.7 39 73.1L567 601.1C576.4 610.5 591.6 610.5 600.9 601.1C610.2 591.7 610.3 576.5 600.9 567.2L449.8 416L480 416C490 416 499.5 411.3 505.5 403.3C511.5 395.3 513.5 384.9 510.7 375.2L507 361.8C494.6 318.5 466 283.3 428.8 262.1L418.5 128L448 128C465.7 128 480 113.7 480 96C480 78.3 465.7 64 448 64L192 64C184.6 64 177.9 66.5 172.5 70.6L222.1 120.3L217.3 183.4L73 39.1zM314.2 416L181.7 283.6C159 304.1 141.9 331 133 361.9L129.2 375.3C126.4 385 128.4 395.3 134.4 403.4C140.4 411.5 150 416 160 416L314.2 416zM288 576C288 593.7 302.3 608 320 608C337.7 608 352 593.7 352 576L352 464L288 464L288 576z");
            }
            else
            {
                pathIcon.Data = Geometry.Parse("M160 96C160 78.3 174.3 64 192 64L448 64C465.7 64 480 78.3 480 96C480 113.7 465.7 128 448 128L418.5 128L428.8 262.1C465.9 283.3 494.6 318.5 507 361.8L510.8 375.2C513.6 384.9 511.6 395.2 505.6 403.3C499.6 411.4 490 416 480 416L160 416C150 416 140.5 411.3 134.5 403.3C128.5 395.3 126.5 384.9 129.3 375.2L133 361.8C145.4 318.5 174 283.3 211.2 262.1L221.5 128L192 128C174.3 128 160 113.7 160 96zM288 464L352 464L352 576C352 593.7 337.7 608 320 608C302.3 608 288 593.7 288 576L288 464z");
            }
            PinButton.Content = pathIcon;
        }
    }

    private void OnVenuePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isDisposed) return;
        Dispatcher.UIThread.Post(UpdateVenueText);
    }

    private void OnBpmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isDisposed) return;
        Dispatcher.UIThread.Post(UpdateBpmText);
    }

    private void OnLightingCueReceived(byte cueByte)
    {
        if (_isDisposed) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (_isDisposed || LightingCueText == null) return;
            if (Enum.IsDefined(typeof(UdpIntake.CueByte), (int)cueByte))
            {
                var cueEnum = (UdpIntake.CueByte)cueByte;
                LightingCueText.Text = FormatEnumName(cueEnum.ToString());
            }
            else
            {
                LightingCueText.Text = $"Cue #{cueByte}";
            }
        });
    }

    private void OnPostProcessingReceived(byte ppByte)
    {
        if (_isDisposed) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (_isDisposed || PostProcessingText == null) return;
            var desc = Enum.IsDefined(typeof(UdpIntake.PostProcessingByte), (int)ppByte)
                ? ((UdpIntake.PostProcessingByte)ppByte).ToString()
                : "Unknown";
            PostProcessingText.Text = FormatEnumName(desc);
        });
    }

    private void OnSpotlightReceived(byte spotByte)
    {
        if (_isDisposed) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (_isDisposed || SpotlightText == null) return;
            var desc = FormatSpotlightText(spotByte);
            SpotlightText.Text = string.IsNullOrWhiteSpace(desc) ? "None" : desc;
        });
    }

    private static string FormatSpotlightText(byte byteValue)
    {
        var result = "";
        foreach (UdpIntake.PerformerByte note in Enum.GetValues<UdpIntake.PerformerByte>())
        {
            if (note == UdpIntake.PerformerByte.None || (byteValue & (byte)note) == 0) continue;
            if (result != "") result += ", ";
            result += note.ToString();
        }
        return string.IsNullOrEmpty(result) ? "None" : result;
    }

    private void OnFogStateReceived(bool isFog)
    {
        if (_isDisposed) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (!_isDisposed) UpdateFogState(isFog);
        });
    }

    private void OnStrobeStateReceived(byte strobeByte)
    {
        if (_isDisposed) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (_isDisposed) return;
            var desc = (UdpIntake.CueByte)strobeByte switch
            {
                UdpIntake.CueByte.Strobe_Slow => "SLOW",
                UdpIntake.CueByte.Strobe_Medium => "MEDIUM",
                UdpIntake.CueByte.Strobe_Fast => "FAST",
                UdpIntake.CueByte.Strobe_Fastest => "FASTEST",
                _ => "OFF"
            };
            UpdateStrobeState(desc);
        });
    }

    private void OnBonusEffectReceived(bool isBonus)
    {
        if (_isDisposed) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (!_isDisposed) UpdateBonusEffect(isBonus);
        });
    }

    private void OnBeatReceived(byte beatByte)
    {
        if (_isDisposed) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (_isDisposed) return;
            if (beatByte == (byte)UdpIntake.BeatByte.Off)
            {
                _beatPulseTimer.Stop();
                _activePulseBeat = 0;
                RenderBeatSegment((byte)UdpIntake.BeatByte.Off);
                return;
            }

            // If a Measure or Strong beat is currently flashing, ignore the immediate trailing Weak fallback
            if (_activePulseBeat is (byte)UdpIntake.BeatByte.Measure or (byte)UdpIntake.BeatByte.Strong
                && beatByte == (byte)UdpIntake.BeatByte.Weak)
            {
                return;
            }

            _activePulseBeat = beatByte;
            RenderBeatSegment(beatByte);
            _beatPulseTimer.Stop();
            _beatPulseTimer.Start();
        });
    }

    private void RenderBeatSegment(byte beatByte)
    {
        if (BeatOffChip == null || BeatWeakChip == null || BeatMeasChip == null || BeatStrongChip == null)
            return;

        // Reset all to default inactive
        SetChipStyle(BeatOffChip, BeatOffText, false, BeatOffBackground, BeatOffBorder, BeatOffTextBrush);
        SetChipStyle(BeatWeakChip, BeatWeakText, false, BeatWeakBackground, BeatWeakBorder, BeatWeakTextBrush);
        SetChipStyle(BeatMeasChip, BeatMeasText, false, BeatMeasBackground, BeatMeasBorder, BeatMeasTextBrush);
        SetChipStyle(BeatStrongChip, BeatStrongText, false, BeatStrongBackground, BeatStrongBorder, BeatStrongTextBrush);

        switch (beatByte)
        {
            case (byte)UdpIntake.BeatByte.Measure:
                SetChipStyle(BeatMeasChip, BeatMeasText, true, BeatMeasBackground, BeatMeasBorder, BeatMeasTextBrush);
                break;

            case (byte)UdpIntake.BeatByte.Strong:
                SetChipStyle(BeatStrongChip, BeatStrongText, true, BeatStrongBackground, BeatStrongBorder, BeatStrongTextBrush);
                break;

            case (byte)UdpIntake.BeatByte.Weak:
                SetChipStyle(BeatWeakChip, BeatWeakText, true, BeatWeakBackground, BeatWeakBorder, BeatWeakTextBrush);
                break;

            default:
                SetChipStyle(BeatOffChip, BeatOffText, true, BeatOffBackground, BeatOffBorder, BeatOffTextBrush);
                break;
        }
    }

    private static void SetChipStyle(Border chip, TextBlock text, bool isActive, IBrush activeBg, IBrush activeBorder, IBrush activeText)
    {
        chip.Background = isActive ? activeBg : ChipInactiveBackground;
        chip.BorderBrush = isActive ? activeBorder : ChipInactiveBorder;
        text.Foreground = isActive ? activeText : ChipInactiveTextBrush;
    }

    private void UpdateVenueText()
    {
        if (VenueSizeText != null)
        {
            var desc = UdpIntake.GetVenueSizeByteDescription(UdpIntake.Venue.Value);
            VenueSizeText.Text = string.IsNullOrWhiteSpace(desc) ? "No Venue" : desc;
        }
    }

    private void UpdateBpmText()
    {
        if (BpmText != null)
        {
            var bpm = UdpIntake.BeatsPerMinute?.Value ?? 0;
            BpmText.Text = bpm > 0 ? $"({bpm:0} BPM)" : "";
        }
    }

    private static string FormatEnumName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Default";
        return name.Replace('_', ' ');
    }

    private void OnStageKitEvent(StageKitTalker.CommandId commandId, byte parameter)
    {
        if (_isDisposed) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (_isDisposed) return;
            switch (commandId)
            {
                case StageKitTalker.CommandId.DisableAll:
                    Array.Clear(_redLeds, 0, _redLeds.Length);
                    Array.Clear(_greenLeds, 0, _greenLeds.Length);
                    Array.Clear(_blueLeds, 0, _blueLeds.Length);
                    Array.Clear(_yellowLeds, 0, _yellowLeds.Length);
                    UpdateStrobeState("OFF");
                    UpdateFogState(false);
                    _bonusEffectHoldTimer.Stop();
                    ApplyBonusEffectBadgeState(false);
                    RenderBeatSegment(0);
                    break;

                case StageKitTalker.CommandId.RedLeds:
                    for (int i = 0; i < 8; i++)
                    {
                        _redLeds[i] = (parameter & (1 << i)) != 0;
                    }
                    break;

                case StageKitTalker.CommandId.GreenLeds:
                    for (int i = 0; i < 8; i++)
                    {
                        _greenLeds[i] = (parameter & (1 << i)) != 0;
                    }
                    break;

                case StageKitTalker.CommandId.BlueLeds:
                    for (int i = 0; i < 8; i++)
                    {
                        _blueLeds[i] = (parameter & (1 << i)) != 0;
                    }
                    break;

                case StageKitTalker.CommandId.YellowLeds:
                    for (int i = 0; i < 8; i++)
                    {
                        _yellowLeds[i] = (parameter & (1 << i)) != 0;
                    }
                    break;

                case StageKitTalker.CommandId.FogOn:
                    UpdateFogState(true);
                    break;

                case StageKitTalker.CommandId.FogOff:
                    UpdateFogState(false);
                    break;

                case StageKitTalker.CommandId.StrobeOff:
                    UpdateStrobeState("OFF");
                    break;

                case StageKitTalker.CommandId.StrobeSlow:
                    UpdateStrobeState("SLOW");
                    break;

                case StageKitTalker.CommandId.StrobeMedium:
                    UpdateStrobeState("MED");
                    break;

                case StageKitTalker.CommandId.StrobeFast:
                    UpdateStrobeState("FAST");
                    break;

                case StageKitTalker.CommandId.StrobeFastest:
                    UpdateStrobeState("FASTEST");
                    break;
            }

            UpdateAllVisuals();
        });
    }

    private void UpdateStrobeState(string state)
    {
        if (state != "OFF" && state != "DISABLED" && state != "UNKNOWN")
        {
            _strobeOffDebounceTimer.Stop();
            _currentStrobeState = state;
            ApplyStrobeBadgeState(state);
        }
        else
        {
            if (_currentStrobeState != "OFF" && !_strobeOffDebounceTimer.IsEnabled)
            {
                _strobeOffDebounceTimer.Start();
            }
            else if (_currentStrobeState == "OFF")
            {
                ApplyStrobeBadgeState("OFF");
            }
        }
    }

    private void ApplyStrobeBadgeState(string state)
    {
        if (StrobeStateText != null && StrobeBadge != null)
        {
            StrobeStateText.Text = state;
            bool isActive = state != "OFF" && state != "DISABLED" && state != "UNKNOWN";

            StrobeBadge.Background = isActive ? StrobeActiveBackground : NeutralBadgeBackground;
            StrobeBadge.BorderBrush = isActive ? StrobeActiveBorder : NeutralBadgeBorder;
            StrobeStateText.Foreground = isActive ? StrobeActiveText : NeutralBadgeText;
        }
    }

    private void UpdateFogState(bool isFog)
    {
        if (FogStateText != null && FogBadge != null)
        {
            FogStateText.Text = isFog ? "ON" : "OFF";

            FogBadge.Background = isFog ? FogActiveBackground : NeutralBadgeBackground;
            FogBadge.BorderBrush = isFog ? FogActiveBorder : NeutralBadgeBorder;
            FogStateText.Foreground = isFog ? FogActiveText : NeutralBadgeText;
        }
    }

    private void UpdateBonusEffect(bool isBonus)
    {
        if (_isDisposed) return;

        if (isBonus)
        {
            _bonusTriggerCount++;
            _bonusEffectHoldTimer.Stop();
            ApplyBonusEffectBadgeState(true);
            _bonusEffectHoldTimer.Start();
        }
    }

    private void ApplyBonusEffectBadgeState(bool isActive)
    {
        if (!isActive)
        {
            _bonusTriggerCount = 0;
        }

        if (BonusEffectText != null && BonusBadge != null)
        {
            if (isActive)
            {
                BonusEffectText.Text = _bonusTriggerCount > 1
                    ? $"TRIGGERED! x{_bonusTriggerCount}"
                    : "TRIGGERED!";
            }
            else
            {
                BonusEffectText.Text = "OFF";
            }

            BonusBadge.Background = isActive ? BonusActiveBackground : NeutralBadgeBackground;
            BonusBadge.BorderBrush = isActive ? BonusActiveBorder : NeutralBadgeBorder;
            BonusEffectText.Foreground = isActive ? BonusActiveText : NeutralBadgeText;
        }
    }

    private void UpdateAllVisuals()
    {
        int redActive = 0;
        int greenActive = 0;
        int blueActive = 0;
        int yellowActive = 0;

        for (int i = 0; i < 8; i++)
        {
            // Red
            if (_redLeds[i])
            {
                redActive++;
                _redBoxes[i].Background = RedActiveBrush;
                _redBoxes[i].BorderBrush = RedActiveBorderBrush;
                _redBoxes[i].BorderThickness = new Thickness(1);
                _redLabels[i].Foreground = WhiteTextBrush;
            }
            else
            {
                _redBoxes[i].Background = RedInactiveBrush;
                _redBoxes[i].BorderBrush = RedInactiveBorderBrush;
                _redBoxes[i].BorderThickness = new Thickness(1);
                _redLabels[i].Foreground = RedInactiveTextBrush;
            }

            // Green
            if (_greenLeds[i])
            {
                greenActive++;
                _greenBoxes[i].Background = GreenActiveBrush;
                _greenBoxes[i].BorderBrush = GreenActiveBorderBrush;
                _greenBoxes[i].BorderThickness = new Thickness(1);
                _greenLabels[i].Foreground = WhiteTextBrush;
            }
            else
            {
                _greenBoxes[i].Background = GreenInactiveBrush;
                _greenBoxes[i].BorderBrush = GreenInactiveBorderBrush;
                _greenBoxes[i].BorderThickness = new Thickness(1);
                _greenLabels[i].Foreground = GreenInactiveTextBrush;
            }

            // Blue
            if (_blueLeds[i])
            {
                blueActive++;
                _blueBoxes[i].Background = BlueActiveBrush;
                _blueBoxes[i].BorderBrush = BlueActiveBorderBrush;
                _blueBoxes[i].BorderThickness = new Thickness(1);
                _blueLabels[i].Foreground = WhiteTextBrush;
            }
            else
            {
                _blueBoxes[i].Background = BlueInactiveBrush;
                _blueBoxes[i].BorderBrush = BlueInactiveBorderBrush;
                _blueBoxes[i].BorderThickness = new Thickness(1);
                _blueLabels[i].Foreground = BlueInactiveTextBrush;
            }

            // Yellow
            if (_yellowLeds[i])
            {
                yellowActive++;
                _yellowBoxes[i].Background = YellowActiveBrush;
                _yellowBoxes[i].BorderBrush = YellowActiveBorderBrush;
                _yellowBoxes[i].BorderThickness = new Thickness(1);
                _yellowLabels[i].Foreground = WhiteTextBrush;
            }
            else
            {
                _yellowBoxes[i].Background = YellowInactiveBrush;
                _yellowBoxes[i].BorderBrush = YellowInactiveBorderBrush;
                _yellowBoxes[i].BorderThickness = new Thickness(1);
                _yellowLabels[i].Foreground = YellowInactiveTextBrush;
            }

            // Blended Box calculation
            UpdateBlendedBox(i);
        }

        RedActiveCountText.Text = redActive > 0 ? $"{redActive} active" : "inactive";
        GreenActiveCountText.Text = greenActive > 0 ? $"{greenActive} active" : "inactive";
        BlueActiveCountText.Text = blueActive > 0 ? $"{blueActive} active" : "inactive";
        YellowActiveCountText.Text = yellowActive > 0 ? $"{yellowActive} active" : "inactive";
    }

    private void UpdateBlendedBox(int index)
    {
        var (cr, cg, cb, isActive) = StageKitColorBlender.BlendPod(
            _blueLeds[index],
            _redLeds[index],
            _greenLeds[index],
            _yellowLeds[index]
        );

        if (!isActive)
        {
            _blendedBoxes[index].Background = BlendedInactiveBrush;
            _blendedBoxes[index].BorderBrush = BlendedInactiveBorderBrush;
            _blendedBoxes[index].BorderThickness = new Thickness(1);
            _blendedLabels[index].Foreground = BlendedInactiveTextBrush;
            return;
        }

        var blendedColor = Color.FromRgb(cr, cg, cb);
        _blendedBoxes[index].Background = new SolidColorBrush(blendedColor);
        _blendedBoxes[index].BorderBrush = new SolidColorBrush(Color.FromArgb(220, cr, cg, cb));
        _blendedBoxes[index].BorderThickness = new Thickness(1);

        // Calculate perceived luminance to select high-contrast text color
        double luminance = (0.299 * cr) + (0.587 * cg) + (0.114 * cb);
        _blendedLabels[index].Foreground = luminance > 155 ? BlackTextBrush : WhiteTextBrush;
    }
}
