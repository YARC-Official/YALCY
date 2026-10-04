using YALCY.Diagnostics;
using System;
using System.Threading.Tasks;
using YALCY.ViewModels;

namespace YALCY;

/// <summary>
/// Provides a headless host for running YALCY integrations without a GUI.
/// Used by the CLI version of YALCY.
/// </summary>
public class HeadlessHost : IDisposable
{
    public MainWindowViewModel ViewModel { get; }

    public HeadlessHost()
    {
        // Load settings before creating the ViewModel
        SettingsManager.LoadSettings();

        // Create ViewModel in headless mode
        ViewModel = new MainWindowViewModel(isHeadless: true);
    }

    /// <summary>
    /// Initializes all enabled integrations based on saved settings.
    /// </summary>
    public async Task InitializeAsync()
    {
        AppLog.Write(LogLevel.Information, "Application", "Initializing YALCY integrations...");

        // Start USB device monitor
        ViewModel.UsbDeviceMonitor.StartUsbDeviceMonitor(ViewModel);
        AppLog.Write(LogLevel.Information, "Application", "  USB device monitor: Started");

        // Initialize UDP intake first (needed by other integrations)
        if (ViewModel.UdpEnableSetting.IsEnabled)
        {
            await ViewModel.UdpIntake.EnableUdpIntake(true, ViewModel);
            AppLog.Write(LogLevel.Information, "Application", "  UDP intake: Enabled");
        }
        else
        {
            AppLog.Write(LogLevel.Information, "Application", "  UDP intake: Disabled");
        }

        // Initialize DMX/sACN
        if (ViewModel.DmxEnabledSetting.IsEnabled)
        {
            ViewModel.DmxTalker.EnableDmxTalker(true, ViewModel);
            AppLog.Write(LogLevel.Information, "Application", "  DMX/sACN: Enabled");
        }
        else
        {
            AppLog.Write(LogLevel.Information, "Application", "  DMX/sACN: Disabled");
        }

        // Initialize Hue
        if (ViewModel.HueEnabledSetting.IsEnabled)
        {
            await ViewModel.HueTalker.EnableHue(true, ViewModel.HueBridgeIp, ViewModel);
            AppLog.Write(LogLevel.Information, "Application", "  Hue: Enabled");
        }
        else
        {
            AppLog.Write(LogLevel.Information, "Application", "  Hue: Disabled");
        }

        // Initialize LIFX LAN
        if (ViewModel.LifxEnabledSetting.IsEnabled)
        {
            await ViewModel.LifxTalker.EnableLifxLan(true, ViewModel);
            AppLog.Write(LogLevel.Information, "Application", "  LIFX: Enabled");
        }
        else
        {
            AppLog.Write(LogLevel.Information, "Application", "  LIFX: Disabled");
        }

        // Initialize Home Assistant
        if (ViewModel.HomeAssistantEnabledSetting.IsEnabled)
        {
            await ViewModel.HomeAssistantTalker.EnableHomeAssistant(true, ViewModel);
            AppLog.Write(LogLevel.Information, "Application", "  Home Assistant: Enabled");
        }
        else
        {
            AppLog.Write(LogLevel.Information, "Application", "  Home Assistant: Disabled");
        }

        // Initialize Serial
        if (ViewModel.SerialEnabledSetting.IsEnabled)
        {
            ViewModel.SerialTalker.EnableSerialTalker(true, ViewModel);
            AppLog.Write(LogLevel.Information, "Application", "  Serial: Enabled");
        }
        else
        {
            AppLog.Write(LogLevel.Information, "Application", "  Serial: Disabled");
        }

        // Initialize StageKit
        if (ViewModel.StageKitEnabledSetting.IsEnabled)
        {
            ViewModel.StageKitTalker.EnableStageKitTalker(true);
            AppLog.Write(LogLevel.Information, "Application", "  StageKit: Enabled");
        }
        else
        {
            AppLog.Write(LogLevel.Information, "Application", "  StageKit: Disabled");
        }

        // Initialize RB3E
        if (ViewModel.Rb3eEnabledSetting.IsEnabled)
        {
            ViewModel.Rb3ETalker.EnableRb3eTalker(true, ViewModel);
            AppLog.Write(LogLevel.Information, "Application", "  RB3E: Enabled");
        }
        else
        {
            AppLog.Write(LogLevel.Information, "Application", "  RB3E: Disabled");
        }

        // Initialize OpenRGB
        if (ViewModel.OpenRgbEnabledSetting.IsEnabled)
        {
            await ViewModel.OpenRgbTalker.EnableOpenRgbTalker(true, ViewModel.OpenRgbServerIp ?? string.Empty, ViewModel.OpenRgbServerPort, ViewModel);
            AppLog.Write(LogLevel.Information, "Application", "  OpenRGB: Enabled");
        }
        else
        {
            AppLog.Write(LogLevel.Information, "Application", "  OpenRGB: Disabled");
        }

        AppLog.Write(LogLevel.Information, "Application", "Initialization complete.");
    }

    /// <summary>
    /// Shuts down all integrations and saves settings.
    /// </summary>
    public async Task ShutdownAsync()
    {
        AppLog.Write(LogLevel.Information, "Application", "Shutting down YALCY integrations...");
        await ViewModel.ShutdownAsync();
        AppLog.Write(LogLevel.Information, "Application", "Shutdown complete.");
    }

    public void Dispose()
    {
        // Synchronous dispose - will block
        ShutdownAsync().GetAwaiter().GetResult();
    }
}
