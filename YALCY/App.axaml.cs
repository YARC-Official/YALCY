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

        mainViewModel.UsbDeviceMonitor.StartUsbDeviceMonitor(mainViewModel);
        mainViewModel.DmxTalker.EnableDmxTalker(mainViewModel.DmxEnabledSetting.IsEnabled, mainViewModel);
        mainViewModel.SerialTalker.EnableSerialTalker(mainViewModel.SerialEnabledSetting.IsEnabled, mainViewModel);
        mainViewModel.Rb3ETalker.EnableRb3eTalker(mainViewModel.Rb3eEnabledSetting.IsEnabled, mainViewModel);
        mainViewModel.StageKitTalker.EnableStageKitTalker(mainViewModel.StageKitEnabledSetting.IsEnabled);

        _ = InitializeAsync(mainViewModel);
    }

    private static async Task InitializeAsync(MainWindowViewModel mainViewModel)
    {
        try
        {
            await mainViewModel.HueTalker.EnableHue(mainViewModel.HueEnabledSetting.IsEnabled, mainViewModel.HueBridgeIp, mainViewModel);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing Hue: {ex.Message}");
        }

        try
        {
            await mainViewModel.LifxTalker.EnableLifxLan(mainViewModel.LifxEnabledSetting.IsEnabled, mainViewModel);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing LIFX: {ex.Message}");
        }

        try
        {
            await mainViewModel.HomeAssistantTalker.EnableHomeAssistant(
                mainViewModel.HomeAssistantEnabledSetting.IsEnabled,
                mainViewModel);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing Home Assistant: {ex.Message}");
        }

        try
        {
            await mainViewModel.OpenRgbTalker.EnableOpenRgbTalker(mainViewModel.OpenRgbEnabledSetting.IsEnabled,
                mainViewModel.OpenRgbServerIp ?? string.Empty, mainViewModel.OpenRgbServerPort, mainViewModel);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing OpenRGB: {ex.Message}");
        }

        try
        {
            await mainViewModel.UdpIntake.EnableUdpIntake(mainViewModel.UdpEnableSetting.IsEnabled, mainViewModel);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing UDP: {ex.Message}");
        }
    }
}