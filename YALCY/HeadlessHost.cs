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
        Console.WriteLine("Initializing YALCY integrations...");

        // Start USB device monitor
        ViewModel.UsbDeviceMonitor.StartUsbDeviceMonitor(ViewModel);
        Console.WriteLine("  USB device monitor: Started");

        // Initialize UDP intake first (needed by other integrations)
        if (ViewModel.UdpEnableSetting.IsEnabled)
        {
            await ViewModel.UdpIntake.EnableUdpIntake(true, ViewModel);
            Console.WriteLine("  UDP intake: Enabled");
        }
        else
        {
            Console.WriteLine("  UDP intake: Disabled");
        }

        // Initialize DMX/sACN
        if (ViewModel.DmxEnabledSetting.IsEnabled)
        {
            ViewModel.DmxTalker.EnableDmxTalker(true, ViewModel);
            Console.WriteLine("  DMX/sACN: Enabled");
        }
        else
        {
            Console.WriteLine("  DMX/sACN: Disabled");
        }

        // Initialize Hue
        if (ViewModel.HueEnabledSetting.IsEnabled)
        {
            await ViewModel.HueTalker.EnableHue(true, ViewModel.HueBridgeIp, ViewModel);
            Console.WriteLine("  Hue: Enabled");
        }
        else
        {
            Console.WriteLine("  Hue: Disabled");
        }

        // Initialize Serial
        if (ViewModel.SerialEnabledSetting.IsEnabled)
        {
            ViewModel.SerialTalker.EnableSerialTalker(true, ViewModel);
            Console.WriteLine("  Serial: Enabled");
        }
        else
        {
            Console.WriteLine("  Serial: Disabled");
        }

        // Initialize StageKit
        if (ViewModel.StageKitEnabledSetting.IsEnabled)
        {
            ViewModel.StageKitTalker.EnableStageKitTalker(true);
            Console.WriteLine("  StageKit: Enabled");
        }
        else
        {
            Console.WriteLine("  StageKit: Disabled");
        }

        // Initialize RB3E
        if (ViewModel.Rb3eEnabledSetting.IsEnabled)
        {
            ViewModel.Rb3ETalker.EnableRb3eTalker(true);
            Console.WriteLine("  RB3E: Enabled");
        }
        else
        {
            Console.WriteLine("  RB3E: Disabled");
        }

        // Initialize OpenRGB
        if (ViewModel.OpenRgbEnabledSetting.IsEnabled)
        {
            await ViewModel.OpenRgbTalker.EnableOpenRgbTalker(true, ViewModel.OpenRgbServerIp, ViewModel.OpenRgbServerPort, ViewModel);
            Console.WriteLine("  OpenRGB: Enabled");
        }
        else
        {
            Console.WriteLine("  OpenRGB: Disabled");
        }

        Console.WriteLine("Initialization complete.");
    }

    /// <summary>
    /// Shuts down all integrations and saves settings.
    /// </summary>
    public async Task ShutdownAsync()
    {
        Console.WriteLine("Shutting down YALCY integrations...");
        await ViewModel.ShutdownAsync();
        Console.WriteLine("Shutdown complete.");
    }

    public void Dispose()
    {
        // Synchronous dispose - will block
        ShutdownAsync().GetAwaiter().GetResult();
    }
}
