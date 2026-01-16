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

    private static readonly object DeviceStateLock = new();
    private readonly SemaphoreSlim _updateSemaphore = new(1, 1);
    private CancellationTokenSource? _updateCts;
    private EventHandler<DeviceListChangedEventArgs>? _changedHandler;

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
        _changedHandler = (sender, e) => OnDeviceListChanged();
        _list.Changed += _changedHandler;

        //actual device list
        //_list.Changed += (sender, e) => OnDeviceListChanged();

        OnDeviceListChanged();
    }

    public void StopUsbDeviceMonitor()
    {
        // Access the MainViewModel instance, can't assume it was set in enable
        var app = (App)Application.Current!;
        var mainViewModel = app.MainViewModel;

        DeviceInserted -= mainViewModel.OnDeviceInserted;
        DeviceRemoved -= mainViewModel.OnDeviceRemoved;

            if (_changedHandler != null) _list.Changed -= _changedHandler;

            lock (DeviceStateLock)
            {
                _connectedHidDevices.Clear();
                _connectedControllerIndices.Clear(); // Clear XInput controllers
            }

        _updateCts?.Cancel();
        _updateCts?.Dispose();
    }

    private void OnDeviceListChanged()
    {
        Console.WriteLine("Device list changed, waiting for update...");

        _updateCts?.Cancel();
        _updateCts?.Dispose();
        _updateCts = new CancellationTokenSource();
        var token = _updateCts.Token;
        _ = Task.Run(() => RefreshDevicesAsync(token), token);
    }

    private async Task RefreshDevicesAsync(CancellationToken token)
    {
        var acquired = false;
        try
        {
            await _updateSemaphore.WaitAsync(token);
            acquired = true;
            await Task.Delay(300, token); // Give Windows time to update the device list

            var newHidDevices = DeviceList.Local.GetHidDevices().ToList();
            var newSerialDevices = DeviceList.Local.GetSerialDevices().ToList();
            var newBleDevices = DeviceList.Local.GetBleDevices().ToList();

            List<HidDevice> previousHidDevices;
            List<SerialDevice> previousSerialDevices;
            List<BleDevice> previousBleDevices;

            lock (DeviceStateLock)
            {
                previousHidDevices = _perviousHidDevices.ToList();
                previousSerialDevices = _perviousSerialDevices.ToList();
                previousBleDevices = _perviousBLEDevices.ToList();
            }

            var removedSerialDevices = previousSerialDevices
                .Where(oldDev => !newSerialDevices.Any(dev => dev.DevicePath == oldDev.DevicePath))
                .ToList();
            var removedHidDevices = previousHidDevices
                .Where(oldDev => !newHidDevices.Any(dev => dev.DevicePath == oldDev.DevicePath))
                .ToList();
            var removedBleDevices = previousBleDevices
                .Where(oldDev => !newBleDevices.Any(dev => dev.DevicePath == oldDev.DevicePath))
                .ToList();

            var addedSerialDevices = newSerialDevices
                .Where(newDev => !previousSerialDevices.Any(dev => dev.DevicePath == newDev.DevicePath))
                .ToList();
            var addedHidDevices = newHidDevices
                .Where(newDev => !previousHidDevices.Any(dev => dev.DevicePath == newDev.DevicePath))
                .Where(IsStageKitHidDevice)
                .ToList();
            var addedBleDevices = newBleDevices
                .Where(newDev => !previousBleDevices.Any(dev => dev.DevicePath == newDev.DevicePath))
                .ToList();

            lock (DeviceStateLock)
            {
                _perviousHidDevices = newHidDevices;
                _perviousSerialDevices = newSerialDevices;
                _perviousBLEDevices = newBleDevices;
                _connectedHidDevices = newHidDevices.Where(IsStageKitHidDevice).ToList();
                _connectedSerialDevices = newSerialDevices;
                _connectedBLEDevices = newBleDevices;
            }

#if WINDOWS
            UpdateConnectedXInputControllers();
#endif

            if (token.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var oldDev in removedSerialDevices)
                {
                    Console.WriteLine("Serial device removed");
                    DeviceRemoved?.Invoke(oldDev);
                }

                foreach (var oldDev in removedHidDevices)
                {
                    Console.WriteLine("HID device removed");
                    DeviceRemoved?.Invoke(oldDev);
                }

                foreach (var oldDev in removedBleDevices)
                {
                    Console.WriteLine("BLE device removed");
                    DeviceRemoved?.Invoke(oldDev);
                }

                foreach (var newDev in addedSerialDevices)
                {
                    Console.WriteLine("Serial device added");
                    SerialDeviceAdded?.Invoke(newDev); //this is mostly for the serial talker watch dog
                    DeviceInserted?.Invoke(newDev);
                }

                foreach (var newDev in addedHidDevices)
                {
                    Console.WriteLine("HID device added");
                    DeviceInserted?.Invoke(newDev);
                }

                foreach (var newDev in addedBleDevices)
                {
                    Console.WriteLine("BLE device added");
                    DeviceInserted?.Invoke(newDev);
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Swallow cancellations caused by device churn.
        }
        finally
        {
            if (acquired)
            {
                _updateSemaphore.Release();
            }
        }
    }

    public static void SendReport(StageKitTalker.CommandId commandId, byte parameter)
    {
        OnStageKitCommand?.Invoke(commandId, parameter);

#if WINDOWS
        List<int> controllerIndices;
        lock (DeviceStateLock)
        {
            controllerIndices = _connectedControllerIndices.ToList();
        }

        foreach (var controllerIndex in controllerIndices) // Only vibrate connected controllers
        {
            SetXInputVibration(controllerIndex, parameter, (byte)commandId);
        }
#endif

        List<HidDevice> hidDevices;
        lock (DeviceStateLock)
        {
            hidDevices = _connectedHidDevices.ToList();
        }

        foreach (var device in hidDevices)
        {
            if (!IsStageKitHidDevice(device))
            {
                continue;
            }

            var report = new byte[4];
            report[0] = 0x01;
            report[1] = 0x5A;
            report[2] = parameter;
            report[3] = (byte)commandId;

            try
            {
                using var stream = device.Open();
                stream.Write(report);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send HID report: {ex.Message}");
                lock (DeviceStateLock)
                {
                    _connectedHidDevices.RemoveAll(connected => connected.DevicePath == device.DevicePath);
                    DeviceRemoved?.Invoke(device);
                }
            }
        }
    }

    private static bool IsStageKitHidDevice(HidDevice device)
    {
        return (device.VendorID == 0x1209 && device.ProductID == 0x2882 && device.ReleaseNumberBcd == 0x0900)
               || (device.VendorID == 0x0E6F && device.ProductID == 0x0103);
    }
#if WINDOWS
    private void UpdateConnectedXInputControllers()
    {
        Console.WriteLine("Updating connected XInput controllers...");
        lock (DeviceStateLock)
        {
            _connectedControllerIndices.Clear();
        }

        for (int i = 0; i < 4; i++) // Check up to 4 controllers
        {
            XINPUT_STATE state = new XINPUT_STATE();
            int result = XInputGetState(i, ref state);

            if (result == 0) // Controller is connected
            {
                lock (DeviceStateLock)
                {
                    _connectedControllerIndices.Add(i);
                }
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
#endif
}
