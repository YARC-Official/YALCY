using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using System;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using YALCY.Integrations.StageKit;
using YALCY.Usb;

namespace YALCY.ViewModels;

public class LedDisplay : Control
{
    private bool[] _ledStates;

    public static readonly StyledProperty<int> ColorProperty = AvaloniaProperty.Register<LedDisplay, int>(nameof(Color));

    // Precise StageKit LED positions (0-200 coordinate system, centered at 100, 100)
    // Blue (Color 0): Diamond (losango) structure (4 outer tips + 4 edge midpoints on the diamond perimeter)
    private readonly Point[] _blueLedPositions = new Point[]
    {
        new Point(100, 32),   // LED 0 - Top Tip (0, -68)
        new Point(134, 66),   // LED 1 - Top-Right Edge Midpoint (+34, -34)
        new Point(168, 100),  // LED 2 - Right Tip (+68, 0)
        new Point(134, 134),  // LED 3 - Bottom-Right Edge Midpoint (+34, +34)
        new Point(100, 168),  // LED 4 - Bottom Tip (0, +68)
        new Point(66, 134),   // LED 5 - Bottom-Left Edge Midpoint (-34, +34)
        new Point(32, 100),   // LED 6 - Left Tip (-68, 0)
        new Point(66, 66),    // LED 7 - Top-Left Edge Midpoint (-34, -34)
    };

    // Green (Color 1): 4 pairs grouped around each of the 4 diamond tips
    private readonly Point[] _greenLedPositions = new Point[]
    {
        new Point(135, 12),   // LED 0 - Top pair right (+35, -88)
        new Point(188, 65),   // LED 1 - Right pair top (+88, -35)
        new Point(188, 135),  // LED 2 - Right pair bottom (+88, +35)
        new Point(135, 188),  // LED 3 - Bottom pair right (+35, +88)
        new Point(65, 188),   // LED 4 - Bottom pair left (-35, +88)
        new Point(12, 135),   // LED 5 - Left pair bottom (-88, +35)
        new Point(12, 65),    // LED 6 - Left pair top (-88, -35)
        new Point(65, 12)     // LED 7 - Top pair left (-35, -88)
    };

    // Yellow (Color 2): Outer ring (radius 128), 8 LEDs aligned cardinally and diagonally
    private readonly Point[] _yellowLedPositions = new Point[]
    {
        new Point(100, -28),      // LED 0 - 12 o'clock (0, -128)
        new Point(190.5, 9.5),    // LED 1 - 1:30 (+90.5, -90.5)
        new Point(228, 100),      // LED 2 - 3 o'clock (+128, 0)
        new Point(190.5, 190.5),  // LED 3 - 4:30 (+90.5, +90.5)
        new Point(100, 228),      // LED 4 - 6 o'clock (0, +128)
        new Point(9.5, 190.5),    // LED 5 - 7:30 (-90.5, +90.5)
        new Point(-28, 100),      // LED 6 - 9 o'clock (-128, 0)
        new Point(9.5, 9.5)       // LED 7 - 10:30 (-90.5, -90.5)
    };

    // Red (Color 3): Outer ring (radius 128), 8 LEDs interleaved with yellow
    private readonly Point[] _redLedPositions = new Point[]
    {
        new Point(149.0, -18.3),  // LED 0 (+49.0, -118.3)
        new Point(218.3, 51.0),   // LED 1 (+118.3, -49.0)
        new Point(218.3, 149.0),  // LED 2 (+118.3, +49.0)
        new Point(149.0, 218.3),  // LED 3 (+49.0, +118.3)
        new Point(51.0, 218.3),   // LED 4 (-49.0, +118.3)
        new Point(-18.3, 149.0),  // LED 5 (-118.3, +49.0)
        new Point(-18.3, 51.0),   // LED 6 (-118.3, -49.0)
        new Point(51.0, -18.3)    // LED 7 (-49.0, -118.3)
    };

    // Pre-allocated Glow & Dome Brushes for High Performance
    // Dark Mode Inactive Socket Brushes
    private static readonly IBrush DarkOffBezelBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#262626"));
    private static readonly IPen DarkOffBezelPen = new Pen(new SolidColorBrush(Avalonia.Media.Color.Parse("#333333")), 1.5);
    private static readonly IBrush DarkOffInnerBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#141414"));
    private static readonly IBrush DarkOffLabelBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#8B8FB5"));

