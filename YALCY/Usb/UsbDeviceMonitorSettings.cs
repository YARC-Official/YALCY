using System;
using System.Collections.ObjectModel;
using System.IO.Enumeration;
using System.Linq;
using Avalonia.Threading;
using HidSharp;
using HidSharp.Experimental;
using ReactiveUI;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
    private ObservableCollection<HidDevice> _stageKitConnectedDevices = new();
    public ObservableCollection<HidDevice> StageKitConnectedDevices
    {
        get => _stageKitConnectedDevices;
        set => this.RaiseAndSetIfChanged(ref _stageKitConnectedDevices, value);
    }

    private ObservableCollection<SerialDeviceViewModel> _serialConnectedDevices = new();
    public ObservableCollection<SerialDeviceViewModel> SerialConnectedDevices
    {
        get => _serialConnectedDevices;
        set => this.RaiseAndSetIfChanged(ref _serialConnectedDevices, value);
    }

    private ObservableCollection<BleDevice> _bleConnectedDevices = new();
    public ObservableCollection<BleDevice> BleConnectedDevices
    {
        get => _bleConnectedDevices;
        set => this.RaiseAndSetIfChanged(ref _bleConnectedDevices, value);
    }

    private void InitializeUsbCollections()
    {
        StageKitConnectedDevices = new ObservableCollection<HidDevice>();
        SerialConnectedDevices = new ObservableCollection<SerialDeviceViewModel>();
        BleConnectedDevices = new ObservableCollection<BleDevice>();
    }

    public void OnDeviceInserted(Device device)
    {
        if (device is HidDevice hidDevice)
        {
            Dispatcher.UIThread.InvokeAsync(() => StageKitConnectedDevices?.Add(hidDevice));
        }
        else if (device is SerialDevice serialDevice)
        {
            Dispatcher.UIThread.InvokeAsync(() => SerialConnectedDevices?.Add(new SerialDeviceViewModel(serialDevice)));
            //Dispatcher.UIThread.InvokeAsync(() => SerialConnectedDevices?.Add(serialDevice));
        }
        else if (device is BleDevice bleDevice)
        {
            Dispatcher.UIThread.InvokeAsync(() => BleConnectedDevices?.Add(bleDevice));
        }
    }

    public void OnDeviceRemoved(Device device)
    {
        if (device is HidDevice hidDevice)
        {
            Dispatcher.UIThread.InvokeAsync(() => StageKitConnectedDevices != null && StageKitConnectedDevices.Remove(hidDevice));
        }
        else if (device is SerialDevice serialDevice)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                var toRemove = SerialConnectedDevices.FirstOrDefault(d => d.DevicePath == serialDevice.DevicePath);
                if (toRemove != null)
                {
                    SerialConnectedDevices.Remove(toRemove);
                }
            });
        }
        else if (device is BleDevice bleDevice)
        {
            Dispatcher.UIThread.InvokeAsync(() => BleConnectedDevices != null && BleConnectedDevices.Remove(bleDevice));
        }
    }
}
