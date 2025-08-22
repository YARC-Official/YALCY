using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using YALCY.Views.Components;

namespace YALCY.Views.Windows;

public partial class LedVisualizerWindow : Window
{
    public LedVisualizerWindow()
    {
        InitializeComponent();
    }

    public void SetVisualizerContent(LedVisualizerPanel content)
    {
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Enable window dragging when clicking on the title bar
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.BeginMoveDrag(e);
        }
    }

    private void OnPinButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Toggle the "Always on Top" state
        this.Topmost = !this.Topmost;
        
        // Update button appearance to show current state
        if (PinButton != null)
        {
            var pathIcon = new PathIcon();
            pathIcon.Width = 15;
            pathIcon.Height = 15;
            
            if (this.Topmost)
            {
                // Active state - pin is active (crossed out pin icon)
                pathIcon.Data = Geometry.Parse("M73 39.1C63.6 29.7 48.4 29.7 39.1 39.1C29.8 48.5 29.7 63.7 39 73.1L567 601.1C576.4 610.5 591.6 610.5 600.9 601.1C610.2 591.7 610.3 576.5 600.9 567.2L449.8 416L480 416C490 416 499.5 411.3 505.5 403.3C511.5 395.3 513.5 384.9 510.7 375.2L507 361.8C494.6 318.5 466 283.3 428.8 262.1L418.5 128L448 128C465.7 128 480 113.7 480 96C480 78.3 465.7 64 448 64L192 64C184.6 64 177.9 66.5 172.5 70.6L222.1 120.3L217.3 183.4L73 39.1zM314.2 416L181.7 283.6C159 304.1 141.9 331 133 361.9L129.2 375.3C126.4 385 128.4 395.3 134.4 403.4C140.4 411.5 150 416 160 416L314.2 416zM288 576C288 593.7 302.3 608 320 608C337.7 608 352 593.7 352 576L352 464L288 464L288 576z");
            }
            else
            {
                // Inactive state - pin is not active (filled pin icon)
                pathIcon.Data = Geometry.Parse("M160 96C160 78.3 174.3 64 192 64L448 64C465.7 64 480 78.3 480 96C480 113.7 465.7 128 448 128L418.5 128L428.8 262.1C465.9 283.3 494.6 318.5 507 361.8L510.8 375.2C513.6 384.9 511.6 395.2 505.6 403.3C499.6 411.4 490 416 480 416L160 416C150 416 140.5 411.3 134.5 403.3C128.5 395.3 126.5 384.9 129.3 375.2L133 361.8C145.4 318.5 174 283.3 211.2 262.1L221.5 128L192 128C174.3 128 160 113.7 160 96zM288 464L352 464L352 576C352 593.7 337.7 608 320 608C302.3 608 288 593.7 288 576L288 464z");
            }
            PinButton.Content = pathIcon;
        }
    }
}