    // Light Mode Inactive Socket Brushes (Soft light metallic/frosted look)
    private static readonly IBrush LightOffBezelBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#D8DEE9"));
    private static readonly IPen LightOffBezelPen = new Pen(new SolidColorBrush(Avalonia.Media.Color.Parse("#C2C9D6")), 1.5);
    private static readonly IBrush LightOffInnerBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#E5E9F0"));
    private static readonly IBrush LightOffLabelBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#5C677D"));

    // Color definitions
    private static readonly Color ColorBlue = Avalonia.Media.Color.Parse("#3B82F6");
    private static readonly Color ColorGreen = Avalonia.Media.Color.Parse("#10B981");
    private static readonly Color ColorYellow = Avalonia.Media.Color.Parse("#F59E0B");
    private static readonly Color ColorRed = Avalonia.Media.Color.Parse("#EF4444");

    private static bool _globalShowLabels;
    public static bool GlobalShowLabels
    {
        get => _globalShowLabels;
        set
        {
            if (_globalShowLabels != value)
            {
                _globalShowLabels = value;
                OnGlobalShowLabelsChanged?.Invoke();
            }
        }
    }
    public static event Action? OnGlobalShowLabelsChanged;

    static LedDisplay()
    {
        AffectsRender<LedDisplay>(ColorProperty);
    }

    public LedDisplay()
    {
        LedStates = new bool[8];
        AttachedToVisualTree += (_, _) =>
        {
            UsbDeviceMonitor.OnStageKitCommand += OnStageKitEvent;
            OnGlobalShowLabelsChanged += OnGlobalLabelsToggled;
            if (Application.Current != null)
            {
                Application.Current.ActualThemeVariantChanged += OnThemeVariantChanged;
            }
        };
        DetachedFromVisualTree += (_, _) =>
        {
            UsbDeviceMonitor.OnStageKitCommand -= OnStageKitEvent;
            OnGlobalShowLabelsChanged -= OnGlobalLabelsToggled;
            if (Application.Current != null)
            {
                Application.Current.ActualThemeVariantChanged -= OnThemeVariantChanged;
            }
        };
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(InvalidateVisual);
    }

    private void OnGlobalLabelsToggled()
    {
        Dispatcher.UIThread.InvokeAsync(InvalidateVisual);
    }

