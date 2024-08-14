using System.Collections.ObjectModel;
using Avalonia.Threading;
using HidSharp;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
    public static ObservableCollection<HidDevice>? UsbConnectedDevices { get; set; }

    private static void InitializeUsbCollections()
    {
        UsbConnectedDevices = new ObservableCollection<HidDevice>();
    }

    public static void OnDeviceInserted(HidDevice device)
    {
        Dispatcher.UIThread.InvokeAsync(() => UsbConnectedDevices?.Add(device));
    }

    public static void OnDeviceRemoved(HidDevice device)
    {
        Dispatcher.UIThread.InvokeAsync(() => UsbConnectedDevices != null && UsbConnectedDevices.Remove(device));
    }

    public static void ClearUsbConnectedDevicesVisualList()
    {
        UsbConnectedDevices?.Clear();
    }
}
