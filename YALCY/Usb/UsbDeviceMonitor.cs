using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using HidSharp;
using HidSharp.Experimental;
using YALCY.Integrations.StageKit;
using YALCY.ViewModels;

namespace YALCY.Usb;

public class UsbDeviceMonitor
{
    private DeviceList _list = DeviceList.Local; //complete device list

    private static List<HidDevice> _perviousHidDevices = new();
    private static List<SerialDevice> _perviousSerialDevices = new();
    private static List<BleDevice> _perviousBLEDevices = new();

    private static List<HidDevice> _connectedHidDevices = new();
    private static List<SerialDevice> _connectedSerialDevices = new();
    private static List<BleDevice> _connectedBLEDevices = new();

    private static List<int> _connectedControllerIndices = new(); // Store connected XInput controller indices

    public static Action<Device> DeviceInserted;
    public static Action<Device> DeviceRemoved;
    public static Action<SerialDevice> SerialDeviceAdded;

    public static event Action<StageKitTalker.CommandId, byte> OnStageKitCommand;

    public void StartUsbDeviceMonitor()
    {
        // Access the MainViewModel instance, can't assume it was set in enable
        var app = (App)Application.Current!;
        var mainViewModel = app.MainViewModel;

        //visual list
        DeviceInserted += mainViewModel.OnDeviceInserted;
        DeviceRemoved += mainViewModel.OnDeviceRemoved;

        //actual device list
        _list.Changed += (sender, e) => OnDeviceListChanged();

        OnDeviceListChanged();
    }

    public void StopUsbDeviceMonitor()
    {
        // Access the MainViewModel instance, can't assume it was set in enable
        var app = (App)Application.Current!;
        var mainViewModel = app.MainViewModel;

        DeviceInserted -= mainViewModel.OnDeviceInserted;
        DeviceRemoved -= mainViewModel.OnDeviceRemoved;
        _connectedHidDevices.Clear();
        _connectedControllerIndices.Clear(); // Clear XInput controllers
    }

    private void OnDeviceListChanged()
    {
        Console.WriteLine("Device list changed, waiting for update...");

        Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(1000); // Give Windows time to update the device list

            var newHidDevices = DeviceList.Local.GetHidDevices().ToList();
            var newSerialDevices = DeviceList.Local.GetSerialDevices().ToList();
            var newBleDevices = DeviceList.Local.GetBleDevices().ToList();

            // Detect removed devices
            foreach (var oldDev in _perviousSerialDevices)
            {
                if (!newSerialDevices.Any(dev => dev.DevicePath == oldDev.DevicePath))
                {
                    Console.WriteLine("Serial device removed");
                    DeviceRemoved?.Invoke(oldDev);
                }
            }

            foreach (var oldDev in _perviousHidDevices)
            {
                if (!newHidDevices.Any(dev => dev.DevicePath == oldDev.DevicePath))
                {
                    Console.WriteLine("HID device removed");
                    DeviceRemoved?.Invoke(oldDev);
                }
            }

            foreach (var oldDev in _perviousBLEDevices)
            {
                if (!newBleDevices.Any(dev => dev.DevicePath == oldDev.DevicePath))
                {
                    Console.WriteLine("BLE device removed");
                    DeviceRemoved?.Invoke(oldDev);
                }
            }

            // Detect added devices
            foreach (var newDev in newSerialDevices)
            {
                if (!_perviousSerialDevices.Any(dev => dev.DevicePath == newDev.DevicePath))
                {
                    Console.WriteLine("Serial device added");
                    SerialDeviceAdded?.Invoke(newDev); //this is mostly for the serial talker watch dog
                    DeviceInserted?.Invoke(newDev);
                }
            }

            foreach (var newDev in newHidDevices)
            {
                if (!_perviousHidDevices.Any(dev => dev.DevicePath == newDev.DevicePath))
                {
                    if ((newDev.VendorID != 0x1209 || newDev.ProductID != 0x2882 || newDev.ReleaseNumberBcd != 0x0900) && (newDev.VendorID != 0x0E6F || newDev.ProductID != 0x0103)) continue;
                    Console.WriteLine("HID device added");
                    DeviceInserted?.Invoke(newDev);
                }
            }

            foreach (var newDev in newBleDevices)
            {
                if (!_perviousBLEDevices.Any(dev => dev.DevicePath == newDev.DevicePath))
                {
                    Console.WriteLine("BLE device added");
                    DeviceInserted?.Invoke(newDev);
                }
            }

            // Update the tracked device lists
            _perviousHidDevices = newHidDevices;
            _perviousSerialDevices = newSerialDevices;
            _perviousBLEDevices = newBleDevices;

            UpdateConnectedXInputControllers();

        });
    }

    public static void SendReport(StageKitTalker.CommandId commandId, byte parameter)
    {
        OnStageKitCommand?.Invoke(commandId, parameter);

        foreach (var controllerIndex in _connectedControllerIndices) // Only vibrate connected controllers
        {
            SetXInputVibration(controllerIndex, parameter, (byte)commandId);
        }

        foreach (var device in _connectedHidDevices)
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
