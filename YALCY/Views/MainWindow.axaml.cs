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

        // Keep ViewModel WindowWidth synchronized for responsive layouts
        this.SizeChanged += (sender, e) =>
        {
            if (DataContext is MainWindowViewModel viewModel && e.NewSize.Width > 0)
            {
                viewModel.WindowWidth = e.NewSize.Width;
            }
        };
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
            SaveSettingsSafely();
            YargTabView.CloseAllDetachedWindows();
            return;
        }

        if (DataContext is MainWindowViewModel viewModel && viewModel.CloseToTrayOnClose)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        // Save now: closing the main window shuts the app down without raising ShutdownRequested,
        // so the save in MainWindowViewModel.ShutdownAsync is not reached on a normal close.
        SaveSettingsSafely();

        // Close all detached windows first
        YargTabView.CloseAllDetachedWindows();
    }

    private void SaveSettingsSafely()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        try
        {
            SettingsManager.SaveSettings(viewModel);
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"Error saving settings: {ex.Message}");
        }
    }
}