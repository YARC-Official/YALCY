using YALCY.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Threading;
using ReactiveUI;
using YALCY.Integrations.Lifx;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
    public LifxOptions LifxOptions { get; } = SettingsManager.LifxOptions;
    public IReadOnlyList<string> LifxTestColors => LifxPalette.Names;
    public string LifxTestColor { get; set; } = "Blue";
    private ICommand? _testLifxColorCommand;
    public ICommand TestLifxColorCommand => _testLifxColorCommand ??= ReactiveCommand.CreateFromTask(() => LifxTalker.TestColorAsync(LifxTestColor, this));
    private bool _lifxBusy;
    public bool LifxBusy { get => _lifxBusy; private set { this.RaiseAndSetIfChanged(ref _lifxBusy, value); this.RaisePropertyChanged(nameof(CanConfigureLifx)); } }
    public bool CanConfigureLifx => !LifxBusy && !LifxEnabledSetting.IsEnabled;
    public void SetLifxBusy(bool busy)
    {
        if (Application.Current?.ApplicationLifetime == null || Dispatcher.UIThread.CheckAccess()) LifxBusy = busy;
        else Dispatcher.UIThread.Post(() => LifxBusy = busy);
    }
    private string _lifxStatus = string.Empty;
    private string _lifxMessage = string.Empty;
    private List<LifxZoneAssignmentSetting> _savedLifxZoneAssignments = new();

    public ICommand DiscoverLifxDevicesCommand { get; set; } = null!;
    public ObservableCollection<LifxDeviceViewModel> LifxDevices { get; private set; } = new();

    public string LifxStatus
    {
        get => _lifxStatus;
        set
        {
            if (_lifxStatus == value) return;
            _lifxStatus = value;
            OnPropertyChanged();
        }
    }

    public string LifxMessage
    {
        get => _lifxMessage;
        set
        {
            if (_lifxMessage == value) return;
            _lifxMessage = value;
            OnPropertyChanged();
        }
    }

    private void FeedInLifxSettings()
    {
        LifxStatus = "LIFX status: Ready to set up.";
        LifxMessage = "Choose whole-room colors or advanced zone mapping, discover your lights, select and test them, then enable LIFX output.";
        LifxEnabledSetting.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EnableSetting.IsEnabled)) this.RaisePropertyChanged(nameof(CanConfigureLifx));
        };
        _savedLifxZoneAssignments = CloneAssignments(SettingsManager.LifxZoneAssignments);
    }

    private void InitializeLifxCollections()
    {
        LifxDevices = new ObservableCollection<LifxDeviceViewModel>();
    }

    public IReadOnlyList<LifxZoneAssignmentSetting> GetLifxZoneAssignments()
    {
        if (LifxDevices.Count > 0)
        {
            return MergeAssignments(_savedLifxZoneAssignments, BuildLifxZoneAssignments(LifxDevices));
        }

        return CloneAssignments(_savedLifxZoneAssignments);
    }

    public void SetLifxDevices(IReadOnlyList<LifxDeviceViewModel> devices)
    {
        void UpdateCollection()
        {
            _savedLifxZoneAssignments = GetLifxZoneAssignments().ToList();
            LifxDevices.Clear();
            foreach (var device in devices)
            {
                LifxDevices.Add(device);
            }

            if (devices.Count > 0)
            {
                _savedLifxZoneAssignments = MergeAssignments(_savedLifxZoneAssignments, BuildLifxZoneAssignments(devices));
            }
        }

        try
        {
            if (Application.Current?.ApplicationLifetime == null || Dispatcher.UIThread.CheckAccess())
            {
                UpdateCollection();
            }
            else
            {
                Dispatcher.UIThread.InvokeAsync(UpdateCollection);
            }
        }
        catch (InvalidOperationException)
        {
            UpdateCollection();
        }
    }

    public void SetLifxStatus(string status, string message)
    {
        void UpdateStatus()
        {
            LifxStatus = status;
            LifxMessage = message;
        }

        try
        {
            if (Application.Current?.ApplicationLifetime == null || Dispatcher.UIThread.CheckAccess())
            {
                UpdateStatus();
            }
            else
            {
                Dispatcher.UIThread.InvokeAsync(UpdateStatus);
            }
        }
        catch (InvalidOperationException)
        {
            UpdateStatus();
        }
    }

    private static List<LifxZoneAssignmentSetting> BuildLifxZoneAssignments(IEnumerable<LifxDeviceViewModel> devices)
    {
        return devices
            .SelectMany(device => device.Zones.Select(zone => new LifxZoneAssignmentSetting
            {
                Serial = device.Serial,
                ZoneIndex = zone.ZoneIndex,
                StageLight = zone.SelectedStageLight
            }))
            .ToList();
    }

    private static List<LifxZoneAssignmentSetting> MergeAssignments(IEnumerable<LifxZoneAssignmentSetting> saved,
        IEnumerable<LifxZoneAssignmentSetting> current) => saved.Concat(current)
        .GroupBy(a => (a.Serial.ToLowerInvariant(), a.ZoneIndex)).Select(g => g.Last()).ToList();

    private static List<LifxZoneAssignmentSetting> CloneAssignments(IEnumerable<LifxZoneAssignmentSetting> assignments)
    {
        return assignments
            .Select(assignment => new LifxZoneAssignmentSetting
            {
                Serial = assignment.Serial,
                ZoneIndex = assignment.ZoneIndex,
                StageLight = LifxStageAssignments.Normalize(assignment.StageLight)
            })
            .ToList();
    }
}

