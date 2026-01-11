using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using HidSharp;
using HidSharp.Experimental;
using OpenRGB.NET;
using ReactiveUI;
using YALCY.Integrations.DMX;
using YALCY.Integrations.Hue;
using YALCY.Integrations.OpenRGB;
using YALCY.Integrations.RB3E;
using YALCY.Integrations.Serial;
using YALCY.Integrations.StageKit;
using YALCY.Usb;
using Device = OpenRGB.NET.Device;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, INotifyPropertyChanged
{

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private readonly IClassicDesktopStyleApplicationLifetime _desktop;

    public new event PropertyChangedEventHandler? PropertyChanged;
    public EnableSetting HueEnabledSetting { get; set; }
    public EnableSetting DmxEnabledSetting { get; set; }
    public EnableSetting StageKitEnabledSetting { get; set; }
    public EnableSetting UdpEnableSetting { get; set; }
    public EnableSetting Rb3eEnabledSetting { get; set; }
    public EnableSetting OpenRgbEnabledSetting { get; set; }
    public EnableSetting SerialEnabledSetting { get; set; }
    public readonly UsbDeviceMonitor UsbDeviceMonitor;
    public readonly HueTalker HueTalker;
    public readonly DmxTalker DmxTalker;
    public readonly StageKitTalker StageKitTalker;
    public readonly Rb3eTalker Rb3ETalker;
    public readonly SerialTalker SerialTalker;
    public readonly Udp.UdpIntake UdpIntake;
    public OpenRgbTalker OpenRgbTalker { get; set; }

    public MainWindowViewModel()
    {
        // Register ShutdownRequested event handler
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            _desktop.ShutdownRequested += ShutdownRequested;
        }

        HueTalker = new HueTalker();
        DmxTalker = new DmxTalker();
        StageKitTalker = new StageKitTalker();
        Rb3ETalker = new Rb3eTalker();
        UdpIntake = new Udp.UdpIntake();
        UsbDeviceMonitor = new UsbDeviceMonitor();
        OpenRgbTalker = new OpenRgbTalker();
        SerialTalker = new SerialTalker();

        // Initialize EnableSettings using loaded settings
        InitializeEnableSettings();

        // Load additional settings
        FeedInUdpSettings();
        //FeedInUsbSettings();
        //FeedInStageKitSettings();
        FeedInDmxSettings();
        //FeedInRb3eSettings();
        FeedInHueSettings();
        FeedInOpenRgbSettings();

        // Other initialization code
        InitializeCommands();
        InitializeSacnAdapterOptions();

        // Initialize collections
        InitializeUdpIntakeCollections();
        InitializeUsbCollections();
        //InitializeStageKitCollections();
        InitializeDmxCollections();
        //InitializeRb3eCollections();
        //InitializeHueCollections();
        InitializeOpenRgbCollections();

        //Things actually start after this fully completes, called from App.axaml.cs
    }

    private void InitializeEnableSettings()
    {
        HueEnabledSetting = new EnableSetting(
            "Hue Enabled",
            SettingsManager.HueEnabledSettingIsEnabled,
            "YALCY is talking hue!",
            "YALCY is NOT talking hue!",
            async (isEnabled) => await HueTalker.EnableHue(isEnabled, HueBridgeIp),
            "Enable or disable output to the Hue Bridge"
        );

        DmxEnabledSetting = new EnableSetting(
            "DMX Enabled",
            SettingsManager.DmxEnabledSettingIsEnabled,
            "YALCY is talking sACN!",
            "YALCY is NOT talking sACN!",
            async (isEnabled) => DmxTalker.EnableDmxTalker(isEnabled),
            "Enable or disable sACN udp output to the network"
        );

        StageKitEnabledSetting = new EnableSetting(
            "StageKit Enabled",
            SettingsManager.StageKitEnabledSettingIsEnabled,
            "YALCY is talking to the stage kits!",
            "YALCY is NOT talking to the stage kits!",
            async (isEnabled) => StageKitTalker.EnableStageKitTalker(isEnabled),
            "Enable or disable output to USB devices"
        );

        UdpEnableSetting = new EnableSetting(
            "UDP Enabled",
            SettingsManager.UdpEnableSettingIsEnabled,
            "YALCY is listening",
            "YALCY is not listening",
            async (isEnabled) => await UdpIntake.EnableUdpIntake(isEnabled),
            "Enable or disable UDP intake from the network via YARG"
        );

        Rb3eEnabledSetting = new EnableSetting(
            "RB3E Enabled",
            SettingsManager.Rb3eEnabledSettingIsEnabled,
            "YALCY is talking RB3E!",
            "YALCY is NOT talking to the RB3E!",
            async (isEnabled) => Rb3ETalker.EnableRb3eTalker(isEnabled),
            "Enable or disable a partial implementation of the RB3E udp protocol"
        );

        SerialEnabledSetting = new EnableSetting(
            "Serial Enabled",
            SettingsManager.SerialEnabledSettingIsEnabled,
            "YALCY is talking serial!",
            "YALCY is NOT talking serial!",
            async (isEnabled) => SerialTalker.EnableSerialTalker(isEnabled),
            "Enable or disable output to a serial device"
        );

        OpenRgbEnabledSetting = new EnableSetting(
            "OpenRGB Enabled",
            SettingsManager.OpenRgbEnabledSettingIsEnabled,
            "YALCY is talking OpenRGB!",
            "YALCY is NOT talking to OpenRGB!",
            async (isEnabled) => OpenRgbTalker.EnableOpenRgbTalker(isEnabled, OpenRgbServerIp, OpenRgbServerPort),
            "Enable or disable output to a OpenRGB client"
        );
    }

    private void InitializeCommands()
    {
        RegisterHueBridgeCommand = ReactiveCommand.CreateFromTask(() => HueTalker.RegisterHueBridgeAsync(HueBridgeIp));
        ConnectToOpenRgbServerCommand = ReactiveCommand.CreateFromTask(() =>
            OpenRgbTalker.ConnectToOpenRgbServerAsync(OpenRgbServerIp, OpenRgbServerPort));
    }

    private async void ShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        // Save the settings
        SettingsManager.SaveSettings(this);

        // Turn off the OpenRGB talker
        OpenRgbTalker.EnableOpenRgbTalker(false, OpenRgbServerIp, OpenRgbServerPort);

        // Turn off the RB3E Talker
        Rb3ETalker.EnableRb3eTalker(false);

        // Turn off the sACN/DMX talker
        DmxTalker.EnableDmxTalker(false);

        // Turn off the Serial Talker
        SerialTalker.EnableSerialTalker(false);

        // Turn off the StageKit
        StageKitTalker.EnableStageKitTalker(false);

        // Turn off the Hue Talker
        await HueTalker.EnableHue(false, HueBridgeIp);

        // Turn off the USB device monitor
        UsbDeviceMonitor.StopUsbDeviceMonitor();

        // Turn off the UDP listener
        await UdpIntake.EnableUdpIntake(false);
    }
}

