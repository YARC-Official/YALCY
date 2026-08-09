using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using YALCY.Integrations.Lifx;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
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
        LifxStatus = "LIFX status: Currently doing nothing.";
        LifxMessage = string.Empty;
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
            return BuildLifxZoneAssignments(LifxDevices);
        }

        return CloneAssignments(_savedLifxZoneAssignments);
    }

    public void SetLifxDevices(IReadOnlyList<LifxDeviceViewModel> devices)
    {
        void UpdateCollection()
        {
            LifxDevices.Clear();
            foreach (var device in devices)
            {
                LifxDevices.Add(device);
            }

            if (devices.Count > 0)
            {
                _savedLifxZoneAssignments = BuildLifxZoneAssignments(devices);
            }
        }

        try
        {
            if (Dispatcher.UIThread.CheckAccess())
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
            if (Dispatcher.UIThread.CheckAccess())
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

public sealed class LifxDeviceViewModel
{
    internal LifxDeviceViewModel(LifxLanDeviceModel device)
    {
        Label = device.Label;
        IpAddress = device.Address.ToString();
        Serial = device.Serial;
        Port = device.Port;
        PowerState = device.IsPowered ? "On" : "Off";
        ZoneCount = device.Zones.Count;
        DeviceSummary = $"{PowerState} | {ZoneCount} zone(s) | {IpAddress}:{Port}";
        Zones = new ObservableCollection<LifxZoneViewModel>(
            device.Zones
                .OrderBy(zone => zone.ZoneIndex)
                .Select(zone => new LifxZoneViewModel(zone)));
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
    private string _selectedStageLight;

    internal LifxZoneViewModel(LifxZoneModel zone)
    {
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
        }
    }
}