    public int Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public bool[] LedStates
    {
        get => _ledStates;
        set
        {
            _ledStates = value;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (LedStates == null) return;

        Point[]? ledPositions = Color switch
        {
            0 => _blueLedPositions,
            1 => _greenLedPositions,
            2 => _yellowLedPositions,
            3 => _redLedPositions,
            _ => null
        };

        if (ledPositions == null) return;

        Color mainColor = Color switch
        {
            0 => ColorBlue,
            1 => ColorGreen,
            2 => ColorYellow,
            3 => ColorRed,
            _ => ColorBlue
        };

        Color coreColor = Color switch
        {
            0 => Avalonia.Media.Color.Parse("#BFDBFE"),
            1 => Avalonia.Media.Color.Parse("#A7F3D0"),
            2 => Avalonia.Media.Color.Parse("#FEF08A"),
            3 => Avalonia.Media.Color.Parse("#FECACA"),
            _ => Avalonia.Media.Color.Parse("#FFFFFF")
        };

        Color deepColor = Color switch
        {
            0 => Avalonia.Media.Color.Parse("#1D4ED8"),
            1 => Avalonia.Media.Color.Parse("#047857"),
            2 => Avalonia.Media.Color.Parse("#B45309"),
            3 => Avalonia.Media.Color.Parse("#B91C1C"),
            _ => Avalonia.Media.Color.Parse("#1D4ED8")
        };

        bool isDark = Application.Current == null ||
                      Application.Current.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark ||
                      (Application.Current.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Default &&
                       Application.Current.PlatformSettings?.GetColorValues().ThemeVariant == Avalonia.Platform.PlatformThemeVariant.Dark);

        var offBezelBrush = isDark ? DarkOffBezelBrush : LightOffBezelBrush;
        var offBezelPen = isDark ? DarkOffBezelPen : LightOffBezelPen;
        var offInnerBrush = isDark ? DarkOffInnerBrush : LightOffInnerBrush;
        var offLabelBrush = isDark ? DarkOffLabelBrush : LightOffLabelBrush;

        // Draw each of the 8 LEDs
        for (int i = 0; i < 8 && i < ledPositions.Length; i++)
        {
            var center = ledPositions[i];
            bool isOn = LedStates[i];

            if (isOn)
            {
                // 1. Outer Bloom / Radial Glow
                var glowBrush = new RadialGradientBrush
                {
                    Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                    GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                    Radius = 0.5,
                    GradientStops =
                    {
                        new GradientStop(Avalonia.Media.Color.FromArgb(170, mainColor.R, mainColor.G, mainColor.B), 0.0),
                        new GradientStop(Avalonia.Media.Color.FromArgb(80, mainColor.R, mainColor.G, mainColor.B), 0.5),
                        new GradientStop(Avalonia.Media.Color.FromArgb(0, mainColor.R, mainColor.G, mainColor.B), 1.0)
                    }
                };
                context.DrawEllipse(glowBrush, null, center, 20, 20);

                // 2. High-intensity Diode Dome (3D Sphere)
                var domeBrush = new RadialGradientBrush
                {
                    Center = new RelativePoint(0.35, 0.35, RelativeUnit.Relative),
                    GradientOrigin = new RelativePoint(0.35, 0.35, RelativeUnit.Relative),
                    Radius = 0.65,
                    GradientStops =
                    {
                        new GradientStop(coreColor, 0.0),
                        new GradientStop(mainColor, 0.65),
                        new GradientStop(deepColor, 1.0)
                    }
                };
                var ringPen = new Pen(new SolidColorBrush(coreColor), 1.0);
                context.DrawEllipse(domeBrush, ringPen, center, 8.5, 8.5);

                // 3. Specular Pinpoint Highlight (hide when labels enabled for cleaner text legibility)
                if (!GlobalShowLabels)
                {
                    var highlightBrush = new SolidColorBrush(Avalonia.Media.Color.FromArgb(230, 255, 255, 255));
                    context.DrawEllipse(highlightBrush, null, new Point(center.X - 2.5, center.Y - 2.5), 2.2, 2.2);
                }
            }
            else
            {
                // Inactive Socket
                context.DrawEllipse(offBezelBrush, offBezelPen, center, 8.5, 8.5);
                context.DrawEllipse(offInnerBrush, null, center, 6.0, 6.0);
            }

            // Draw channel index label inside the LED socket (e.g. B1..B8, G1..G8, Y1..Y8, R1..R8)
            if (GlobalShowLabels)
            {
                string prefix = Color switch
                {
                    0 => "B",
                    1 => "G",
                    2 => "Y",
                    3 => "R",
                    _ => ""
                };
                string label = $"{prefix}{i + 1}";

                IBrush textBrush = isOn ? Brushes.White : offLabelBrush;
                var formattedText = new FormattedText(
                    label,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
                    6.5,
                    textBrush
                );

                context.DrawText(formattedText, new Point(center.X - formattedText.Width / 2, center.Y - formattedText.Height / 2));
            }
        }
    }

    private void OnStageKitEvent(StageKitTalker.CommandId commandId, byte parameter)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var updateLed = false;
            switch (commandId)
            {
                case StageKitTalker.CommandId.DisableAll:
                    Array.Clear(LedStates, 0, LedStates.Length);
                    InvalidateVisual();
                    return;

                case StageKitTalker.CommandId.BlueLeds:
                    updateLed = (Color == 0);
                    break;

                case StageKitTalker.CommandId.GreenLeds:
                    updateLed = (Color == 1);
                    break;

                case StageKitTalker.CommandId.YellowLeds:
                    updateLed = (Color == 2);
                    break;

                case StageKitTalker.CommandId.RedLeds:
                    updateLed = (Color == 3);
                    break;
            }

            if (!updateLed) return;
            for (int i = 0; i < 8; i++)
            {
                LedStates[i] = (parameter & (1 << i)) != 0;
            }
            InvalidateVisual();
        });
    }
}
