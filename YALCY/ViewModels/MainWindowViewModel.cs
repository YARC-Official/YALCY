using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using HidSharp;
using HidSharp.Experimental;
using OpenRGB.NET;
using ReactiveUI;
using YALCY.Integrations.DMX;
using YALCY.Integrations.HomeAssistant;
using YALCY.Integrations.Hue;
using YALCY.Integrations.Lifx;
using YALCY.Integrations.OpenRGB;
using YALCY.Integrations.RB3E;
using YALCY.Integrations.Serial;
using YALCY.Integrations.StageKit;
using YALCY.Usb;
using YALCY.ViewModels.OpenRGB;
using Device = OpenRGB.NET.Device;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, INotifyPropertyChanged
{

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        this.RaisePropertyChanged(propertyName);
    }

    private string _themeVariant = SettingsManager.SystemThemeVariant;
    public IReadOnlyList<string> ThemeVariants { get; } =
        new[]
        {
            SettingsManager.SystemThemeVariant,
            SettingsManager.DarkThemeVariant,
            SettingsManager.LightThemeVariant
        };

    public string ThemeVariant
    {
        get => _themeVariant;
        set
        {
            var normalized = SettingsManager.NormalizeThemeVariant(value);
            if (_themeVariant == normalized)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _themeVariant, normalized);
            SettingsManager.ThemeVariant = normalized;
            (Application.Current as App)?.SetThemeVariant(normalized);
        }
    }

    private readonly IClassicDesktopStyleApplicationLifetime? _desktop;
    private readonly bool _isHeadless;
    private bool _closeToTrayOnClose;
    public EnableSetting HueEnabledSetting { get; set; }
    public EnableSetting DmxEnabledSetting { get; set; }
    public EnableSetting StageKitEnabledSetting { get; set; }
    public EnableSetting UdpEnableSetting { get; set; }
    public EnableSetting Rb3eEnabledSetting { get; set; }
    public EnableSetting OpenRgbEnabledSetting { get; set; }
    public EnableSetting SerialEnabledSetting { get; set; }
    public EnableSetting LifxEnabledSetting { get; set; }
    public EnableSetting HomeAssistantEnabledSetting { get; set; }
    public readonly UsbDeviceMonitor UsbDeviceMonitor;
    public readonly HueTalker HueTalker;
    public readonly LifxTalker LifxTalker;
    public readonly HomeAssistantTalker HomeAssistantTalker;
    public readonly DmxTalker DmxTalker;
    public readonly StageKitTalker StageKitTalker;
    public readonly Rb3eTalker Rb3ETalker;
    public readonly SerialTalker SerialTalker;
    public readonly Udp.UdpIntake UdpIntake;
    public OpenRgbTalker OpenRgbTalker { get; set; }
    public bool CloseToTrayOnClose
    {
        get => _closeToTrayOnClose;
        set => this.RaiseAndSetIfChanged(ref _closeToTrayOnClose, value);
    }

    private bool _showLedLabels;
    public bool ShowLedLabels
    {
        get => _showLedLabels;
        set
        {
            this.RaiseAndSetIfChanged(ref _showLedLabels, value);
            LedDisplay.GlobalShowLabels = value;
        }
    }

    private double _windowWidth = 1700;
    public double WindowWidth
    {
        get => _windowWidth;
        set
        {
            if (Math.Abs(_windowWidth - value) > 0.1)
            {
                this.RaiseAndSetIfChanged(ref _windowWidth, value);
                UpdateLayoutBreakpoints();
            }
        }
    }

    private bool _isCompactMode;
    public bool IsCompactMode
    {
        get => _isCompactMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _isCompactMode, value);
            this.RaisePropertyChanged(nameof(IsVisualizerCardVisible));
        }
    }

    private bool _isNarrowMode;
    public bool IsNarrowMode
    {
        get => _isNarrowMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _isNarrowMode, value);
        }
    }

    private bool _isVisualizerDetached;
    public bool IsVisualizerDetached
    {
        get => _isVisualizerDetached;
        set
        {
            this.RaiseAndSetIfChanged(ref _isVisualizerDetached, value);
            this.RaisePropertyChanged(nameof(IsVisualizerCardVisible));
        }
    }

    public bool IsVisualizerCardVisible => !IsNarrowMode && !IsVisualizerDetached;

    private int _gridColumns4 = 4;
    public int GridColumns4
    {
        get => _gridColumns4;
        set => this.RaiseAndSetIfChanged(ref _gridColumns4, value);
    }

    private int _gridColumns3 = 3;
    public int GridColumns3
    {
        get => _gridColumns3;
        set => this.RaiseAndSetIfChanged(ref _gridColumns3, value);
    }

    private int _gridColumns2 = 2;
    public int GridColumns2
    {
        get => _gridColumns2;
        set => this.RaiseAndSetIfChanged(ref _gridColumns2, value);
    }

    private void UpdateLayoutBreakpoints()
    {
        GridColumns4 = _windowWidth < 650 ? 1 : (_windowWidth < 1050 ? 2 : 4);
        GridColumns3 = _windowWidth < 700 ? 1 : (_windowWidth < 1050 ? 2 : 3);
        GridColumns2 = _windowWidth < 1200 ? 1 : 2;
        IsCompactMode = _windowWidth < 1200;
        IsNarrowMode = GridColumns4 <= 2;
    }

    public MainWindowViewModel(bool isHeadless = false)
    {
        _isHeadless = isHeadless;

        // Register ShutdownRequested event handler only for GUI mode
        if (!isHeadless && Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            _desktop.ShutdownRequested += ShutdownRequested;
        }

        HueTalker = new HueTalker();
        LifxTalker = new LifxTalker();
        HomeAssistantTalker = new HomeAssistantTalker();
        DmxTalker = new DmxTalker();
        StageKitTalker = new StageKitTalker();
        Rb3ETalker = new Rb3eTalker();
        UdpIntake = new Udp.UdpIntake();
        UsbDeviceMonitor = new UsbDeviceMonitor();
        OpenRgbTalker = new OpenRgbTalker();
        SerialTalker = new SerialTalker();
        InitializeSafety();

        // Initialize EnableSettings using loaded settings
        InitializeEnableSettings();

        // Load additional settings
        FeedInUdpSettings();
        //FeedInUsbSettings();
        //FeedInStageKitSettings();
        FeedInDmxSettings();
        //FeedInRb3eSettings();
        FeedInHueSettings();
        FeedInLifxSettings();
        FeedInHomeAssistantSettings();
        FeedInOpenRgbSettings();
        FeedInStrobeModeSettings();
        FeedInAppSettings();

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
        InitializeLifxCollections();
        InitializeHomeAssistantCollections();
        InitializeOpenRgbCollections();

        // Wire up DmxTalker to dimmer settings
        MasterDimmerSettings.SetDmxTalker(DmxTalker);
        MasterDimmerValues.SetDmxTalker(DmxTalker);

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
            async (isEnabled) => Rb3ETalker.EnableRb3eTalker(isEnabled, this),
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

        LifxEnabledSetting = new EnableSetting(
            "LIFX Enabled",
            SettingsManager.LifxEnabledSettingIsEnabled,
            "YALCY is talking LIFX!",
            "YALCY is NOT talking LIFX!",
            async (isEnabled) => await LifxTalker.EnableLifxLan(isEnabled),
            "Enable or disable output to LIFX LAN devices"
        );

        HomeAssistantEnabledSetting = new EnableSetting(
            "Home Assistant Enabled",
            SettingsManager.HomeAssistantEnabledSettingIsEnabled,
            "YALCY is talking Home Assistant!",
            "YALCY is NOT talking Home Assistant!",
            async (isEnabled) => await HomeAssistantTalker.EnableHomeAssistant(isEnabled),
            "Enable or disable Home Assistant light service output"
        );

        OpenRgbEnabledSetting = new EnableSetting(
            "OpenRGB Enabled",
            SettingsManager.OpenRgbEnabledSettingIsEnabled,
            "YALCY is talking OpenRGB!",
            "YALCY is NOT talking to OpenRGB!",
            async (isEnabled) => await OpenRgbTalker.EnableOpenRgbTalker(isEnabled, OpenRgbServerIp ?? "127.0.0.1", OpenRgbServerPort),
            "Enable or disable output to a OpenRGB client"
        );
    }

    private void FeedInAppSettings()
    {
        CloseToTrayOnClose = SettingsManager.CloseToTrayOnClose;
        _themeVariant = SettingsManager.NormalizeThemeVariant(SettingsManager.ThemeVariant);
        this.RaisePropertyChanged(nameof(ThemeVariant));
        (Application.Current as App)?.SetThemeVariant(_themeVariant);
    }

    private void InitializeCommands()
    {
        RegisterHueBridgeCommand = ReactiveCommand.CreateFromTask(() => HueTalker.RegisterHueBridgeAsync(HueBridgeIp));
        DiscoverLifxDevicesCommand = ReactiveCommand.CreateFromTask(() => LifxTalker.DiscoverDevicesAsync(this));
        DiscoverHomeAssistantLightsCommand = ReactiveCommand.CreateFromTask(() => HomeAssistantTalker.DiscoverLightsAsync(this));
        AddHomeAssistantEntityCommand = ReactiveCommand.Create(AddHomeAssistantEntity);
        ConnectToOpenRgbServerCommand = ReactiveCommand.CreateFromTask(() =>
            OpenRgbTalker.ConnectToOpenRgbServerAsync(OpenRgbServerIp ?? "127.0.0.1", OpenRgbServerPort));
    }

    /// <summary>
    /// Shuts down all integrations and saves settings. Used by both GUI and CLI.
    /// </summary>
    public async Task ShutdownAsync()
    {
        // Save the settings
        SettingsManager.SaveSettings(this);

        // Turn off the OpenRGB talker
        await OpenRgbTalker.EnableOpenRgbTalker(false, OpenRgbServerIp ?? "127.0.0.1", OpenRgbServerPort, this);

        // Turn off the RB3E Talker
        Rb3ETalker.EnableRb3eTalker(false, this);

        // Turn off the sACN/DMX talker
        DmxTalker.EnableDmxTalker(false, this);

        // Turn off the Serial Talker
        SerialTalker.EnableSerialTalker(false, this);

        // Turn off the StageKit
        StageKitTalker.EnableStageKitTalker(false);

        // Turn off the Hue Talker
        await HueTalker.EnableHue(false, HueBridgeIp, this);

        // Turn off the LIFX Talker
        await LifxTalker.EnableLifxLan(false, this);

        // Turn off the Home Assistant Talker
        await HomeAssistantTalker.EnableHomeAssistant(false, this);

        // Turn off the USB device monitor
        UsbDeviceMonitor.StopUsbDeviceMonitor();

        // Turn off the UDP listener
        await UdpIntake.EnableUdpIntake(false, this);
    }

    private bool _isShuttingDown;
    private async void ShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_isShuttingDown) return;
        _isShuttingDown = true;

        e.Cancel = true;
        await ShutdownAsync();
        _desktop?.Shutdown();
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
