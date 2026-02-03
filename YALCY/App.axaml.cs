using System;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using YALCY.ViewModels;
using YALCY.Views;

namespace YALCY;

public class App : Application
{
    public MainWindowViewModel MainViewModel { get; private set; }

    public static string Version
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                    return $"{version.Major}.{version.Minor}.{version.Build}";
            }
            return "1.0.0";
        }
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        SettingsManager.LoadSettings();
        MainViewModel = new MainWindowViewModel();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = MainViewModel
            };
            //Don't move this. With how Avalonia works, this must be here or else there will be duplicate errors for things like the udp ports
            InitializeComponents();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnTrayShowClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        if (desktop.MainWindow is not Window window)
        {
            return;
        }

        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }

    private void OnTrayHideClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        if (desktop.MainWindow is MainWindow mainWindow)
        {
            mainWindow.HideToTray();
        }
        else
        {
            desktop.MainWindow?.Hide();
        }
    }

    private void OnTrayExitClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        if (desktop.MainWindow is MainWindow mainWindow)
        {
            mainWindow.RequestExit();
            return;
        }

        desktop.Shutdown();
    }

    private void InitializeComponents()
    {
        var mainViewModel = MainViewModel;

        mainViewModel.UsbDeviceMonitor.StartUsbDeviceMonitor(mainViewModel);
        // Fire-and-forget async calls - intentionally not awaited during initialization
        _ = mainViewModel.HueTalker.EnableHue(mainViewModel.HueEnabledSetting.IsEnabled, mainViewModel.HueBridgeIp, mainViewModel);
        mainViewModel.DmxTalker.EnableDmxTalker(mainViewModel.DmxEnabledSetting.IsEnabled, mainViewModel);
        mainViewModel.SerialTalker.EnableSerialTalker(mainViewModel.SerialEnabledSetting.IsEnabled, mainViewModel);
        mainViewModel.Rb3ETalker.EnableRb3eTalker(mainViewModel.Rb3eEnabledSetting.IsEnabled);
        mainViewModel.StageKitTalker.EnableStageKitTalker(mainViewModel.StageKitEnabledSetting.IsEnabled);
        _ = mainViewModel.OpenRgbTalker.EnableOpenRgbTalker(mainViewModel.OpenRgbEnabledSetting.IsEnabled,
            mainViewModel.OpenRgbServerIp ?? string.Empty, mainViewModel.OpenRgbServerPort, mainViewModel);
        _ = mainViewModel.UdpIntake.EnableUdpIntake(mainViewModel.UdpEnableSetting.IsEnabled, mainViewModel);
    }
}
