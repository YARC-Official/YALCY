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
/// Minimalist, hardware-grade real-time LED strip visualizer control for WLED.
/// Renders direct RGBW diode outputs, segment dividers, and optional stacked multi-strip lanes.
/// </summary>
public sealed class WledStripVisualizer : Control
{
    private WledRgbwColor[]? _cachedBuffer;
    private int _cachedLedCount = 32;
    private bool _cachedReversed;
    private int _cachedOffset;
    private long _lastRenderTimestamp;
    private MainWindowViewModel? _viewModel;

    // Minimalist hardware track and diode styling
    private static readonly IBrush TrackBackgroundBrush = new SolidColorBrush(Color.FromRgb(14, 16, 22));
    private static readonly IPen TrackBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(32, 37, 48)), 1);
    private static readonly IBrush OffDiodeBrush = new SolidColorBrush(Color.FromRgb(22, 26, 34));
    private static readonly IPen OffDiodePen = new Pen(new SolidColorBrush(Color.FromRgb(36, 42, 56)), 1);
    private static readonly IBrush WhiteCoreBrush = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255));
    private static readonly Typeface LabelTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

    // Segment styling
    private static readonly IPen SegmentDividerPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 59, 130, 246)), 1.5, DashStyle.Dash);
    private static readonly IBrush SegmentTagBrush = new SolidColorBrush(Color.FromRgb(96, 165, 250));

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
        if (width <= 10 || height <= 6) return;

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

        double laneSpacing = 4;
        double totalLaneHeight = height - ((count - 1) * laneSpacing);
        double laneHeight = Math.Max(18, totalLaneHeight / count);

        for (int s = 0; s < count; s++)
        {
            var seg = segments[s];
            double laneY = s * (laneHeight + laneSpacing);
            var laneRect = new RoundedRect(new Rect(0, laneY, width, laneHeight), 4);
            context.DrawRectangle(TrackBackgroundBrush, TrackBorderPen, laneRect);

            // Minimalist lane label
            string segTitle = string.IsNullOrWhiteSpace(seg.Name) ? $"S{seg.Id + 1}" : seg.Name;
            var textBadge = new FormattedText(
                $"{segTitle} ({seg.Length})",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                LabelTypeface,
                8,
                SegmentTagBrush);
            context.DrawText(textBadge, new Point(6, laneY + (laneHeight - textBadge.Height) / 2));

            // Diodes layout
            int segLength = Math.Max(1, seg.Length);
            int displayDiodes = Math.Min(segLength, 48);
            double labelWidth = Math.Max(50, textBadge.Width + 12);
            double diodeAreaLeft = labelWidth;
            double diodeAreaWidth = width - diodeAreaLeft - 6;
            double slotWidth = diodeAreaWidth / displayDiodes;
            double diodeHeight = Math.Max(6, laneHeight - 6);
            double diodeWidth = Math.Max(2.5, Math.Min(slotWidth - 1.5, 12));
            double dy = laneY + 3;

            for (int d = 0; d < displayDiodes; d++)
            {
                int bufferIndex = seg.Start + (int)((long)d * segLength / displayDiodes);
                bufferIndex = Math.Clamp(bufferIndex, 0, hasBuffer ? _cachedBuffer!.Length - 1 : 0);

                WledRgbwColor c = hasBuffer ? _cachedBuffer![bufferIndex] : WledRgbwColor.Black;
                double dx = diodeAreaLeft + (d * slotWidth) + ((slotWidth - diodeWidth) / 2);

                DrawDiode(context, dx, dy, diodeWidth, diodeHeight, c);
            }
        }
    }

    private void RenderContinuousStrip(DrawingContext context, double width, double height, bool hasBuffer)
    {
        int ledCount = Math.Max(1, _cachedLedCount);
        int displayLeds = Math.Min(ledCount, 64); // Display up to 64 diode cells horizontally

        // Sleek recessed hardware track
        var trackRect = new RoundedRect(new Rect(0, 0, width, height), 4);
        context.DrawRectangle(TrackBackgroundBrush, TrackBorderPen, trackRect);

        double paddingX = 6;
        double paddingTop = 3;
        double diodeHeight = Math.Max(6, height - 6);
        double availableWidth = width - (paddingX * 2);
        double slotWidth = availableWidth / displayLeds;
        double diodeWidth = Math.Max(2.5, Math.Min(slotWidth - 1.5, 12));

        // Render each physical diode
        for (int i = 0; i < displayLeds; i++)
        {
            int bufferIndex = (int)((long)i * ledCount / displayLeds);
            bufferIndex = Math.Clamp(bufferIndex, 0, hasBuffer ? _cachedBuffer!.Length - 1 : 0);

            WledRgbwColor c = hasBuffer ? _cachedBuffer![bufferIndex] : WledRgbwColor.Black;
            double x = paddingX + (i * slotWidth) + ((slotWidth - diodeWidth) / 2);
            double y = paddingTop;

            DrawDiode(context, x, y, diodeWidth, diodeHeight, c);
        }

        // Draw segment demarcation dividers if multiple segments exist
        if (_viewModel != null && _viewModel.WledSegments.Count > 1)
        {
            foreach (var seg in _viewModel.WledSegments)
            {
                if (seg.Start > 0 && seg.Start < ledCount)
                {
                    double divX = paddingX + ((double)seg.Start / ledCount) * availableWidth;
                    context.DrawLine(SegmentDividerPen, new Point(divX, paddingTop), new Point(divX, paddingTop + diodeHeight));
                }
            }
        }
    }

    private static void DrawDiode(DrawingContext context, double x, double y, double diodeWidth, double diodeHeight, WledRgbwColor c)
    {
        var diodeRect = new Rect(x, y, diodeWidth, diodeHeight);
        var roundedDiode = new RoundedRect(diodeRect, 2);
        bool isLit = c.R > 2 || c.G > 2 || c.B > 2 || c.W > 2;

        if (isLit)
        {
            // Glow aura
            byte alphaGlow = (byte)Math.Min(130, Math.Max(c.R, Math.Max(c.G, Math.Max(c.B, c.W))) / 2);
            var glowBrush = new SolidColorBrush(Color.FromArgb(alphaGlow, c.R, c.G, c.B));
            context.DrawRectangle(glowBrush, null, new RoundedRect(new Rect(x - 1.5, y - 1.5, diodeWidth + 3, diodeHeight + 3), 3));

            // Diode body
            var diodeBrush = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
            context.DrawRectangle(diodeBrush, null, roundedDiode);

            // Dedicated 4th White (W) Diode Core
            if (c.W > 10)
            {
                double coreSize = Math.Max(2, diodeWidth * 0.4);
                double coreX = x + (diodeWidth - coreSize) / 2;
                double coreY = y + (diodeHeight - coreSize) / 2;
                context.DrawRectangle(WhiteCoreBrush, null, new RoundedRect(new Rect(coreX, coreY, coreSize, coreSize), 1.5));
            }
        }
        else
        {
            // Sleek unlit diode socket
            context.DrawRectangle(OffDiodeBrush, OffDiodePen, roundedDiode);
        }
    }
}
