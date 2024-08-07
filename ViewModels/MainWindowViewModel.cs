using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using HidSharp;
using HueApi.Models.Clip;
using ReactiveUI;
using YALCY.Scripts.DMX;
using YALCY.Scripts.Hue;
using YALCY.Scripts.OpenRGB;
using YARG.Integration.RB3E;

namespace YALCY.ViewModels;

public class MainWindowViewModel : ViewModelBase, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private ushort _udpListenPort;

    public ushort UdpListenPort
    {
        get => _udpListenPort;
        set => this.RaiseAndSetIfChanged(ref _udpListenPort, value);
    }

    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    public static ObservableCollection<HidDevice>? ConnectedDevices { get; set; }
    public static ObservableCollection<DataTypes.ByteIndexModel>? ByteIndexes { get; set; }

    public DataTypes.DmxDimmerChannelSetting MasterDimmerSettings = new("Master Dimmer Channels", 1, 8, 15, 22, 29, 36, 43, 50);
    public DataTypes.DmxDimmerValueSetting MasterDimmerValues = new("Master Dimmer Values", 255, 255, 255, 255, 255, 255, 255, 255);

    public DataTypes.DmxChannelSetting StrobeChannels = new("Strobe Channels", 7, 14, 21, 28, 35, 42, 49, 56);
    public DataTypes.DmxChannelSetting FogChannels = new("Fog Channels", 6, 13, 20, 27, 34, 41, 48, 55);
    public DataTypes.DmxChannelSetting RedChannels = new("Red Channels", 2, 9, 16, 23, 30, 37, 44, 51);
    public DataTypes.DmxChannelSetting BlueChannels = new("Blue Channels", 3, 10, 17, 24, 31, 38, 45, 52);
    public DataTypes.DmxChannelSetting YellowChannels = new("Yellow Channels", 4, 11, 18, 25, 32, 39, 46, 53);
    public DataTypes.DmxChannelSetting GreenChannels = new("Green Channels", 5, 12, 19, 26, 33, 40, 47, 54);
    public DataTypes.DmxSingleSetting BpmChannelSetting = new("BPM Channel", 56);
    public DataTypes.DmxSingleSetting CueChangeChannelSetting = new("Cue Change Channel", 57);
    public DataTypes.DmxSingleSetting PostProcessingChannelSetting = new("Post-Processing Channel", 62);
    public DataTypes.DmxSingleSetting KeyFrameChannelSetting = new("KeyFrame Channel", 60);
    public DataTypes.DmxSingleSetting BeatLineChannelSetting = new("BeatLine Channel", 58);
    public DataTypes.DmxSingleSetting BonusEffectChannelSetting = new("Bonus Effect Channel", 59);
    public DataTypes.DmxSingleSetting DrumNoteChannelSetting = new("Drum Note Channel", 61);
    public DataTypes.DmxSingleSetting GuitarNoteChannelSetting = new("Guitar Note Channel", 63);
    public DataTypes.DmxSingleSetting BassNoteChannelSetting = new("Bass Note Channel", 64);
    public DataTypes.DmxSingleSetting CurrentPerformerSetting = new("Current Performer", 65);
    public DataTypes.DmxSingleSetting KeysNoteChannelSetting = new("Keys Note Channel", 66);
    public DataTypes.DmxSingleSetting VocalsNoteChannelSetting = new("Vocals Note Channel", 67);
    public DataTypes.DmxSingleSetting Harmony0NoteChannelSetting = new("Harmony 0 Note Channel", 68);
    public DataTypes.DmxSingleSetting Harmony1NoteChannelSetting = new("Harmony 1 Note Channel", 69);
    public DataTypes.DmxSingleSetting Harmony2NoteChannelSetting = new("Harmony 2 Note Channel", 70);
    public DataTypes.DmxSingleSetting BroadcastUniverseSetting = new("Broadcast Universe", 1);
    public DataTypes.DmxSingleSetting CurrentSceneSetting = new("Current Scene", 71);
    public DataTypes.DmxSingleSetting VenueSizeSetting = new("Venue Size", 72);
    public DataTypes.DmxSingleSetting PauseStateSetting = new("Pause State", 73);
    public DataTypes.DmxSingleSetting SongSectionSetting = new("Song Section", 74);

    public DataTypes.EnableSetting HueEnabledSetting { get; set; }
    public DataTypes.EnableSetting DmxEnabledSetting { get; set; }
    public DataTypes.EnableSetting StageKitEnabledSetting { get; set; }
    public DataTypes.EnableSetting UdpEnableSetting { get; set; }
    public DataTypes.EnableSetting Rb3eEnabledSetting { get; set; }
    public DataTypes.EnableSetting OpenRgbEnabledSetting { get; set; }

    public ObservableCollection<DataTypes.DmxSingleSetting> AdvancedSettingsContainer { get; set; }
    public ObservableCollection<DataTypes.IDmxChannelSetting> EffectsChannelSettingsContainer { get; set; }
    public ObservableCollection<DataTypes.IDmxChannelSetting> MasterDimmerSettingsContainer { get; set; }
    public ObservableCollection<DataTypes.IDmxChannelSetting> ColorChannelSettingsContainer { get; set; }
    public ObservableCollection<DataTypes.DmxSingleSetting> InstrumentNoteSettingsContainer { get; set; }
    public ObservableCollection<DataTypes.DmxSingleSetting> BroadcastSettingsContainer { get; set; }

    private string _hueIpStatus;

    public string HueIpStatus
    {
        get => _hueIpStatus;
        set
        {
            if (_hueIpStatus == value) return;
            _hueIpStatus = value;
            OnPropertyChanged();
        }
    }

    private string _hueRegisterStatus;

    public string HueRegisterStatus
    {
        get => _hueRegisterStatus;
        set
        {
            if (_hueRegisterStatus == value) return;
            _hueRegisterStatus = value;
            OnPropertyChanged();
        }
    }

    private string _hueStreamingClientStatus;

    public string HueStreamingClientStatus
    {
        get => _hueStreamingClientStatus;
        set
        {
            if (_hueStreamingClientStatus == value) return;
            _hueStreamingClientStatus = value;
            OnPropertyChanged();
        }
    }

    private string _hueEntertainmentGroupStatus;

    public string HueEntertainmentGroupStatus
    {
        get => _hueEntertainmentGroupStatus;
        set
        {
            if (_hueEntertainmentGroupStatus == value) return;
            _hueEntertainmentGroupStatus = value;
            OnPropertyChanged();
        }
    }

    private string _hueStreamingActiveStatus;

    public string HueStreamingActiveStatus
    {
        get => _hueStreamingActiveStatus;
        set
        {
            if (_hueStreamingActiveStatus == value) return;
            _hueStreamingActiveStatus = value;
            OnPropertyChanged();
        }
    }

    private string _hueMessage;

    public string HueMessage
    {
        get => _hueMessage;
        set
        {
            if (_hueMessage != value)
            {
                _hueMessage = value;
                OnPropertyChanged();
            }
        }
    }

    private string? _hueBridgeIp;

    public string? HueBridgeIp
    {
        get => _hueBridgeIp;
        set
        {
            if (_hueBridgeIp != value)
            {
                _hueBridgeIp = value;
                OnPropertyChanged();
            }
        }
    }

    private RegisterEntertainmentResult _hueAuthResult;

    public RegisterEntertainmentResult HueAuthResult
    {
        get => _hueAuthResult;
        set
        {
            if (_hueAuthResult != value)
            {
                _hueAuthResult = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand RegisterHueBridgeCommand { set; get; }

    public readonly USBDeviceMonitor UsbDeviceMonitor;
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
        UsbDeviceMonitor = new USBDeviceMonitor();
        OpenRgbTalker = new OpenRgbTalker();

        // Initialize EnableSettings using loaded settings
        InitializeEnableSettings();

        // Load additional settings
        FeedInSettings();

        // Other initialization code
        InitializeCommands();
        InitializeCollections();

        //Things actually start after this fully completes, called from App.axaml.cs
    }

    private void InitializeEnableSettings()
    {
        HueEnabledSetting = new DataTypes.EnableSetting(
            "Hue Enabled",
            SettingsManager.HueEnabledSettingIsEnabled,
            "YALCY is talking hue!",
            "YALCY is NOT talking hue!",
            async (isEnabled) => await HueTalker.EnableHue(isEnabled, HueBridgeIp), "Enable or disable output to the Hue Bridge"
        );


        DmxEnabledSetting = new DataTypes.EnableSetting(
            "DMX Enabled",
            SettingsManager.DmxEnabledSettingIsEnabled,
            "YALCY is talking sACN!",
            "YALCY is NOT talking sACN!",
            async (isEnabled) => DmxTalker.EnableDmxTalker(isEnabled), "Enable or disable sACN udp output to the network"
        );

        StageKitEnabledSetting = new DataTypes.EnableSetting(
            "StageKit Enabled",
            SettingsManager.StageKitEnabledSettingIsEnabled,
            "YALCY is talking to the stage kits!",
            "YALCY is NOT talking to the stage kits!",
             async (isEnabled) => StageKitTalker.EnableStageKitTalker(isEnabled), "Enable or disable output to USB devices"
        );

        UdpEnableSetting = new DataTypes.EnableSetting(
            "UDP Enabled",
            SettingsManager.UdpEnableSettingIsEnabled,
            "YALCY is listening",
            "YALCY is not listening",
            async (isEnabled) => await UdpIntake.EnableUdpIntake(isEnabled), "Enable or disable UDP intake from the network via YARG"
        );

        Rb3eEnabledSetting = new DataTypes.EnableSetting(
            "RB3E Enabled",
            SettingsManager.Rb3eEnabledSettingIsEnabled,
            "YALCY is talking RB3E!",
            "YALCY is NOT talking to the RB3E!",
            async (isEnabled) => Rb3ETalker.EnableRb3eTalker(isEnabled), "Enable or disable a partial implementation of the RB3E udp protocol"
        );

        OpenRgbEnabledSetting = new DataTypes.EnableSetting(
            "OpenRGB Enabled",
            SettingsManager.OpenRgbEnabledSettingIsEnabled,
            "YALCY is talking OpenRGB!",
            "YALCY is NOT talking to OpenRGB!",
            async (isEnabled) => OpenRgbTalker.EnableOpenRgbTalker(isEnabled), "Enable or disable output to a OpenRGB client"
        );
    }

    private void FeedInSettings()
    {
        UdpListenPort = SettingsManager.UdpListenPort;

        HueRegisterStatus = "Registering Status: Currently doing nothing.";
        HueBridgeIp = SettingsManager.HueBridgeIp;

        HueAuthResult = new RegisterEntertainmentResult
        {
            Username = SettingsManager.HueAuthResultUsername,
            StreamingClientKey = SettingsManager.HueAuthResultStreamingClientKey,
            Ip = SettingsManager.HueAuthResultIp
        };
        HueStreamingClientStatus = "Streaming Client Status: Currently doing nothing.";
        HueEntertainmentGroupStatus = "Entertainment Group Status: Currently doing nothing.";
        HueStreamingActiveStatus = "Streaming Active Status: Currently doing nothing.";

            BpmChannelSetting.Value = SettingsManager.BpmChannelSettingValue;
            CueChangeChannelSetting.Value = SettingsManager.CueChangeChannelSettingValue;
            PostProcessingChannelSetting.Value = SettingsManager.PostProcessingChannelSettingValue;
            KeyFrameChannelSetting.Value = SettingsManager.KeyFrameChannelSettingValue;
            BeatLineChannelSetting.Value = SettingsManager.BeatLineChannelSettingValue;
            BonusEffectChannelSetting.Value = SettingsManager.BonusEffectChannelSettingValue;
            DrumNoteChannelSetting.Value = SettingsManager.DrumNoteChannelSettingValue;
            GuitarNoteChannelSetting.Value = SettingsManager.GuitarNoteChannelSettingValue;
            BassNoteChannelSetting.Value = SettingsManager.BassNoteChannelSettingValue;
            KeysNoteChannelSetting.Value = SettingsManager.KeysNoteChannelSettingValue;
            VocalsNoteChannelSetting.Value = SettingsManager.VocalsNoteChannelSettingValue;
            Harmony0NoteChannelSetting.Value = SettingsManager.Harmony0NoteChannelSettingValue;
            Harmony1NoteChannelSetting.Value = SettingsManager.Harmony1NoteChannelSettingValue;
            Harmony2NoteChannelSetting.Value = SettingsManager.Harmony2NoteChannelSettingValue;
            CurrentPerformerSetting.Value = SettingsManager.CurrentPerformerSettingValue;
            BroadcastUniverseSetting.Value = SettingsManager.BroadcastUniverseSettingValue;
            CurrentSceneSetting.Value = SettingsManager.CurrentSceneSettingValue;
            VenueSizeSetting.Value = SettingsManager.VenueSizeSettingValue;
            PauseStateSetting.Value = SettingsManager.PauseStateSettingValue;
            SongSectionSetting.Value = SettingsManager.SongSectionSettingValue;

            MasterDimmerSettings.Channel = SettingsManager.MasterDimmerSettingsChannel;
            MasterDimmerValues.Channel = SettingsManager.MasterDimmerValuesChannel;
            StrobeChannels.Channel = SettingsManager.StrobeChannelsChannel;
            FogChannels.Channel = SettingsManager.FogChannelsChannel;
            RedChannels.Channel = SettingsManager.RedChannelsChannel;
            BlueChannels.Channel = SettingsManager.BlueChannelsChannel;
            YellowChannels.Channel = SettingsManager.YellowChannelsChannel;
            GreenChannels.Channel = SettingsManager.GreenChannelsChannel;
    }

    private void InitializeCommands()
    {
        RegisterHueBridgeCommand = ReactiveCommand.CreateFromTask(() => HueTalker.RegisterHueBridgeAsync(HueBridgeIp));
    }

    private void InitializeCollections()
    {
        ConnectedDevices = new ObservableCollection<HidDevice>();
        ByteIndexes = new ObservableCollection<DataTypes.ByteIndexModel>();
        var slot = 0;
        foreach (var name in Enum.GetNames<UdpIntake.ByteIndexName>())
        {
            ByteIndexes.Add(new DataTypes.ByteIndexModel
            {
                Name = name,
                Index = slot.ToString(),
                CurrentValue = 0,
                ValueDescription = "",
            });
            slot++;
        }

        EffectsChannelSettingsContainer = new ObservableCollection<DataTypes.IDmxChannelSetting>
        {
            FogChannels,
            StrobeChannels,
        };

        MasterDimmerSettingsContainer = new ObservableCollection<DataTypes.IDmxChannelSetting>
        {
            MasterDimmerSettings,
            MasterDimmerValues,
        };

        ColorChannelSettingsContainer = new ObservableCollection<DataTypes.IDmxChannelSetting>
        {
            RedChannels,
            BlueChannels,
            YellowChannels,
            GreenChannels,
        };

        InstrumentNoteSettingsContainer = new ObservableCollection<DataTypes.DmxSingleSetting>
        {
            GuitarNoteChannelSetting,
            BassNoteChannelSetting,
            DrumNoteChannelSetting,
            KeysNoteChannelSetting,
            VocalsNoteChannelSetting,
            Harmony0NoteChannelSetting,
            Harmony1NoteChannelSetting,
            Harmony2NoteChannelSetting,
        };

        AdvancedSettingsContainer = new ObservableCollection<DataTypes.DmxSingleSetting>
        {
            BpmChannelSetting,
            CueChangeChannelSetting,
            BeatLineChannelSetting,
            BonusEffectChannelSetting,
            KeyFrameChannelSetting,
            PostProcessingChannelSetting,
            CurrentPerformerSetting,
            CurrentSceneSetting,
            VenueSizeSetting,
            PauseStateSetting,
            SongSectionSetting,
        };

        BroadcastSettingsContainer = new ObservableCollection<DataTypes.DmxSingleSetting>
        {
            BroadcastUniverseSetting,
        };
    }

    public static void OnDeviceInserted(HidDevice device)
    {
        Dispatcher.UIThread.InvokeAsync(() => ConnectedDevices?.Add(device));
    }

    public static void OnDeviceRemoved(HidDevice device)
    {
        Dispatcher.UIThread.InvokeAsync(() => ConnectedDevices != null && ConnectedDevices.Remove(device));
    }

    public static void ClearConnectedDevices()
    {
        ConnectedDevices?.Clear();
    }

    private async void ShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        // Save the settings
        SettingsManager.SaveSettings(this);

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
