using YALCY.Diagnostics;
using System;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
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
        AccentHelper.UpdateApplicationAccentColors(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AccentHelper.UpdateApplicationAccentColors(this);

        if (PlatformSettings != null)
        {
            PlatformSettings.ColorValuesChanged += (s, e) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    AccentHelper.UpdateApplicationAccentColors(this);
                });
            };
        }

        ActualThemeVariantChanged += (s, e) =>
        {
            AccentHelper.UpdateApplicationAccentColors(this);
        };

        SettingsManager.LoadSettings();
        SetThemeVariant(SettingsManager.ThemeVariant);
        MainViewModel = new MainWindowViewModel();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = new MainWindow
            {
                DataContext = MainViewModel
            };
            //Don't move this. With how Avalonia works, this must be here or else there will be duplicate errors for things like the udp ports
            InitializeComponents();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void SetThemeVariant(string themeVariant)
    {
        RequestedThemeVariant = SettingsManager.NormalizeThemeVariant(themeVariant) switch
        {
            SettingsManager.LightThemeVariant => ThemeVariant.Light,
            SettingsManager.DarkThemeVariant => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
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

        // Each integration is started on its own: if one of them fails (e.g. an sACN adapter that can't be bound),
        // the others - above all the UDP intake from YARG - must still come up.
        TryStart("USB monitor", () => mainViewModel.UsbDeviceMonitor.StartUsbDeviceMonitor(mainViewModel));
        TryStart("DMX", () => mainViewModel.DmxTalker.EnableDmxTalker(mainViewModel.DmxEnabledSetting.IsEnabled, mainViewModel));
        TryStart("Serial", () => mainViewModel.SerialTalker.EnableSerialTalker(mainViewModel.SerialEnabledSetting.IsEnabled, mainViewModel));
        TryStart("RB3E", () => mainViewModel.Rb3ETalker.EnableRb3eTalker(mainViewModel.Rb3eEnabledSetting.IsEnabled, mainViewModel));
        TryStart("StageKit", () => mainViewModel.StageKitTalker.EnableStageKitTalker(mainViewModel.StageKitEnabledSetting.IsEnabled));

        _ = InitializeAsync(mainViewModel);
    }

    private static void TryStart(string name, Action start)
    {
        try
        {
            start();
        }
        catch (Exception ex)
        {
            AppLog.Write(LogLevel.Error, "Application", $"Error initializing {name}: {ex.Message}");
        }
    }

    private static async Task InitializeAsync(MainWindowViewModel mainViewModel)
    {
        // Start listening to YARG first and don't wait for it: the receive loop only returns when UDP is switched off.
        _ = StartUdpIntakeAsync(mainViewModel);

        try
        {
            await mainViewModel.HueTalker.EnableHue(mainViewModel.HueEnabledSetting.IsEnabled, mainViewModel.HueBridgeIp, mainViewModel);
        }
        catch (Exception ex)
        {
            AppLog.Write(LogLevel.Error, "Application", $"Error initializing Hue: {ex.Message}");
        }

        try
        {
            await mainViewModel.LifxTalker.EnableLifxLan(mainViewModel.LifxEnabledSetting.IsEnabled, mainViewModel);
        }
        catch (Exception ex)
        {
            AppLog.Write(LogLevel.Error, "Application", $"Error initializing LIFX: {ex.Message}");
        }

        try
        {
            await mainViewModel.HomeAssistantTalker.EnableHomeAssistant(
                mainViewModel.HomeAssistantEnabledSetting.IsEnabled,
                mainViewModel);
        }
        catch (Exception ex)
        {
            AppLog.Write(LogLevel.Error, "Application", $"Error initializing Home Assistant: {ex.Message}");
        }

        try
        {
            await mainViewModel.OpenRgbTalker.EnableOpenRgbTalker(mainViewModel.OpenRgbEnabledSetting.IsEnabled,
                mainViewModel.OpenRgbServerIp ?? string.Empty, mainViewModel.OpenRgbServerPort, mainViewModel);
        }
        catch (Exception ex)
        {
            AppLog.Write(LogLevel.Error, "Application", $"Error initializing OpenRGB: {ex.Message}");
        }

    }

    private static async Task StartUdpIntakeAsync(MainWindowViewModel mainViewModel)
    {
        try
        {
            await mainViewModel.UdpIntake.EnableUdpIntake(mainViewModel.UdpEnableSetting.IsEnabled, mainViewModel);
        }
        catch (Exception ex)
        {
            AppLog.Write(LogLevel.Error, "Application", $"Error initializing UDP: {ex.Message}");
        }
    }
}