using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using YALCY.Scripts.DMX;
using YALCY.Scripts.Hue;
using YALCY.Scripts.OpenRGB;
using YARG.Integration.RB3E;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, INotifyPropertyChanged
{
    public new event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private readonly IClassicDesktopStyleApplicationLifetime _desktop;

    public EnableSetting HueEnabledSetting { get; set; }
    public EnableSetting DmxEnabledSetting { get; set; }
    public EnableSetting StageKitEnabledSetting { get; set; }
    public EnableSetting UdpEnableSetting { get; set; }
    public EnableSetting Rb3eEnabledSetting { get; set; }
    public EnableSetting OpenRgbEnabledSetting { get; set; }

    public ICommand RegisterHueBridgeCommand { set; get; }

    public readonly UsbDeviceMonitor UsbDeviceMonitor;
    public readonly HueTalker HueTalker;
    public readonly DmxTalker DmxTalker;
    public readonly StageKitTalker StageKitTalker;
    public readonly Rb3eTalker Rb3ETalker;
    public readonly UdpIntake UdpIntake;
    public readonly OpenRgbTalker OpenRgbTalker;

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
        UdpIntake = new UdpIntake();
        UsbDeviceMonitor = new UsbDeviceMonitor();
        OpenRgbTalker = new OpenRgbTalker();

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

        // Initialize collections
        InitializeUdpIntakeCollections();
        InitializeUsbCollections();
        //InitializeStageKitCollections();
        InitializeDmxCollections();
        //InitializeRb3eCollections();
        //InitializeHueCollections();
        //InitializeOpenRgbCollections();



        //Things actually start after this fully completes, called from App.axaml.cs
    }


    private void InitializeEnableSettings()
    {
        HueEnabledSetting = new EnableSetting(
            "Hue Enabled",
            SettingsManager.HueEnabledSettingIsEnabled,
            "YALCY is talking hue!",
            "YALCY is NOT talking hue!",
            async (isEnabled) => await HueTalker.EnableHue(isEnabled, HueBridgeIp), "Enable or disable output to the Hue Bridge"
        );

        DmxEnabledSetting = new EnableSetting(
            "DMX Enabled",
            SettingsManager.DmxEnabledSettingIsEnabled,
            "YALCY is talking sACN!",
            "YALCY is NOT talking sACN!",
            async (isEnabled) => DmxTalker.EnableDmxTalker(isEnabled), "Enable or disable sACN udp output to the network"
        );

        StageKitEnabledSetting = new EnableSetting(
            "StageKit Enabled",
            SettingsManager.StageKitEnabledSettingIsEnabled,
            "YALCY is talking to the stage kits!",
            "YALCY is NOT talking to the stage kits!",
             async (isEnabled) => StageKitTalker.EnableStageKitTalker(isEnabled), "Enable or disable output to USB devices"
        );

        UdpEnableSetting = new EnableSetting(
            "UDP Enabled",
            SettingsManager.UdpEnableSettingIsEnabled,
            "YALCY is listening",
            "YALCY is not listening",
            async (isEnabled) => await UdpIntake.EnableUdpIntake(isEnabled), "Enable or disable UDP intake from the network via YARG"
        );

        Rb3eEnabledSetting = new EnableSetting(
            "RB3E Enabled",
            SettingsManager.Rb3eEnabledSettingIsEnabled,
            "YALCY is talking RB3E!",
            "YALCY is NOT talking to the RB3E!",
            async (isEnabled) => Rb3ETalker.EnableRb3eTalker(isEnabled), "Enable or disable a partial implementation of the RB3E udp protocol"
        );

        OpenRgbEnabledSetting = new EnableSetting(
            "OpenRGB Enabled",
            SettingsManager.OpenRgbEnabledSettingIsEnabled,
            "YALCY is talking OpenRGB!",
            "YALCY is NOT talking to OpenRGB!",
            async (isEnabled) => OpenRgbTalker.EnableOpenRgbTalker(isEnabled), "Enable or disable output to a OpenRGB client"
        );
    }

    private void InitializeCommands()
    {
        RegisterHueBridgeCommand = ReactiveCommand.CreateFromTask(() => HueTalker.RegisterHueBridgeAsync(HueBridgeIp));
    }

    private async void ShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        // Save the settings
        SettingsManager.SaveSettings(this);

        // Turn off the OpenRGB talker
        OpenRgbTalker.EnableOpenRgbTalker(false);

        // Turn off the RB3E Talker
        Rb3ETalker.EnableRb3eTalker(false);

        // Turn off the sACN/DMX talker
        DmxTalker.EnableDmxTalker(false);

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

    [JsonIgnore]
    public string ToggleButtonContent => IsEnabled ? _onString : _offString;

    public EnableSetting(string label, bool isEnabled, string onString, string offString, Func<bool, Task>onSettingChanged, string toolTip)
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

