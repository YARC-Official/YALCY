using Avalonia.Controls;
using YALCY.Views.Tabs;
using YALCY.ViewModels;

namespace YALCY.Views;

public partial class MainWindow : Window
{
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        
        // Close all detached windows when main window is closing
        this.Closing += OnMainWindowClosing;
    }

    public void RequestExit()
    {
        _allowClose = true;
        Close();
    }

    public void HideToTray()
    {
        YargTabView.CloseAllDetachedWindows();
        Hide();
    }
    
    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose)
        {
            YargTabView.CloseAllDetachedWindows();
            return;
        }

        if (DataContext is MainWindowViewModel viewModel && viewModel.CloseToTrayOnClose)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        // Close all detached windows first
        YargTabView.CloseAllDetachedWindows();
    }
}
