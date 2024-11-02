using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using HidSharp;
using YALCY.Integrations.StageKit;
using YALCY.ViewModels;

namespace YALCY.Usb;

public class UsbDeviceMonitor
{
    private static List<HidDevice> _connectedDevices = new();
    private DeviceList _list = DeviceList.Local;
    private static List<int> _connectedControllerIndices = new(); // Store connected XInput controller indices

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

        UpdateConnectedXInputControllers(); // Detect connected XInput controllers
    }

    public void StopUsbDeviceMonitor()
    {
        DeviceInserted -= MainWindowViewModel.OnDeviceInserted;
        DeviceRemoved -= MainWindowViewModel.OnDeviceRemoved;
        _connectedDevices.Clear();
        _connectedControllerIndices.Clear(); // Clear XInput controllers
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
        UpdateConnectedXInputControllers(); // Update XInput controller list on device change
    }

    private void AddToList(HidDevice dev)
    {
        if ((dev.VendorID != 0x1209 || dev.ProductID != 0x2882 || dev.ReleaseNumberBcd != 0x0900) && (dev.VendorID != 0x0E6F || dev.ProductID != 0x0103)) return;
        _connectedDevices.Add(dev);
        DeviceInserted?.Invoke(dev);
    }

    public static void SendReport(StageKitTalker.CommandId commandId, byte parameter)
    {
        OnStageKitCommand?.Invoke(commandId, parameter);

        foreach (var controllerIndex in _connectedControllerIndices) // Only vibrate connected controllers
        {
            SetXInputVibration(controllerIndex, parameter, (byte)commandId);
        }

        foreach (var device in _connectedDevices)
        {
            byte[] report;
            if (device.VendorID == 0x1209 && device.ProductID == 0x2882 && device.ReleaseNumberBcd == 0x0900) //santroller sage kit
            {
                report = new byte[4];
                report[0] = 0x01;
                report[1] = 0x5A;
                report[2] = parameter;
                report[3] = (byte)commandId;

                var stream = device.Open();
                stream.Write(report);
                stream.Close();
            }
        }
    }

    private void UpdateConnectedXInputControllers()
    {
        _connectedControllerIndices.Clear();

        for (int i = 0; i < 4; i++) // Check up to 4 controllers
        {
            XINPUT_STATE state = new XINPUT_STATE();
            int result = XInputGetState(i, ref state);

            if (result == 0) // Controller is connected
            {
                _connectedControllerIndices.Add(i);
            }
        }
    }

    private static void SetXInputVibration(int controllerIndex, byte leftMotor, byte rightMotor)
    {
        XINPUT_VIBRATION vibration = new XINPUT_VIBRATION
        {
            wLeftMotorSpeed = (ushort)(leftMotor << 8), // Left-shift by 8 bits
            wRightMotorSpeed = (ushort)(rightMotor << 8) // Left-shift by 8 bits
        };
        XInputSetState(controllerIndex, ref vibration);
    }

    [DllImport("XInput1_4.dll", EntryPoint = "XInputSetState")]
    private static extern int XInputSetState(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

    [DllImport("XInput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState(int dwUserIndex, ref XINPUT_STATE pState);

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_VIBRATION
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }
}