public class EnableSetting : ReactiveObject
{
    private string _label;
    private bool _isEnabled;
    private readonly string _onString;
    private readonly string _offString;
    private readonly Func<bool, Task> _onSettingChanged;

    [DataMember]
    public string Label
    {
        get => _label;
        set => this.RaiseAndSetIfChanged(ref _label, value);
    }

    [DataMember]
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            this.RaiseAndSetIfChanged(ref _isEnabled, value);
            this.RaisePropertyChanged(nameof(ToggleButtonContent));
            _onSettingChanged?.Invoke(value);
        }
    }

    private string _toolTip;

    [JsonIgnore]
    public string ToolTip
    {
        get => _toolTip;
        set => this.RaiseAndSetIfChanged(ref _toolTip, value);
    }

    [JsonIgnore] public string ToggleButtonContent => IsEnabled ? _onString : _offString;

    public EnableSetting(string label, bool isEnabled, string onString, string offString,
        Func<bool, Task> onSettingChanged, string toolTip)
    {
        Label = label;
        _label = label;
        IsEnabled = isEnabled;
        _onString = onString;
        _offString = offString;
        _onSettingChanged = onSettingChanged;
        _toolTip = toolTip;
        ToolTip = toolTip;
    }
}

public class DeviceCategory : ReactiveObject, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    public Device Device { get; set; }
    private int _category;

    public int Category
    {
        get => _category;
        set
        {
            RemoveFromCategoryList(_category);
            _category = value;
            UpdateDeviceCategoryList(_category);
            OnPropertyChanged(nameof(Category));
        }
    }

    public DeviceCategory(Device device, int initialCategory)
    {
        Device = device;
        _category = initialCategory;
    }

    private void RemoveFromCategoryList(int category)
    {
        App app;
        MainWindowViewModel mainViewModel;

        app = (App)Application.Current!;
        mainViewModel = app.MainViewModel;

        switch (category)
        {
            case 0:
                mainViewModel.OpenRgbTalker.OffList.Remove(Device);
                break;

            case 1:
                mainViewModel.OpenRgbTalker.LightPodList.Remove(Device);
                lock (mainViewModel.OpenRgbTalker.LightPodStates)
                {
                    mainViewModel.OpenRgbTalker.LightPodStates.Remove(Device.Index);
                }
                break;

            case 2:
                mainViewModel.OpenRgbTalker.StrobeList.Remove(Device);
                break;

            case 3:
                mainViewModel.OpenRgbTalker.FoggerList.Remove(Device);
                break;
        }
    }

    private void UpdateDeviceCategoryList(int category)
    {
        App app;
        MainWindowViewModel mainViewModel;

        app = (App)Application.Current!;
        mainViewModel = app.MainViewModel;

        // Add the device to the correct list based on the category
        switch (category)
        {
            case 0:
                mainViewModel.OpenRgbTalker.OffList.Add(Device);
                break;

            case 1:
                mainViewModel.OpenRgbTalker.LightPodList.Add(Device);
                lock (mainViewModel.OpenRgbTalker.LightPodStates)
                {
                    if (!mainViewModel.OpenRgbTalker.LightPodStates.ContainsKey(Device.Index))
                    {
                        // Initialize the light pod state for this device
                        mainViewModel.OpenRgbTalker.LightPodStates[Device.Index] = new Color[Device.Leds.Length];
                    }
                }
                break;

            case 2:
                mainViewModel.OpenRgbTalker.StrobeList.Add(Device);
                break;

            case 3:
                mainViewModel.OpenRgbTalker.FoggerList.Add(Device);
                break;
        }
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
