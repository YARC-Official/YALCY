using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using OpenRGB.NET;
using ReactiveUI;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
    private string? _openRgbServerIp;
    private ushort _openRgbServerPort;
    private string _openRgbStatus = null!;
    private static ObservableCollection<Device>? OpenRgbDevices { get; set; }
    public ICommand ConnectToOpenRgbServerCommand { set; get; } = null!;
    public ObservableCollection<DeviceCategory> DeviceCategories { get; set; } = null!;
    public ObservableCollection<DeviceWithZones> DevicesWithZones { get; set; } = null!;

    public string OpenRgbStatus
    {
        get => _openRgbStatus;
        set
        {
            if (_openRgbStatus == value) return;
            _openRgbStatus = value;
            OnPropertyChanged();
        }
    }

    public string? OpenRgbServerIp
    {
        get => _openRgbServerIp;
        set
        {
            if (_openRgbServerIp == value) return;
            _openRgbServerIp = value;
            OnPropertyChanged();
        }
    }

    public ushort OpenRgbServerPort
    {
        get => _openRgbServerPort;
        set => this.RaiseAndSetIfChanged(ref _openRgbServerPort, value);
    }


    private void FeedInOpenRgbSettings()
    {
        OpenRgbStatus = "OpenRGB Status: Currently doing nothing.";
        OpenRgbServerIp = SettingsManager.OpenRgbServerIp;
        OpenRgbServerPort = SettingsManager.OpenRgbServerPort;
    }

    private void InitializeOpenRgbCollections()
    {
        OpenRgbDevices = new ObservableCollection<Device>();
        DeviceCategories = new ObservableCollection<DeviceCategory>();
        DevicesWithZones = new ObservableCollection<DeviceWithZones>();
    }

    public static void OnOpenRgbDeviceInserted(Device device)
    {
        Dispatcher.UIThread.InvokeAsync(() => OpenRgbDevices?.Add(device));
    }

    public static void OnOpenRgbDeviceRemoved(Device device)
    {
        Dispatcher.UIThread.InvokeAsync(() => OpenRgbDevices != null && OpenRgbDevices.Remove(device));
    }

    public static void ClearOpenRgbVisualList()
    {
        OpenRgbDevices?.Clear();
    }
    
    public void ClearDevicesWithZones()
    {
        DevicesWithZones?.Clear();
    }
}