public sealed class LifxDeviceViewModel : ReactiveObject
{
    private readonly LifxOptions _options;
    private bool _isIncluded;
    private int _updateRate;
    public bool IsIncluded
    {
        get => _isIncluded;
        set
        {
            this.RaiseAndSetIfChanged(ref _isIncluded, value);
            (_options.DeviceEnabled ??= new())[Serial] = value;
        }
    }
    public int UpdateRate
    {
        get => _updateRate;
        set
        {
            this.RaiseAndSetIfChanged(ref _updateRate, Math.Clamp(value, 0, 60));
            (_options.DeviceUpdateRates ??= new())[Serial] = _updateRate;
        }
    }
    public bool IsReady { get; }
    public ICommand IdentifyCommand { get; }

    internal LifxDeviceViewModel(LifxLanDeviceModel device, LifxOptions options, LifxTalker talker)
    {
        _options = options;
        _isIncluded = options.Includes(device.Serial, device.Label, device.Address.ToString());
        _updateRate = Math.Clamp(options.DeviceUpdateRates?.GetValueOrDefault(device.Serial) ?? 0, 0, 60);
        IsReady = device.HasState;
        IdentifyCommand = ReactiveCommand.CreateFromTask(() => talker.IdentifyAsync(device.Serial));
        Label = device.Label;
        IpAddress = device.Address.ToString();
        Serial = device.Serial;
        Port = device.Port;
        PowerState = device.IsPowered ? "On" : "Off";
        ZoneCount = device.Zones.Count;
        DeviceSummary = device.HasState
            ? $"Responded to discovery | Last reported power: {PowerState} | {ZoneCount} zone(s) | {IpAddress}:{Port}"
            : $"No color/power response | {IpAddress}:{Port}  -  increase the discovery window and scan again.";
        Zones = new ObservableCollection<LifxZoneViewModel>(
            device.Zones
                .OrderBy(zone => zone.ZoneIndex)
                .Select(zone => new LifxZoneViewModel(zone, device.Label)));
    }

    public string Label { get; }
    public string IpAddress { get; }
    public string Serial { get; }
    public int Port { get; }
    public string PowerState { get; }
    public int ZoneCount { get; }
    public string DeviceSummary { get; }
    public ObservableCollection<LifxZoneViewModel> Zones { get; }
}

public sealed class LifxZoneViewModel : ReactiveObject
{
    private readonly LifxZoneModel _zone;
    private readonly string _deviceLabel;
    private string _selectedStageLight;

    internal LifxZoneViewModel(LifxZoneModel zone, string deviceLabel)
    {
        _deviceLabel = deviceLabel;
        _zone = zone;
        _selectedStageLight = LifxStageAssignments.Normalize(zone.AssignedStageLight);
    }

    public int ZoneIndex => _zone.ZoneIndex;
    public string ZoneLabel => $"Zone {_zone.ZoneIndex + 1}";
    public string CurrentState => _zone.CurrentColor.IsOn ? "On" : "Off";
    public IReadOnlyList<string> AvailableStageLights => LifxStageAssignments.AllOptions;

    public string SelectedStageLight
    {
        get => _selectedStageLight;
        set
        {
            var normalized = LifxStageAssignments.Normalize(value);
            if (_selectedStageLight == normalized)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedStageLight, normalized);
            _zone.AssignedStageLight = normalized;
            AppLog.Write(LogLevel.Information, "LIFX", $"{_deviceLabel}, {ZoneLabel}: assignment changed to {normalized}.");
        }
    }
}
