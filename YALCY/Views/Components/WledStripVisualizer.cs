using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using YALCY.Integrations.WLED;
using YALCY.ViewModels;

namespace YALCY.Views.Components;

/// <summary>
/// High-performance, GPU-accelerated live LED strip visualizer control for WLED.
/// Renders direct RGBW diode outputs, pulse accents, segment dividers, and optional stacked multi-strip lanes in real time.
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
    private static readonly Typeface MiniTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);

    // Segment & Stacked Lane styling
    private static readonly IPen SegmentDividerPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 70, 160, 255)), 1.5, DashStyle.Dash);
    private static readonly IBrush LaneBackgroundBrush = new SolidColorBrush(Color.FromArgb(160, 26, 29, 38));
    private static readonly IPen LaneBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(100, 58, 66, 82)), 1);
    private static readonly IBrush SegmentTagBrush = new SolidColorBrush(Color.FromRgb(100, 185, 255));

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
            vm.PropertyChanged += HandleViewModelPropertyChanged;
            vm.WledSegments.CollectionChanged += HandleSegmentsCollectionChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_viewModel != null)
        {
            _viewModel.WledTalker.OnFrameRendered -= HandleFrameRendered;
            _viewModel.PropertyChanged -= HandleViewModelPropertyChanged;
            _viewModel.WledSegments.CollectionChanged -= HandleSegmentsCollectionChanged;
            _viewModel = null;
        }
    }

    private void HandleSegmentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Background);
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.WledVisualizerStacked) ||
            e.PropertyName == nameof(MainWindowViewModel.WledMultiStripMode) ||
            e.PropertyName == nameof(MainWindowViewModel.WledVisualizerHeight))
        {
            Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Background);
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

        bool hasBuffer = _cachedBuffer != null && _cachedBuffer.Length > 0;
        bool isStacked = _viewModel != null && _viewModel.WledVisualizerStacked && _viewModel.WledSegments.Count > 1;

        if (isStacked)
        {
            RenderStackedMultiStrip(context, width, height, hasBuffer);
        }
        else
        {
            RenderContinuousStrip(context, width, height, hasBuffer);
        }
    }

    private void RenderStackedMultiStrip(DrawingContext context, double width, double height, bool hasBuffer)
    {
        var segments = _viewModel!.WledSegments;
        int count = segments.Count;
        if (count == 0) return;

        double paddingX = 12;
        double paddingTop = 8;
        double laneSpacing = 6;
        double totalLaneHeight = height - (paddingTop * 2) - ((count - 1) * laneSpacing);
        double laneHeight = Math.Max(26, totalLaneHeight / count);
        double availableWidth = width - (paddingX * 2);

        for (int s = 0; s < count; s++)
        {
            var seg = segments[s];
            double laneY = paddingTop + (s * (laneHeight + laneSpacing));
            var laneRect = new Rect(paddingX, laneY, availableWidth, laneHeight);
            var roundedLane = new RoundedRect(laneRect, 6);

            // Draw lane backdrop
            context.DrawRectangle(LaneBackgroundBrush, LaneBorderPen, roundedLane);

            // Lane badge / label
            string segTitle = string.IsNullOrWhiteSpace(seg.Name)
                ? $"Strip {seg.Id + 1}"
                : seg.Name;
            string segInfo = $"{segTitle} ({seg.Length} LEDs: {seg.Start}..{seg.Stop - 1})";
            var textBadge = new FormattedText(
                segInfo,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelTypeface,
                8.5,
                SegmentTagBrush);
            context.DrawText(textBadge, new Point(paddingX + 8, laneY + 3));

            // Diodes layout inside lane
            int segLength = Math.Max(1, seg.Length);
            int displayDiodes = Math.Min(segLength, 48);
            double diodeAreaLeft = paddingX + 8;
            double diodeAreaTop = laneY + 15;
            double diodeAreaWidth = availableWidth - 16;
            double diodeHeight = Math.Max(6, laneHeight - 19);
            double slotWidth = diodeAreaWidth / displayDiodes;
            double diodeWidth = Math.Max(2.5, Math.Min(slotWidth - 2, 12));

            for (int d = 0; d < displayDiodes; d++)
            {
                int bufferIndex = seg.Start + (int)((long)d * segLength / displayDiodes);
                bufferIndex = Math.Clamp(bufferIndex, 0, hasBuffer ? _cachedBuffer!.Length - 1 : 0);

                WledRgbwColor c = hasBuffer ? _cachedBuffer![bufferIndex] : WledRgbwColor.Black;
                double dx = diodeAreaLeft + (d * slotWidth) + ((slotWidth - diodeWidth) / 2);
                double dy = diodeAreaTop;

                DrawDiode(context, dx, dy, diodeWidth, diodeHeight, c);
            }
        }
    }

    private void RenderContinuousStrip(DrawingContext context, double width, double height, bool hasBuffer)
    {
        int ledCount = Math.Max(1, _cachedLedCount);
        int displayLeds = Math.Min(ledCount, 64); // Display up to 64 diode cells horizontally

        double paddingX = 14;
        double paddingTop = 12;
        double diodeHeight = Math.Max(8, height - 32);
        double availableWidth = width - (paddingX * 2);
        double slotWidth = availableWidth / displayLeds;
        double diodeWidth = Math.Max(3, Math.Min(slotWidth - 2, 14));

        // 2. Render each physical diode
        for (int i = 0; i < displayLeds; i++)
        {
            int bufferIndex = (int)((long)i * ledCount / displayLeds);
            bufferIndex = Math.Clamp(bufferIndex, 0, hasBuffer ? _cachedBuffer!.Length - 1 : 0);

            WledRgbwColor c = hasBuffer ? _cachedBuffer![bufferIndex] : WledRgbwColor.Black;
            double x = paddingX + (i * slotWidth) + ((slotWidth - diodeWidth) / 2);
            double y = paddingTop;

            DrawDiode(context, x, y, diodeWidth, diodeHeight, c);
        }

        // Draw segment demarcation dividers and labels if multiple segments exist
        if (_viewModel != null && _viewModel.WledSegments.Count > 1)
        {
            foreach (var seg in _viewModel.WledSegments)
            {
                if (seg.Start > 0 && seg.Start < ledCount)
                {
                    double divX = paddingX + ((double)seg.Start / ledCount) * availableWidth;
                    context.DrawLine(SegmentDividerPen, new Point(divX, paddingTop - 3), new Point(divX, paddingTop + diodeHeight + 3));
                }
            }
        }

        // 3. Render telemetry badges & rulers below the strip
        double labelY = height - 16;
        var labelBrush = new SolidColorBrush(Color.FromRgb(140, 145, 160));

        // Start & End markers
        var textLeft = new FormattedText("LED 1", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelTypeface, 9, labelBrush);
        context.DrawText(textLeft, new Point(paddingX, labelY));

        var textRight = new FormattedText($"LED {ledCount}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelTypeface, 9, labelBrush);
        context.DrawText(textRight, new Point(width - paddingX - textRight.Width, labelY));

        // Badges for Reverse / Offset / Segments
        double badgeX = width / 2;
        if (_cachedReversed)
        {
            var revBrush = new SolidColorBrush(Color.FromRgb(255, 170, 0));
            var revText = new FormattedText("◀ REVERSE DIRECTION", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelTypeface, 9, revBrush);
            context.DrawText(revText, new Point(badgeX - (revText.Width / 2) - 40, labelY));
        }

        if (_cachedOffset > 0)
        {
            var offsetBrush = new SolidColorBrush(Color.FromRgb(64, 196, 255));
            var offText = new FormattedText($"OFFSET: +{_cachedOffset}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelTypeface, 9, offsetBrush);
            context.DrawText(offText, new Point(badgeX + 30, labelY));
        }
        else if (_viewModel != null && _viewModel.WledSegments.Count > 1)
        {
            var segSummary = new FormattedText($"{_viewModel.WledSegments.Count} STRIPS DETECTED", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelTypeface, 8.5, SegmentTagBrush);
            context.DrawText(segSummary, new Point(badgeX - (segSummary.Width / 2), labelY));
        }
    }

    private static void DrawDiode(DrawingContext context, double x, double y, double diodeWidth, double diodeHeight, WledRgbwColor c)
    {
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
}
