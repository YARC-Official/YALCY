using System.Collections.ObjectModel;
using Avalonia.Threading;
using HidSharp;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
    public static ObservableCollection<HidDevice>? ConnectedDevices { get; set; }
    private void InitializeUsbCollections()
    {
        ConnectedDevices = new ObservableCollection<HidDevice>();
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
}
