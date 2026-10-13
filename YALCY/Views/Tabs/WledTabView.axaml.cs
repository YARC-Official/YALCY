using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using YALCY.ViewModels;

namespace YALCY.Views.Tabs;

public partial class WledTabView : UserControl
{
    private bool? _lastIsCompact = null;
    private DispatcherTimer? _telemetryTimer;
    private long _lastPingTicks;

    public WledTabView()
    {
        InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is MainWindowViewModel vm)
        {
            vm.IsWledTabActive = true;
        }
        StartTelemetryTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (DataContext is MainWindowViewModel vm)
        {
            vm.IsWledTabActive = false;
        }
        StopTelemetryTimer();
    }

    private void StartTelemetryTimer()
    {
        if (_telemetryTimer == null)
        {
            _telemetryTimer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _telemetryTimer.Tick += OnTelemetryTimerTick;
        }

        _telemetryTimer.Start();
        OnTelemetryTimerTick(null, EventArgs.Empty);
    }

    private void StopTelemetryTimer()
    {
        _telemetryTimer?.Stop();
    }

    private async void OnTelemetryTimerTick(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.RefreshWledTelemetry();

            long now = Environment.TickCount64;
            if (vm.IsWledConnected && (now - _lastPingTicks >= 1500))
            {
                _lastPingTicks = now;
                await vm.MeasureWledLatencyAsync();
            }
            else if (!vm.IsWledConnected)
            {
                vm.ResetWledLatency();
            }
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.IsCompactMode) ||
                    args.PropertyName == nameof(MainWindowViewModel.IsNarrowMode))
                {
                    ApplyLayoutState(vm.IsCompactMode || vm.IsNarrowMode);
                }
            };
            ApplyLayoutState(vm.IsCompactMode || vm.IsNarrowMode);
        }
    }

    private void ApplyLayoutState(bool isCompact)
    {
        if (_lastIsCompact == isCompact) return;
        _lastIsCompact = isCompact;

        var contentGrid = this.FindControl<Grid>("ContentGrid");
        var leftColumn = this.FindControl<StackPanel>("LeftColumnPanel");
        var rightColumn = this.FindControl<StackPanel>("RightColumnPanel");

        if (contentGrid == null || leftColumn == null || rightColumn == null) return;

        if (isCompact)
        {
            // Switch to 1-column, 2-row vertical stack so content never overflows in narrow/compact windows
            contentGrid.ColumnDefinitions.Clear();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

            contentGrid.RowDefinitions.Clear();
            contentGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            contentGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Grid.SetColumn(leftColumn, 0);
            Grid.SetRow(leftColumn, 0);
            leftColumn.Margin = new Thickness(0, 0, 0, 16);

            Grid.SetColumn(rightColumn, 0);
            Grid.SetRow(rightColumn, 1);
            rightColumn.Margin = new Thickness(0);
        }
        else
        {
            // Restore wide 2-column side-by-side layout
            contentGrid.RowDefinitions.Clear();
            contentGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            contentGrid.ColumnDefinitions.Clear();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

            Grid.SetRow(leftColumn, 0);
            Grid.SetColumn(leftColumn, 0);
            leftColumn.Margin = new Thickness(0, 0, 8, 0);

            Grid.SetRow(rightColumn, 0);
            Grid.SetColumn(rightColumn, 1);
            rightColumn.Margin = new Thickness(8, 0, 0, 0);
        }
    }
}
