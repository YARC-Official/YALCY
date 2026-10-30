using YALCY.Diagnostics;
using Avalonia.Controls;
using YALCY.Views.Tabs;
using YALCY.ViewModels;

namespace YALCY.Views;

public partial class MainWindow : Window
{
    private bool _allowClose;
    private bool _shutdownPending;
    private bool _shutdownComplete;
    private readonly Avalonia.Threading.DispatcherTimer _saveStatusTimer = new()
    {
        Interval = System.TimeSpan.FromMilliseconds(500)
    };

    public MainWindow()
    {
        InitializeComponent();
        _saveStatusTimer.Tick += (_, _) =>
        {
            if (IsVisible && DataContext is MainWindowViewModel model) model.RefreshSettingsSaveStatus();
        };
        Opened += (_, _) => _saveStatusTimer.Start();
        Closed += (_, _) => _saveStatusTimer.Stop();

        // Close all detached windows when main window is closing
        this.Closing += OnMainWindowClosing;

        // Keep ViewModel WindowWidth synchronized for responsive layouts
        this.SizeChanged += (sender, e) =>
        {
            if (DataContext is MainWindowViewModel viewModel && e.NewSize.Width > 0)
            {
                viewModel.WindowWidth = e.NewSize.Width;
            }
        };

        this.Opened += (_, _) => SingleInstanceManager.SetWindowAppId(this, "YALCY.MainWindow");
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

    private async void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_shutdownComplete)
        {
            YargTabView.CloseAllDetachedWindows();
            return;
        }

        if (!_allowClose && !_shutdownPending &&
            DataContext is MainWindowViewModel { CloseToTrayOnClose: true, IsShutdownComplete: false })
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        e.Cancel = true;
        if (_shutdownPending) return;
        _shutdownPending = true;
        try
        {
            if (DataContext is MainWindowViewModel viewModel)
                await viewModel.ShutdownAsync();
        }
        catch (System.Exception ex)
        {
            AppLog.Write(LogLevel.Error, "Application", $"Error shutting down: {ex.Message}");
        }
        finally
        {
            _shutdownComplete = true;
            // Let the canceled Closing event unwind before issuing the final close.
            Avalonia.Threading.Dispatcher.UIThread.Post(Close);
        }
    }
}
