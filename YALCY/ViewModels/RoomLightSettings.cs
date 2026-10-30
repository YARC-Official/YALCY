using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Threading;
using ReactiveUI;
using YALCY.Integrations.RoomLights;
using YALCY.Diagnostics;
using YALCY.Views.Components;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
    public RoomLightPanelViewModel Govee { get; } = new(RoomLightKind.Govee, SettingsManager.GoveeOptions);
    public RoomLightPanelViewModel Wiz { get; } = new(RoomLightKind.Wiz, SettingsManager.WizOptions);
    public RoomLightPanelViewModel Nanoleaf { get; } = new(RoomLightKind.Nanoleaf, SettingsManager.NanoleafOptions);
    public EnableSetting GoveeEnabledSetting => Govee.EnabledSetting;
    public EnableSetting WizEnabledSetting => Wiz.EnabledSetting;
    public EnableSetting NanoleafEnabledSetting => Nanoleaf.EnabledSetting;
}

public sealed class RoomLightPanelViewModel : ReactiveObject
{
    private readonly RoomLightKind _kind;
    private bool _busy;
    private string _status = "In testing — not verified on physical hardware.";
    private RoomLightDevice? _selected;
    private readonly SemaphoreSlim _operations = new(1, 1);
    private readonly Dictionary<string, IntegrationStatus> _deviceStates = new();
    private bool _shuttingDown;
    public string Name { get; }
    public string SetupHint { get; }
    public bool IsNanoleaf => _kind == RoomLightKind.Nanoleaf;
    public bool CanDiscover => !IsNanoleaf;
    public string EmptyStateDescription => IsNanoleaf
        ? "Start by adding your controller’s IP address. Then select its row and use Pair selected above. Check Use after pairing to include it in your light show."
        : "Enable local control in the manufacturer’s app, then discover lights on your network. If discovery finds none, add an IP address manually. New lights stay unchecked until you choose Use. Disable output before adding lights.";
    public bool CanConfigure => !IsBusy && !EnabledSetting.IsEnabled;
    public bool IsBusy { get => _busy; private set { this.RaiseAndSetIfChanged(ref _busy, value); this.RaisePropertyChanged(nameof(CanConfigure)); } }
    public string Status { get => _status; private set => this.RaiseAndSetIfChanged(ref _status, value); }
    public RoomLightDevice? SelectedDevice { get => _selected; set => this.RaiseAndSetIfChanged(ref _selected, value); }
    public RoomLightOptions Options { get; }
    public RoomLightOutput Output { get; }
    public EnableSetting EnabledSetting { get; private set; } = null!;
    public string[] TestColors { get; } = { "Blue", "Red", "Green", "Yellow", "White" };
    public string TestColor { get; set; } = "Blue";
    public ICommand AddCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand DiscoverCommand { get; }
    public ICommand PairCommand { get; }
    public ICommand TestCommand { get; }
    public RoomLightPanelViewModel(RoomLightKind kind, RoomLightOptions options)
    {
        _kind = kind; Options = options; Options.Devices ??= new();
        foreach (var item in Options.Devices.Where(d => d == null).ToArray()) Options.Devices.Remove(item!);
        Output = new(kind);
        Name = kind switch { RoomLightKind.Govee => "Govee LAN", RoomLightKind.Wiz => "WiZ", _ => "Nanoleaf" };
        SetupHint = kind switch
        {
            RoomLightKind.Govee => "Enable LAN Control for each supported RGB device in Govee Home. Scan or enter its IPv4 address. Close other Govee LAN controllers if port 4002 is busy. Whole-device colors only; RGBIC segments and scene restoration are not supported.",
            RoomLightKind.Wiz => "Enable local communication in the WiZ app, then scan or enter the RGB bulb’s IPv4 address. Uses the standard local UDP API. Devices requiring signed commands are not supported yet.",
            _ => "Enter the IPv4 address of a compatible Nanoleaf Light Panels, Canvas, Shapes, or Lines controller. Select its row, hold its power button for 5–7 seconds until the indicator flashes, then click Pair selected. Whole-controller colors only; Essentials, Elements, and individual panel mapping are not supported."
        };
        Output.DeviceStatusChanged += (address, message, error) => OnUi(() =>
        {
            foreach (var device in Options.Devices.Where(d => d.Address.Trim() == address)) device.Status = message;
            AppLog.Write(error ? LogLevel.Warning : LogLevel.Information, Name, $"{address}: {message}");
            _deviceStates[address] = error ? IntegrationStatus.Error : IntegrationStatus.Connected;
            StatusFooter.UpdateStatus(Name, !EnabledSetting.IsEnabled || _shuttingDown ? IntegrationStatus.Off :
                _deviceStates.Values.Any(s => s == IntegrationStatus.Error) ? IntegrationStatus.Error :
                _deviceStates.Values.Any(s => s == IntegrationStatus.Connecting) ? IntegrationStatus.Connecting : IntegrationStatus.Connected);
        });
        AddCommand = ReactiveCommand.Create(() => { if (CanConfigure) { var item = new RoomLightDevice(); Options.Devices.Add(item); SelectedDevice = item; } });
        RemoveCommand = ReactiveCommand.Create(() => { if (CanConfigure && SelectedDevice != null) Options.Devices.Remove(SelectedDevice); });
        DiscoverCommand = ReactiveCommand.CreateFromTask(() => ConfigureAsync(async () =>
        {
            var found = await RoomLightSetup.DiscoverAsync(_kind);
            foreach (var item in found)
                if (!Options.Devices.Any(d => d.Address.Trim() == item.Address)) Options.Devices.Add(item);
            Status = $"Found {found.Count} device(s). Check the boxes for lights to control, choose assignments, then enable output. If none appear, try a manual IP address.";
        }));
        PairCommand = ReactiveCommand.CreateFromTask(() => ConfigureAsync(async () =>
        {
            var device = SelectedDevice ?? throw new ArgumentException("Select a device row to pair first.");
            device.Token = await RoomLightSetup.PairNanoleafAsync(device.Address);
            Status = "Paired. Select the device checkbox, then enable output. Save settings to keep the pairing.";
        }));
        TestCommand = ReactiveCommand.Create(() =>
        {
            if (!EnabledSetting.IsEnabled || SelectedDevice == null || !SelectedDevice.Selected)
            { Status = "Enable output and select a checked device row before testing."; return; }
            Output.Test(SelectedDevice.Address, TestColor);
            Status = "Two-second test requested. Global blackout overrides tests.";
        });
    }
    public void Initialize(bool enabled)
    {
        EnabledSetting = new EnableSetting(Name + " Enabled", enabled, Name + " enabled", Name + " disabled",
            ChangeOutputAsync, "In testing: local whole-device color output");
        EnabledSetting.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(EnableSetting.IsEnabled)) this.RaisePropertyChanged(nameof(CanConfigure)); };
    }
    public Task StartAsync() => ChangeOutputAsync(EnabledSetting.IsEnabled);
    private async Task ChangeOutputAsync(bool enabled)
    {
        await _operations.WaitAsync();
        IsBusy = true;
        try
        {
            if (_shuttingDown) return;
            _deviceStates.Clear();
            if (enabled)
                foreach (var device in Options.Devices.Where(d => d.Selected)) _deviceStates[device.Address.Trim()] = IntegrationStatus.Connecting;
            StatusFooter.UpdateStatus(Name, enabled ? IntegrationStatus.Connecting : IntegrationStatus.Off);
            await Output.EnableAsync(enabled, Options);
            Status = enabled ? "Starting selected devices; see each row for connection status." : "Output stopped.";
        }
        catch (Exception ex) { Status = ex.Message; StatusFooter.UpdateStatus(Name, IntegrationStatus.Error, Status); }
        finally { IsBusy = false; _operations.Release(); }
    }
    private async Task ConfigureAsync(Func<Task> action)
    {
        if (!CanConfigure || _shuttingDown) return;
        await _operations.WaitAsync();
        IsBusy = true;
        try { await action(); }
        catch (Exception ex) { Status = ex is System.Net.Http.HttpRequestException ? "Could not reach the device. Check its address and network." : ex.Message; }
        finally { IsBusy = false; _operations.Release(); }
    }
    public async Task StopAsync()
    {
        _shuttingDown = true;
        await _operations.WaitAsync();
        try { await Output.EnableAsync(false, Options); StatusFooter.UpdateStatus(Name, IntegrationStatus.Off); }
        finally { _operations.Release(); }
    }
    private static void OnUi(Action action)
    {
        if (Application.Current?.ApplicationLifetime == null || Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }
}
