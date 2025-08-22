using Avalonia.Controls;
using YALCY.Views.Tabs;

namespace YALCY.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Close all detached windows when main window is closing
        this.Closing += OnMainWindowClosing;
    }
    
    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // Close all detached windows first
        YargTabView.CloseAllDetachedWindows();
    }
}
