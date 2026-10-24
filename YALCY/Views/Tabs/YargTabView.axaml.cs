using YALCY.Lighting;
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

namespace YALCY.Views.Tabs;

public partial class YargTabView : UserControl
{
    private static LedVisualizerWindow? _allDetachedWindow;
    private static StrobeVisualizerWindow? _allStrobeWindow;
    private static bool _isAppClosing;
    
    private LedVisualizerWindow? _detachedWindow;
    private StrobeVisualizerWindow? _strobeVisualizerWindow;
    private CancellationTokenSource? _testAnimationCts;
    
    public static void CloseAllDetachedWindows()
    {
        _isAppClosing = true;

        if (_allDetachedWindow != null)
        {
            var win = _allDetachedWindow;
            _allDetachedWindow = null;
            try
            {
                win.Close();
            }
            catch (Exception) { }
        }
        
        if (_allStrobeWindow != null)
        {
            var win = _allStrobeWindow;
            _allStrobeWindow = null;
            try
            {
                win.Close();
            }
            catch (Exception) { }
        }

        StageKitStatusWindow.CloseActiveInstance();
        _isAppClosing = false;
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
        LightingOutput.Preview(LightingCommand.BlueLeds, 0xFF);
        LightingOutput.Preview(LightingCommand.GreenLeds, 0xFF);
        LightingOutput.Preview(LightingCommand.YellowLeds, 0xFF);
        LightingOutput.Preview(LightingCommand.RedLeds, 0xFF);
    }

    private void OnClearLedsClicked(object? sender, RoutedEventArgs e)
    {
        _testAnimationCts?.Cancel();
        LightingOutput.Preview(LightingCommand.DisableAll, 0x00);
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

                    LightingOutput.Preview(LightingCommand.BlueLeds, b);
                    LightingOutput.Preview(LightingCommand.GreenLeds, g);
                    LightingOutput.Preview(LightingCommand.YellowLeds, y);
                    LightingOutput.Preview(LightingCommand.RedLeds, r);

                    await Task.Delay(85, ct);
                }
            }

            if (!ct.IsCancellationRequested)
            {
                LightingOutput.Preview(LightingCommand.DisableAll, 0x00);
            }
        }
        catch (TaskCanceledException) { }
    }

    public static void OpenLedVisualizer(MainWindowViewModel? vm = null)
    {
        if (_allDetachedWindow != null)
        {
            if (!_allDetachedWindow.IsVisible)
            {
                _allDetachedWindow.Show();
            }
            if (_allDetachedWindow.WindowState == WindowState.Minimized)
            {
                _allDetachedWindow.WindowState = WindowState.Normal;
            }
            _allDetachedWindow.Activate();
            SingleInstanceManager.BringWindowToForeground(_allDetachedWindow);
            return;
        }

        vm ??= (Application.Current as App)?.MainViewModel;
        if (vm != null)
        {
            vm.IsVisualizerDetached = true;
        }

        var window = new LedVisualizerWindow
        {
            DataContext = vm
        };
        _allDetachedWindow = window;

        window.Closed += (s, args) =>
        {
            _allDetachedWindow = null;
            if (!_isAppClosing && vm != null)
            {
                vm.IsVisualizerDetached = false;
            }
        };

        window.Show();
        SingleInstanceManager.BringWindowToForeground(window);
    }

    public static void CloseLedVisualizer()
    {
        if (_allDetachedWindow != null)
        {
            var win = _allDetachedWindow;
            _allDetachedWindow = null;
            try { win.Close(); } catch (Exception) { }
        }
    }

    public static void OpenStrobeVisualizer()
    {
        if (_allStrobeWindow != null)
        {
            if (!_allStrobeWindow.IsVisible)
            {
                _allStrobeWindow.Show();
            }
            if (_allStrobeWindow.WindowState == WindowState.Minimized)
            {
                _allStrobeWindow.WindowState = WindowState.Normal;
            }
            _allStrobeWindow.Activate();
            SingleInstanceManager.BringWindowToForeground(_allStrobeWindow);
            return;
        }

        var window = new StrobeVisualizerWindow();
        _allStrobeWindow = window;

        window.Closed += (s, args) =>
        {
            _allStrobeWindow = null;
        };

        window.Show();
        SingleInstanceManager.BringWindowToForeground(window);
    }

    public static void CloseStrobeVisualizer()
    {
        if (_allStrobeWindow != null)
        {
            var win = _allStrobeWindow;
            _allStrobeWindow = null;
            try { win.Close(); } catch (Exception) { }
        }
    }

    public static void OpenStageKitStatus()
    {
        var window = StageKitStatusWindow.ActiveInstance;
        if (window != null)
        {
            if (!window.IsVisible)
            {
                window.Show();
            }
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }
            window.Activate();
            SingleInstanceManager.BringWindowToForeground(window);
            return;
        }

        window = new StageKitStatusWindow();
        window.Show();
        SingleInstanceManager.BringWindowToForeground(window);
    }

    private void OnDetachVisualizerClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var toggleButton = sender as Avalonia.Controls.Primitives.ToggleButton;
        if (toggleButton?.IsChecked == false)
        {
            CloseLedVisualizer();
            _detachedWindow = null;
        }
        else
        {
            OpenLedVisualizer(this.DataContext as MainWindowViewModel);
            _detachedWindow = _allDetachedWindow;
            if (_detachedWindow != null && toggleButton != null)
            {
                _detachedWindow.Closed += (s, args) =>
                {
                    toggleButton.IsChecked = false;
                };
            }
        }
    }
    
    private void OnStrobeVisualizerClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var toggleButton = sender as Avalonia.Controls.Primitives.ToggleButton;
        if (toggleButton?.IsChecked == false)
        {
            CloseStrobeVisualizer();
            _strobeVisualizerWindow = null;
        }
        else
        {
            OpenStrobeVisualizer();
            _strobeVisualizerWindow = _allStrobeWindow;
            if (_strobeVisualizerWindow != null && toggleButton != null)
            {
                _strobeVisualizerWindow.Closed += (s, args) =>
                {
                    toggleButton.IsChecked = false;
                };
            }
        }
    }
}
