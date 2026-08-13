using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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

    private static List<HidDevice> _previousHidDevices = new();
    private static List<SerialDevice> _previousSerialDevices = new();
    private static List<BleDevice> _previousBLEDevices = new();

    private static List<HidDevice> _connectedHidDevices = new();
    private static List<SerialDevice> _connectedSerialDevices = new();
    private static List<BleDevice> _connectedBLEDevices = new();

    private static List<int> _connectedXInputStageKitIndices = new();
    private static int _outputSuppressed;

    public static Action<Device> DeviceInserted;
    public static Action<Device> DeviceRemoved;
    public static Action<SerialDevice> SerialDeviceAdded;

    public static event Action<StageKitTalker.CommandId, byte> OnStageKitCommand;

    internal static bool IsOutputSuppressed => Volatile.Read(ref _outputSuppressed) != 0;

    private MainWindowViewModel? _mainViewModel;
    private bool _isRunning;

    public void StartUsbDeviceMonitor(MainWindowViewModel? viewModel = null)
    {
        if (viewModel != null)
        {
            _mainViewModel = viewModel;
        }

        if (_mainViewModel != null)
        {
            //visual list
            DeviceInserted += _mainViewModel.OnDeviceInserted;
            DeviceRemoved += _mainViewModel.OnDeviceRemoved;
        }

        //actual device list
        _changedHandler = (sender, e) => OnDeviceListChanged();
        _list.Changed += _changedHandler;

        _isRunning = true;
        OnDeviceListChanged();
    }

    public void StopUsbDeviceMonitor()
    {
        if (!_isRunning) return;
        _isRunning = false;

        if (_mainViewModel != null)
        {
            DeviceInserted -= _mainViewModel.OnDeviceInserted;
            DeviceRemoved -= _mainViewModel.OnDeviceRemoved;
        }

        if (_changedHandler != null) _list.Changed -= _changedHandler;

        lock (DeviceStateLock)
        {
            _connectedHidDevices.Clear();
            _connectedXInputStageKitIndices.Clear();
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
                previousHidDevices = _previousHidDevices.ToList();
                previousSerialDevices = _previousSerialDevices.ToList();
                previousBleDevices = _previousBLEDevices.ToList();
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
                _previousHidDevices = newHidDevices;
                _previousSerialDevices = newSerialDevices;
                _previousBLEDevices = newBleDevices;
                _connectedHidDevices = newHidDevices.Where(IsStageKitHidDevice).ToList();
                _connectedSerialDevices = newSerialDevices;
                _connectedBLEDevices = newBleDevices;
            }

#if WINDOWS
            UpdateConnectedXInputStageKits();
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
        if (IsOutputSuppressed)
        {
            return;
        }

        DispatchReport(commandId, parameter);
    }

    internal static void EnterSafetyBlackout()
    {
        if (Interlocked.Exchange(ref _outputSuppressed, 1) != 0)
        {
            return;
        }

        DispatchReport(StageKitTalker.CommandId.DisableAll, 0);
    }

    internal static void ExitSafetyBlackout()
    {
        Interlocked.Exchange(ref _outputSuppressed, 0);
    }

    private static void DispatchReport(StageKitTalker.CommandId commandId, byte parameter)
    {
        var subscribers = OnStageKitCommand?.GetInvocationList();
        if (subscribers != null)
        {
            foreach (var subscriber in subscribers)
            {
                try
                {
                    ((Action<StageKitTalker.CommandId, byte>)subscriber)(commandId, parameter);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Stage Kit command subscriber failed: {ex.Message}");
                }
            }
        }

#if WINDOWS
        List<int> stageKitIndices;
        lock (DeviceStateLock)
        {
            stageKitIndices = _connectedXInputStageKitIndices.ToList();
        }

        foreach (var stageKitIndex in stageKitIndices)
        {
            SendXInputStageKitReport(stageKitIndex, parameter, (byte)commandId);
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
    private const byte XInputDeviceSubtypeStageKit = 0x09;
    private const int XInputSuccess = 0;
    private const int XInputMaxControllers = 4;

    private void UpdateConnectedXInputStageKits()
    {
        Console.WriteLine("Updating connected XInput Stage Kits...");
        var stageKitIndices = new List<int>();

        for (var controllerIndex = 0; controllerIndex < XInputMaxControllers; controllerIndex++)
        {
            var capabilities = new XINPUT_CAPABILITIES();
            var result = XInputGetCapabilities(controllerIndex, 0, ref capabilities);

            if (result == XInputSuccess && capabilities.SubType == XInputDeviceSubtypeStageKit)
            {
                stageKitIndices.Add(controllerIndex);
            }
        }

        lock (DeviceStateLock)
        {
            _connectedXInputStageKitIndices = stageKitIndices;
        }
    }

    private static void SendXInputStageKitReport(int controllerIndex, byte parameter, byte commandId)
    {
        var report = new XINPUT_VIBRATION
        {
            wLeftMotorSpeed = (ushort)(parameter << 8),
            wRightMotorSpeed = (ushort)(commandId << 8)
        };
        XInputSetState(controllerIndex, ref report);
    }

    [DllImport("XInput1_4.dll", EntryPoint = "XInputSetState")]
    private static extern int XInputSetState(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

    [DllImport("XInput1_4.dll", EntryPoint = "XInputGetCapabilities")]
    private static extern int XInputGetCapabilities(
        int dwUserIndex,
        uint dwFlags,
        ref XINPUT_CAPABILITIES pCapabilities);

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_CAPABILITIES
    {
        public byte Type;
        public byte SubType;
        public ushort Flags;
        public XINPUT_GAMEPAD Gamepad;
        public XINPUT_VIBRATION Vibration;
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
