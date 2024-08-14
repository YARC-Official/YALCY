using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using HidSharp;
using YALCY.Integrations.StageKit;
using YALCY.ViewModels;

namespace YALCY.Usb;

public class UsbDeviceMonitor
{
    private static List<HidDevice> _connectedDevices = new();
    private DeviceList _list = DeviceList.Local;

    public event Action<HidDevice> DeviceInserted;
    public event Action<HidDevice> DeviceRemoved;
    public static event Action<StageKitTalker.CommandId, byte> OnStageKitCommand;

    public void StartUsbDeviceMonitor()
    {
        //visual list
        DeviceInserted += MainWindowViewModel.OnDeviceInserted;
        DeviceRemoved += MainWindowViewModel.OnDeviceRemoved;

        //actual device list
        _list.Changed += (sender, e) => OnDeviceListChanged(e);

        var hidDeviceList = _list.GetHidDevices().ToArray();
        _connectedDevices.Clear();
        foreach (var dev in hidDeviceList)
        {
            AddToList(dev);
        }
    }

    public void StopUsbDeviceMonitor()
    {
        DeviceInserted -= MainWindowViewModel.OnDeviceInserted;
        DeviceRemoved -= MainWindowViewModel.OnDeviceRemoved;
        _connectedDevices.Clear();
    }

    private void OnDeviceListChanged(DeviceListChangedEventArgs e)
    {
        // I thought DeviceListChangedEventsArgs would be useful to see if it was an add or remove event but not sure if
        // that is true.
        var hidDeviceList = DeviceList.Local.GetHidDevices().ToArray();
        _connectedDevices.Clear();
        Dispatcher.UIThread.InvokeAsync(MainWindowViewModel.ClearUsbConnectedDevicesVisualList);
        foreach (var dev in hidDeviceList)
        {
            AddToList(dev);
        }
    }

    public static void SendReport(StageKitTalker.CommandId commandId, byte parameter)
    {
        OnStageKitCommand?.Invoke(commandId, parameter);

        foreach (var device in _connectedDevices)
        {
            byte[] report;
            if (device.VendorID == 0x1209) //santroller
            {
                report = new byte[4];
                report[0] = 0x01;
                report[1] = 0x5A;
                report[2] = parameter;
                report[3] = (byte)commandId;
            }
            else //stage kit
            {
                report = new byte[8];
                report[0] = 0x00;
                report[1] = 0x08;
                report[2] = 0x00;
                report[3] = parameter;
                report[4] = (byte)commandId;
                report[5] = 0x00;
                report[6] = 0x00;
                report[7] = 0x00;
            }

            var stream = device.Open();
            stream.Write(report);
            stream.Close();
        }
    }

    private void AddToList(HidDevice dev)
    {
        if ((dev.VendorID != 0x1209 || dev.ProductID != 0x2882 || dev.ReleaseNumberBcd != 0x0900) &&
            (dev.VendorID == 0x0E6F || dev.ProductID != 0x0103)) return;
        _connectedDevices.Add(dev);
        DeviceInserted?.Invoke(dev);
    }
}
