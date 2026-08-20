using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Input;
using YALCY.Views.Components;
using YALCY.Views.Windows;
using Avalonia.Interactivity;
using YALCY.ViewModels;
using YALCY.Usb;
using YALCY.Integrations.StageKit;

namespace YALCY.Views.Tabs;

public partial class YargTabView : UserControl
{
    private static LedVisualizerWindow? _allDetachedWindow;
    private static StrobeVisualizerWindow? _allStrobeWindow;
    
    private LedVisualizerWindow? _detachedWindow;
    private StrobeVisualizerWindow? _strobeVisualizerWindow;
    private CancellationTokenSource? _testAnimationCts;
    
    public static void CloseAllDetachedWindows()
    {
        if (_allDetachedWindow != null)
        {
            try
            {
                _allDetachedWindow.Close();
            }

            catch (Exception) { }
            _allDetachedWindow = null;
        }
        
        if (_allStrobeWindow != null)
        {
            try
            {
                _allStrobeWindow.Close();
            }
            catch (Exception) { }
            _allStrobeWindow = null;
        }
    }

    private void OnDataGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Button)
        {
            e.Handled = true;
        }
    }

    public YargTabView()
    {
        InitializeComponent();
    }

    private void OnToggleHighlightClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is Udp.UdpIntake.IDatapacketMember member)
        {
            member.IsHighlighted = !member.IsHighlighted;
            if (DataContext is MainWindowViewModel vm)
            {
                vm.ReorderCombinedCollection();
            }
        }
    }

    private void OnResetUdpListenPortClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ResetUdpListenPort();
        }
    }

    private void OnTestAllLedsClicked(object? sender, RoutedEventArgs e)
    {
        _testAnimationCts?.Cancel();
        UsbDeviceMonitor.SimulateStageKitCommand(StageKitTalker.CommandId.BlueLeds, 0xFF);
        UsbDeviceMonitor.SimulateStageKitCommand(StageKitTalker.CommandId.GreenLeds, 0xFF);
        UsbDeviceMonitor.SimulateStageKitCommand(StageKitTalker.CommandId.YellowLeds, 0xFF);
        UsbDeviceMonitor.SimulateStageKitCommand(StageKitTalker.CommandId.RedLeds, 0xFF);
    }

    private void OnClearLedsClicked(object? sender, RoutedEventArgs e)
    {
        _testAnimationCts?.Cancel();
        UsbDeviceMonitor.SimulateStageKitCommand(StageKitTalker.CommandId.DisableAll, 0x00);
    }

    private async void OnTestRotateLedsClicked(object? sender, RoutedEventArgs e)
    {
        _testAnimationCts?.Cancel();
        _testAnimationCts = new CancellationTokenSource();
        var ct = _testAnimationCts.Token;

        try
        {
            for (int cycle = 0; cycle < 3 && !ct.IsCancellationRequested; cycle++)
            {
                for (int i = 0; i < 8 && !ct.IsCancellationRequested; i++)
                {
                    byte b = (byte)(1 << i);
                    byte g = (byte)(1 << ((i + 2) % 8));
                    byte y = (byte)(1 << ((i + 4) % 8));
                    byte r = (byte)(1 << ((i + 6) % 8));

                    UsbDeviceMonitor.SimulateStageKitCommand(StageKitTalker.CommandId.BlueLeds, b);
                    UsbDeviceMonitor.SimulateStageKitCommand(StageKitTalker.CommandId.GreenLeds, g);
                    UsbDeviceMonitor.SimulateStageKitCommand(StageKitTalker.CommandId.YellowLeds, y);
                    UsbDeviceMonitor.SimulateStageKitCommand(StageKitTalker.CommandId.RedLeds, r);

                    await Task.Delay(85, ct);
                }
            }

            if (!ct.IsCancellationRequested)
            {
                UsbDeviceMonitor.SimulateStageKitCommand(StageKitTalker.CommandId.DisableAll, 0x00);
            }
        }
        catch (TaskCanceledException) { }
    }

    private void OnDetachVisualizerClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var toggleButton = sender as Avalonia.Controls.Primitives.ToggleButton;
        if (toggleButton?.IsChecked == false)
        {
            // Close detached visualizer window and re-dock to YARG tab
            if (_detachedWindow != null)
            {
                try { _detachedWindow.Close(); } catch (Exception) { }
                _detachedWindow = null;
            }
            _allDetachedWindow = null;
            if (this.DataContext is YALCY.ViewModels.MainWindowViewModel vm)
            {
                vm.IsVisualizerDetached = false;
            }
        }
        else
        {
            // Detach visualizer into floating window
            if (_detachedWindow == null)
            {
                if (this.DataContext is YALCY.ViewModels.MainWindowViewModel vm)
                {
                    vm.IsVisualizerDetached = true;
                }

                _detachedWindow = new LedVisualizerWindow
                {
                    DataContext = this.DataContext
                };
                _allDetachedWindow = _detachedWindow;

                var mainWindow = this.VisualRoot as Window;

                _detachedWindow.Closed += (s, args) =>
                {
                    _detachedWindow = null;
                    _allDetachedWindow = null;
                    if (toggleButton != null)
                    {
                        toggleButton.IsChecked = false;
                    }
                    if (this.DataContext is YALCY.ViewModels.MainWindowViewModel closeVm)
                    {
                        closeVm.IsVisualizerDetached = false;
                    }
                };

                if (mainWindow != null)
                {
                    _detachedWindow.Show(mainWindow);
                }
                else
                {
                    _detachedWindow.Show();
                }
            }
        }
    }
    
    private void OnStrobeVisualizerClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var toggleButton = sender as Avalonia.Controls.Primitives.ToggleButton;
        if (toggleButton?.IsChecked == false)
        {
            // Close strobe visualizer
            if (_strobeVisualizerWindow != null)
            {
                try { _strobeVisualizerWindow.Close(); } catch (Exception) { }
                _strobeVisualizerWindow = null;
            }
            _allStrobeWindow = null;
        }
        else
        {
            // Open strobe visualizer
            if (_strobeVisualizerWindow == null)
            {
                _strobeVisualizerWindow = new StrobeVisualizerWindow();
                _allStrobeWindow = _strobeVisualizerWindow;

                var mainWindow = this.VisualRoot as Window;
                
                _strobeVisualizerWindow.Closed += (s, args) =>
                {
                    _allStrobeWindow = null;
                    _strobeVisualizerWindow = null;
                    if (toggleButton != null)
                    {
                        toggleButton.IsChecked = false;
                    }
                };
                
                if (mainWindow != null)
                {
                    _strobeVisualizerWindow.Show(mainWindow);
                }
                else
                {
                    _strobeVisualizerWindow.Show();
                }
            }
        }
    }
}
