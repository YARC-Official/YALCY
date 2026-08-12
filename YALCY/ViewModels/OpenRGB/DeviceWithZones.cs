using System.Collections.ObjectModel;
using System.Windows.Input;
using OpenRGB.NET;
using ReactiveUI;

namespace YALCY.ViewModels.OpenRGB;

public class DeviceWithZones : ReactiveObject
{
    public Device Device { get; set; }
    public ObservableCollection<DeviceZoneCategory> Zones { get; set; }
    public ICommand IdentifyDeviceCommand { get; }

    public DeviceWithZones(Device device, MainWindowViewModel? viewModel = null)
    {
        Device = device;
        Zones = new ObservableCollection<DeviceZoneCategory>();
        IdentifyDeviceCommand = ReactiveCommand.Create(() => viewModel?.OpenRgbTalker.IdentifyDevice(Device));

        // Create a zone category for each zone in the device
        for (int i = 0; i < device.Zones.Length; i++)
        {
            var zone = device.Zones[i];
            Zones.Add(new DeviceZoneCategory(device, zone, i, 0, viewModel));
        }
    }
}
