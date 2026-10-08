using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using YALCY.Integrations.WLED;
using YALCY.ViewModels;

namespace YALCY.Views.Components;

/// <summary>
/// High-performance, GPU-accelerated live LED strip visualizer control for WLED.
/// Renders direct RGBW diode outputs, pulse accents, and geometry transforms in real time.
/// </summary>
public sealed class WledStripVisualizer : Control
{
    private WledRgbwColor[]? _cachedBuffer;
    private int _cachedLedCount = 32;
    private bool _cachedReversed;
    private int _cachedOffset;
    private long _lastRenderTimestamp;
    private MainWindowViewModel? _viewModel;

    // Preallocated pens and brushes to prevent GC thrashing
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.FromRgb(20, 22, 28));
    private static readonly IBrush ChassisBorderBrush = new SolidColorBrush(Color.FromRgb(45, 50, 62));
    private static readonly IPen ChassisBorderPen = new Pen(ChassisBorderBrush, 1);
    private static readonly IBrush OffDiodeBrush = new SolidColorBrush(Color.FromRgb(32, 36, 44));
    private static readonly IPen OffDiodePen = new Pen(new SolidColorBrush(Color.FromRgb(48, 54, 66)), 1);
    private static readonly IBrush WhiteCoreBrush = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
    private static readonly Typeface LabelTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

    public WledStripVisualizer()
    {
        ClipToBounds = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is MainWindowViewModel vm)
        {
            _viewModel = vm;
            vm.WledTalker.OnFrameRendered += HandleFrameRendered;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_viewModel != null)
        {
            _viewModel.WledTalker.OnFrameRendered -= HandleFrameRendered;
            _viewModel = null;
        }
    }

    private void HandleFrameRendered(WledRgbwColor[] buffer, int ledCount, bool isReversed, int offset)
    {
        long now = Environment.TickCount64;
        // Cap visualizer framerate to ~35 FPS (28ms) to keep UI thread light
        if (now - _lastRenderTimestamp < 28) return;
        _lastRenderTimestamp = now;

        if (_cachedBuffer == null || _cachedBuffer.Length != ledCount)
        {
            _cachedBuffer = new WledRgbwColor[ledCount];
        }

        Array.Copy(buffer, _cachedBuffer, ledCount);
        _cachedLedCount = ledCount;
        _cachedReversed = isReversed;
        _cachedOffset = offset;

        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Background);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width <= 10 || height <= 10) return;

        // 1. Draw sleek chassis capsule
        var chassisRect = new RoundedRect(new Rect(0, 0, width, height), 10);
        context.DrawRectangle(BackgroundBrush, ChassisBorderPen, chassisRect);

        int ledCount = Math.Max(1, _cachedLedCount);
        int displayLeds = Math.Min(ledCount, 64); // Display up to 64 diode cells horizontally

        double paddingX = 14;
        double paddingTop = 12;
        double diodeHeight = Math.Max(8, height - 32);
        double availableWidth = width - (paddingX * 2);
        double slotWidth = availableWidth / displayLeds;
        double diodeWidth = Math.Max(3, Math.Min(slotWidth - 2, 14));

        bool hasBuffer = _cachedBuffer != null && _cachedBuffer.Length > 0;

        // 2. Render each physical diode
        for (int i = 0; i < displayLeds; i++)
        {
            int bufferIndex = (int)((long)i * ledCount / displayLeds);
            bufferIndex = Math.Clamp(bufferIndex, 0, hasBuffer ? _cachedBuffer!.Length - 1 : 0);

            WledRgbwColor c = hasBuffer ? _cachedBuffer![bufferIndex] : WledRgbwColor.Black;
            double x = paddingX + (i * slotWidth) + ((slotWidth - diodeWidth) / 2);
            double y = paddingTop;

            var diodeRect = new Rect(x, y, diodeWidth, diodeHeight);
            var roundedDiode = new RoundedRect(diodeRect, 3);

            bool isLit = c.R > 2 || c.G > 2 || c.B > 2 || c.W > 2;

            if (isLit)
            {
                // Glow aura
                byte alphaGlow = (byte)Math.Min(120, Math.Max(c.R, Math.Max(c.G, Math.Max(c.B, c.W))) / 2);
                var glowBrush = new SolidColorBrush(Color.FromArgb(alphaGlow, c.R, c.G, c.B));
                context.DrawRectangle(glowBrush, null, new RoundedRect(new Rect(x - 2, y - 2, diodeWidth + 4, diodeHeight + 4), 5));

                // Diode body
                var diodeBrush = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
                context.DrawRectangle(diodeBrush, null, roundedDiode);

                // Dedicated 4th White (W) Diode Core
                if (c.W > 10)
                {
                    double coreSize = Math.Max(2, diodeWidth * 0.45);
                    double coreX = x + (diodeWidth - coreSize) / 2;
                    double coreY = y + (diodeHeight - coreSize) / 2;
                    context.DrawRectangle(WhiteCoreBrush, null, new RoundedRect(new Rect(coreX, coreY, coreSize, coreSize), 2));
                }
            }
            else
            {
                // Off diode socket
                context.DrawRectangle(OffDiodeBrush, OffDiodePen, roundedDiode);
            }
        }

        // 3. Render telemetry badges & rulers below the strip
        double labelY = height - 16;
        var labelBrush = new SolidColorBrush(Color.FromRgb(140, 145, 160));

        // Start & End markers
        var textLeft = new FormattedText("LED 1", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelTypeface, 9, labelBrush);
        context.DrawText(textLeft, new Point(paddingX, labelY));

        var textRight = new FormattedText($"LED {ledCount}", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelTypeface, 9, labelBrush);
        context.DrawText(textRight, new Point(width - paddingX - textRight.Width, labelY));

        // Badges for Reverse / Offset
        double badgeX = width / 2;
        if (_cachedReversed)
        {
            var revBrush = new SolidColorBrush(Color.FromRgb(255, 170, 0));
            var revText = new FormattedText("◀ REVERSE DIRECTION", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelTypeface, 9, revBrush);
            context.DrawText(revText, new Point(badgeX - (revText.Width / 2) - 40, labelY));
        }

        if (_cachedOffset > 0)
        {
            var offsetBrush = new SolidColorBrush(Color.FromRgb(64, 196, 255));
            var offText = new FormattedText($"OFFSET: +{_cachedOffset}", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelTypeface, 9, offsetBrush);
            context.DrawText(offText, new Point(badgeX + 30, labelY));
        }
    }
}
