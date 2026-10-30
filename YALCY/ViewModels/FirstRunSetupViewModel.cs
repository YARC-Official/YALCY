using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using YALCY.Diagnostics;

namespace YALCY.ViewModels;

public sealed class ProtocolSetupOption : ReactiveObject
{
    private readonly EnableSetting _setting;
    private bool _isEnabled;

    public ProtocolSetupOption(string name, string connection, string description, string setupHint, EnableSetting setting)
    {
        Name = name;
        Connection = connection;
        Description = description;
        SetupHint = setupHint;
        _setting = setting;
        _isEnabled = setting.IsEnabled;
    }

    public string Name { get; }
    public string Connection { get; }
    public string Description { get; }
    public string SetupHint { get; }
    public bool IsEnabled { get => _isEnabled; set => this.RaiseAndSetIfChanged(ref _isEnabled, value); }

    internal async Task ApplyAsync()
    {
        _setting.IsEnabled = IsEnabled;
        await _setting.PendingChange;
    }
}

public sealed class FirstRunSetupViewModel : ReactiveObject
{
    private readonly Action _save;
    private bool _isBusy;
    private string _errorMessage = "";
    public bool IsBusy { get => _isBusy; private set => this.RaiseAndSetIfChanged(ref _isBusy, value); }
    public string ErrorMessage { get => _errorMessage; private set => this.RaiseAndSetIfChanged(ref _errorMessage, value); }
    public ProtocolSetupOption YargInput { get; }
    public string Introduction { get; private set; } = "Choose how you want to light up your life!";
    public IReadOnlyList<ProtocolSetupOption> Outputs { get; }
    public string Summary => $"{Outputs.Count(option => option.IsEnabled)} of {Outputs.Count} outputs selected. YARG listening {(YargInput.IsEnabled ? "on" : "off")}.";

