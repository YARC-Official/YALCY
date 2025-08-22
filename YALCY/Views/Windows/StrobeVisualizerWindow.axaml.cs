using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using YALCY.Integrations.StageKit;
using YALCY.Udp;
using YALCY.Usb;

namespace YALCY.Views.Windows;

public partial class StrobeVisualizerWindow : Window
{
    private CancellationTokenSource cts = new();
    private Task strobeTask = Task.CompletedTask;
    private IBrush _strobeColor = Brushes.White;
    private IBrush _strobeOffColor = new SolidColorBrush(Color.Parse("#1a1d2a"));
    private bool _darkMode = false;

    enum StrobeSpeed
    {
        Off,
        Slow,
        Medium,
        Fast,
        Fastest
    }

    public StrobeVisualizerWindow()
    {
        InitializeComponent();
        UsbDeviceMonitor.OnStageKitCommand += OnStageKitEvent;
        
        // Initialize display
        UpdateDisplay(false, StrobeSpeed.Off, 0);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Enable window dragging when clicking on the title bar
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.BeginMoveDrag(e);
        }
    }
    private void OnStageKitEvent(StageKitTalker.CommandId commandId, byte parameter)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (commandId)
            {
                case StageKitTalker.CommandId.StrobeOff:
                    StopStrobeEffect();
                    break;

                case StageKitTalker.CommandId.StrobeSlow:
                    StartStrobeEffect(1);
                    break;

                case StageKitTalker.CommandId.StrobeMedium:
                    StartStrobeEffect(2);
                    break;

                case StageKitTalker.CommandId.StrobeFast:
                    StartStrobeEffect(3);
                    break;

                case StageKitTalker.CommandId.StrobeFastest:
                    StartStrobeEffect(4);
                    break;

                case StageKitTalker.CommandId.DisableAll:
                    StopStrobeEffect();
                    break;
            }
        });
    }

    private void StartStrobeEffect(int speed)
    {   
        float bpm = UdpIntake.BeatsPerMinute.Value;
        int interval = CalculateDelay(speed, bpm);
        
        cts = new CancellationTokenSource();
        
        strobeTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                // Turn strobe ON
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    this.StrobeCanvas.Background = _strobeColor;
                });
                
                await Task.Delay(interval, cts.Token);
                
                // Turn strobe OFF
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    this.StrobeCanvas.Background = _strobeOffColor;
                });
                
                await Task.Delay(interval, cts.Token);
            }
        }, cts.Token);

        UpdateDisplay(true, (StrobeSpeed)speed, (int)bpm);
    }

    private void StopStrobeEffect()
    {
        cts.Cancel();
        
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            this.StrobeCanvas.Background = _strobeOffColor;
            UpdateDisplay(false, StrobeSpeed.Off, 0);
        });
    }

    private void UpdateDisplay(bool isActive, StrobeSpeed speed, int bpm)
    {
        if (StatusText != null)
        {
            StatusText.Text = isActive ? "On" : "Off";
        }
        
        if (SpeedText != null)
        {
            if (isActive && speed != StrobeSpeed.Off)
            {
                SpeedText.Text = $"{speed} @{bpm}";
            }
        }
    }

    private int CalculateDelay(int speed, float bpm)
    {
        int noteValue = speed switch
        {
            1 => 16,  // Slow (16th note)
            2 => 24,  // Medium (24th note)
            3 => 32,  // Fast (32nd note)
            4 => 64,  // Fastest (64th note)
            _ => 16   // Default to slow
        };

        return (int)(60000.0 / bpm * 4 / noteValue);
    }

    protected override void OnClosed(EventArgs e)
    {
        UsbDeviceMonitor.OnStageKitCommand -= OnStageKitEvent;
        cts.Cancel();
        cts.Dispose();
        base.OnClosed(e);
    }

    #region Windows management (pinning, dragging, etc.)

    private void OnPinButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.Topmost = !this.Topmost;
        
        if (PinButton != null)
        {
            var pathIcon = new PathIcon();
            pathIcon.Width = 15;
            pathIcon.Height = 15;
            
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

    private void OnDarkButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this._darkMode = !this._darkMode;
        var pathIcon = new PathIcon();
        pathIcon.Width = 15;
        pathIcon.Height = 15;

        if (this._darkMode)
        {
            pathIcon.Data = Geometry.Parse("M320 64C178.6 64 64 178.6 64 320C64 461.4 178.6 576 320 576C388.8 576 451.3 548.8 497.3 504.6C504.6 497.6 506.7 486.7 502.6 477.5C498.5 468.3 488.9 462.6 478.8 463.4C473.9 463.8 469 464 464 464C362.4 464 280 381.6 280 280C280 207.9 321.5 145.4 382.1 115.2C391.2 110.7 396.4 100.9 395.2 90.8C394 80.7 386.6 72.5 376.7 70.3C358.4 66.2 339.4 64 320 64z");

            _strobeOffColor = new SolidColorBrush(Color.Parse("#000000"));
            this.Background = _strobeOffColor;
            StrobeCanvas.Background = _strobeOffColor;
            MainContent.Padding = new Thickness(0);
        }
        else
        {
            pathIcon.Data = Geometry.Parse("M303.3 112.7C196.2 121.2 112 210.8 112 320C112 434.9 205.1 528 320 528C353.3 528 384.7 520.2 412.6 506.3C309.2 482.9 232 390.5 232 280C232 214.2 259.4 154.9 303.3 112.7zM64 320C64 178.6 178.6 64 320 64C339.4 64 358.4 66.2 376.7 70.3C386.6 72.5 394 80.8 395.2 90.8C396.4 100.8 391.2 110.6 382.1 115.2C321.5 145.4 280 207.9 280 280C280 381.6 362.4 464 464 464C469 464 473.9 463.8 478.8 463.4C488.9 462.6 498.4 468.2 502.6 477.5C506.8 486.8 504.6 497.6 497.3 504.6C451.3 548.8 388.8 576 320 576C178.6 576 64 461.4 64 320z");

            _strobeOffColor = new SolidColorBrush(Color.Parse("#1a1d2a"));
            this.Background = new SolidColorBrush(Color.Parse("#0f111a"));
            StrobeCanvas.Background = _strobeOffColor;
            MainContent.Padding = new Thickness(10);
        }

        DarkButton.Content = pathIcon;
    }

    #endregion
}
