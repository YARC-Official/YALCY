using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using YALCY.ViewModels;
using YALCY.Views;
using System.Reflection;

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

    private void InitializeComponents()
    {
        var mainViewModel = MainViewModel;

        mainViewModel.UsbDeviceMonitor.StartUsbDeviceMonitor();
        mainViewModel.HueTalker.EnableHue(mainViewModel.HueEnabledSetting.IsEnabled, mainViewModel.HueBridgeIp);
        mainViewModel.DmxTalker.EnableDmxTalker(mainViewModel.DmxEnabledSetting.IsEnabled);
        mainViewModel.SerialTalker.EnableSerialTalker(mainViewModel.SerialEnabledSetting.IsEnabled);
        mainViewModel.Rb3ETalker.EnableRb3eTalker(mainViewModel.Rb3eEnabledSetting.IsEnabled);
        mainViewModel.StageKitTalker.EnableStageKitTalker(mainViewModel.StageKitEnabledSetting.IsEnabled);
        mainViewModel.OpenRgbTalker.EnableOpenRgbTalker(mainViewModel.OpenRgbEnabledSetting.IsEnabled,
            mainViewModel.OpenRgbServerIp, mainViewModel.OpenRgbServerPort);
        mainViewModel.UdpIntake.EnableUdpIntake(mainViewModel.UdpEnableSetting.IsEnabled);
    }
}
