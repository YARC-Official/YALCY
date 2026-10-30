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
using YALCY.Views.Windows;

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

            bool startHidden = SettingsManager.SetupCompleted && CommandLineHandler.ShouldStartHidden(Program.StartupArgs);
            if (startHidden)
            {
                desktop.MainWindow.WindowState = WindowState.Minimized;
                desktop.MainWindow.Opened += (s, e) =>
                {
                    desktop.MainWindow?.Hide();
                };
            }

            //Don't move this. With how Avalonia works, this must be here or else there will be duplicate errors for things like the udp ports
            InitializeComponents();

            if (!SettingsManager.SetupCompleted)
            {
                var setupShown = false;
                desktop.MainWindow.Opened += async (_, _) =>
                {
                    if (setupShown) return;
                    setupShown = true;
                    try
                    {
                        await MainViewModel.InitializationTask;
                        if (MainViewModel.IsShutdownComplete || !desktop.MainWindow.IsVisible) return;
                        var setup = new FirstRunSetupWindow
                        {
                            DataContext = new FirstRunSetupViewModel(MainViewModel)
                        };
                        await setup.ShowDialog(desktop.MainWindow);
                    }
                    catch (Exception ex)
                    {
                        AppLog.Write(LogLevel.Error, "Application", $"Could not show first-run setup: {ex.Message}");
                    }
                };
            }

            if (Program.StartupArgs != null && Program.StartupArgs.Length > 0)
            {
                CommandLineHandler.ExecuteArguments(Program.StartupArgs, allowHide: SettingsManager.SetupCompleted);
            }
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

    public static void ShowMainWindow()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
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
            SingleInstanceManager.BringWindowToForeground(window);
        });
    }

    public static void HideMainWindowToTray()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
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
        });
    }

    public static void RequestExit()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return;
            }

            if (desktop.MainWindow is MainWindow mainWindow)
            {
                mainWindow.RequestExit();
                return;
            }

            desktop.Shutdown();
        });
    }

    private void OnTrayShowClick(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void OnTrayHideClick(object? sender, EventArgs e)
    {
        HideMainWindowToTray();
    }

    private void OnTrayExitClick(object? sender, EventArgs e)
    {
        RequestExit();
    }

    private void InitializeComponents()
    {
        var mainViewModel = MainViewModel;

        // Each integration is started on its own: if one of them fails (e.g. an sACN adapter that can't be bound),
        // the others - above all the UDP intake from YARG - must still come up.
        TryStart("USB monitor", () => mainViewModel.UsbDeviceMonitor.StartUsbDeviceMonitor(mainViewModel));
        TryStart("DMX", () => mainViewModel.DmxTalker.EnableDmxTalker(mainViewModel.DmxEnabledSetting.IsEnabled, mainViewModel));
        TryStart("Art-Net", () => mainViewModel.ArtNetTalker.EnableDmxTalker(mainViewModel.ArtNetEnabledSetting.IsEnabled, mainViewModel));
        TryStart("Serial", () => mainViewModel.SerialTalker.EnableSerialTalker(mainViewModel.SerialEnabledSetting.IsEnabled, mainViewModel));
        TryStart("RB3E", () => mainViewModel.Rb3ETalker.EnableRb3eTalker(mainViewModel.Rb3eEnabledSetting.IsEnabled, mainViewModel));
        TryStart("StageKit", () => mainViewModel.StageKitTalker.EnableStageKitTalker(mainViewModel.StageKitEnabledSetting.IsEnabled));

        mainViewModel.InitializationTask = InitializeAsync(mainViewModel);
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
        await mainViewModel.WledSerial.Output.EnableAsync(mainViewModel.WledSerialEnabledSetting.IsEnabled, mainViewModel.WledSerial.Options);
        await mainViewModel.Govee.StartAsync();
        await mainViewModel.Wiz.StartAsync();
        await mainViewModel.Nanoleaf.StartAsync();
        await mainViewModel.GlowWorm.Output.EnableAsync(mainViewModel.GlowWormEnabledSetting.IsEnabled, mainViewModel.GlowWorm.Options);
        // Include listener startup in the lifetime tracked by shutdown.
        await StartUdpIntakeAsync(mainViewModel);

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

        try
        {
            await mainViewModel.WledTalker.EnableWled(mainViewModel.WledEnabledSetting.IsEnabled, mainViewModel);
        }
        catch (Exception ex)
        {
            AppLog.Write(LogLevel.Error, "Application", $"Error initializing WLED: {ex.Message}");
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