    public FirstRunSetupViewModel(MainWindowViewModel main, bool isFirstRun = true) : this(
        new ProtocolSetupOption("Listen to YARG", "Game input · UDP", "Receives the lighting cues sent by YARG. This is on by default.",
            "In YARG, enable the UDP data stream under experimental settings.", main.UdpEnableSetting),
        new[]
        {
            new ProtocolSetupOption("Stage Kit", "USB", "Controls Rock Band Stage Kit and FatsCo lighting, strobe, and fog hardware.", "Connect your hardware by USB. See the Stage Kit tab for device details.", main.StageKitEnabledSetting),
            new ProtocolSetupOption("Philips Hue", "Hue Bridge · local network", "Streams lighting to Philips Hue lights through a Hue Bridge <b>Entertainment</b> area.", "Pair your bridge and setup an Entertainment area in the Hue tab.", main.HueEnabledSetting),
            new ProtocolSetupOption("LIFX", "Local network · Wi-Fi", "Controls LIFX bulbs and supported zones directly on your local network, without a cloud token.", "Discover and select your lights in the LIFX tab. Whole-room mode needs no zone mapping.", main.LifxEnabledSetting),
            new ProtocolSetupOption("Govee LAN", "Local network · in testing", "Controls compatible Govee LAN lights locally. In testing; physical hardware validation is pending.", "Enable LAN Control in Govee Home, then discover and select lights in the Govee LAN tab.", main.GoveeEnabledSetting),
            new ProtocolSetupOption("WiZ", "Local network · in testing", "Controls compatible WiZ lights locally. In testing; physical hardware validation is pending.", "Enable local communication in WiZ, then discover and select RGB bulbs in the WiZ tab.", main.WizEnabledSetting),
            new ProtocolSetupOption("Nanoleaf", "Local network · in testing", "Controls compatible Nanoleaf lights locally. In testing; physical hardware validation is pending.", "Enter the controller IP and pair it in the Nanoleaf tab. Supports whole-controller colors on compatible panels, Canvas, Shapes, and Lines.", main.NanoleafEnabledSetting),
            new ProtocolSetupOption("WLED", "Local network · Wi-Fi / Ethernet", "Sends realtime colors to LED strips and controllers running WLED.", "Set the controller address, LED count, and layout in the WLED tab.", main.WledEnabledSetting),
            new ProtocolSetupOption("WLED Serial", "USB serial · Adalight", "Sends LED colors to a compatible WLED controller over USB serial instead of the network.", "Choose the serial port, baud rate, and LED count in the WLED Serial tab.", main.WledSerialEnabledSetting),
            new ProtocolSetupOption("OpenRGB", "OpenRGB SDK server", "Controls supported PC RGB devices and accessories through OpenRGB.", "Start OpenRGB's SDK server, then configure its address and device mappings in the OpenRGB tab.", main.OpenRgbEnabledSetting),
            new ProtocolSetupOption("Home Assistant", "Home Assistant server", "Controls light entities managed by your Home Assistant installation.", "Enter your server URL and access token, then select light entities in the Home Assistant tab.", main.HomeAssistantEnabledSetting),
            new ProtocolSetupOption("Glow Worm", "Local network · UDP", "Sends RGB colors to Glow Worm Luciferin lighting devices.", "Configure the destination and LED layout in the Glow Worm tab.", main.GlowWormEnabledSetting),
            new ProtocolSetupOption("DMX / sACN", "Ethernet · E1.31", "Sends DMX channel data over sACN to compatible lighting nodes and fixtures.", "Choose your network adapter, universe, and channel mappings in the DMX / Art-Net tab.", main.DmxEnabledSetting),
            new ProtocolSetupOption("Art-Net", "Ethernet · ArtDmx", "Sends DMX channel data to an Art-Net receiver using a destination IP address.", "Configure the receiver address, universe, and channel mappings in the DMX / Art-Net tab.", main.ArtNetEnabledSetting),
            new ProtocolSetupOption("Serial / USB DMX", "Serial hardware", "Sends lighting data through supported serial / USB DMX hardware.", "Check compatible hardware and channel configuration in the Serial tab.", main.SerialEnabledSetting),
            new ProtocolSetupOption("RB3E", "Network · UDP", "Forwards lighting data using YALCY's partial Rock Band 3 Enhanced protocol support.", "Use this for an RB3E-compatible receiver; see the RB3E tab for details.", main.Rb3eEnabledSetting)
        },
        () =>
        {
            var previous = SettingsManager.SetupCompleted;
            SettingsManager.SetupCompleted = true;
            try { SettingsManager.SaveSettings(main); }
            catch { SettingsManager.SetupCompleted = previous; throw; }
        })
    {
        if (!isFirstRun)
            Introduction = "Review your lighting setup. Your current protocol selections are shown below; nothing changes until you finish.";
    }

    internal FirstRunSetupViewModel(ProtocolSetupOption input, IReadOnlyList<ProtocolSetupOption> outputs, Action save)
    {
        YargInput = input;
        Outputs = outputs;
        _save = save;
        foreach (var option in outputs.Prepend(input))
            option.PropertyChanged += (_, _) => this.RaisePropertyChanged(nameof(Summary));
    }

    public async Task<bool> CompleteAsync()
    {
        if (IsBusy) return false;
        IsBusy = true;
        ErrorMessage = "";
        try
        {
            foreach (var option in Outputs.Prepend(YargInput))
            {
                try { await option.ApplyAsync(); }
                catch (Exception ex)
                {
                    // Keep the user's enable choice: pairing/address configuration
                    // may still be needed in that protocol's tab.
                    AppLog.Write(LogLevel.Error, option.Name, $"Setup could not start output: {ex.Message}");
                }
            }
            _save();
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Write(LogLevel.Error, "Settings", $"Could not save first-run setup: {ex.Message}");
            ErrorMessage = "Your choices could not be saved. Check access to your settings folder, then try again. Details are in the Log panel.";
            return false;
        }
        finally { IsBusy = false; }
    }
}
