using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using OpenRGB.NET;
using YALCY.Integrations.StageKit;
using YALCY.Udp;
using YALCY.Usb;
using YALCY.ViewModels;
using Device = OpenRGB.NET.Device;
using YALCY.Views.Components;

namespace YALCY.Integrations.OpenRGB;

public class ZoneInfo
{
    public Device Device { get; set; } = null!;
    public int ZoneIndex { get; set; }
    public Zone Zone { get; set; } = null!;
}

public class OpenRgbTalker
{
    private CancellationTokenSource cts = new();
    private Task updateTask = Task.CompletedTask;

    // Shared lock for device/zone list and state mutations and snapshots
    public readonly object Lock = new();

    // Legacy device-based lists (kept for backward compatibility)
    public List<Device> OffList = new();
    public List<Device> LightPodList = new();
    public List<Device> StrobeList = new();
    public List<Device> FoggerList = new();
    public Dictionary<int, Color[]> LightPodStates = new Dictionary<int, Color[]>();
    
    // New zone-based dictionaries
    public Dictionary<string, ZoneInfo> OffZones = new();
    public Dictionary<string, ZoneInfo> LightPodZones = new();
    public Dictionary<string, ZoneInfo> StrobeZones = new();
    public Dictionary<string, ZoneInfo> FoggerZones = new();
    public Dictionary<string, Color[]> LightPodZoneStates = new Dictionary<string, Color[]>();
    
    private static string name = "YALCY";
    private static bool autoConnect = false; // can't catch exceptions in constructor
    private static int timeoutMs = 1000;
    private static uint protocolVersionNumber = 4;
    private OpenRgbClient client;

    public event Action<Device> OpenRgbDeviceInserted;
    public event Action<Device> OpenRgbDeviceRemoved;

    private MainWindowViewModel? _mainViewModel;

    public async Task ConnectToOpenRgbServerAsync(string serverIp, ushort serverPort, MainWindowViewModel? viewModel = null)
    {
        if (viewModel != null)
        {
            _mainViewModel = viewModel;
        }

        if (_mainViewModel == null)
        {
            Console.WriteLine("OpenRgbTalker: No ViewModel provided and none cached.");
            return;
        }

        var mainViewModel = _mainViewModel;

        Console.WriteLine("Connecting to OpenRGB server");

        var (isValid, statusMessage) = Helpers.IpValidator(serverIp);

        mainViewModel.OpenRgbStatus = statusMessage;
        if (!isValid)
        {
            return;
        }

        try
        {
            // Clear all device lists to prevent duplicates on reconnection
            lock (Lock)
            {
                OffList.Clear();
                LightPodList.Clear();
                StrobeList.Clear();
                FoggerList.Clear();
                LightPodStates.Clear();
                OffZones.Clear();
                LightPodZones.Clear();
                StrobeZones.Clear();
                FoggerZones.Clear();
                LightPodZoneStates.Clear();
            }
            
            // Clear visual lists in the UI synchronously
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                MainWindowViewModel.ClearOpenRgbVisualList();
                mainViewModel.DeviceCategories.Clear();
                mainViewModel.DevicesWithZones.Clear();
            });
            
            client = new OpenRgbClient(serverIp, serverPort, name, autoConnect, timeoutMs, protocolVersionNumber);

            // This really should be awaited since it waits for timeoutMs, however it isn't written that way.
            // I'll need to look into the OpenRGB.NET library to see if it's possible to alter this to await.
            client.Connect();

            mainViewModel.OpenRgbStatus = "OpenRGB status: Connected to OpenRGB";

            var plugins = client.GetPlugins();

            var devices = client.GetAllControllerData();

            var profiles = client.GetProfiles();

            //actual data list
            client.DeviceListUpdated += OnDeviceLisUpdate;

            //visual list
            OpenRgbDeviceInserted += MainWindowViewModel.OnOpenRgbDeviceInserted;
            OpenRgbDeviceRemoved += MainWindowViewModel.OnOpenRgbDeviceRemoved;

            foreach (var device in devices)
            {
                OpenRgbDeviceInserted?.Invoke(device);
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    mainViewModel.DeviceCategories.Add(new DeviceCategory(device, 0, mainViewModel));
                    mainViewModel.DevicesWithZones.Add(new DeviceWithZones(device, mainViewModel));
                });
            }

            UsbDeviceMonitor.OnStageKitCommand += OnStageKitEvent;
            StatusFooter.UpdateStatus("OpenRGB", IntegrationStatus.Connected);
        }
        catch (Exception ex)
        {
            mainViewModel.OpenRgbStatus = $"OpenRGB status: {ex.Message}";
            StatusFooter.UpdateStatus("OpenRGB", IntegrationStatus.Error);
        }
    }

    public async Task EnableOpenRgbTalker(bool isEnabled, string serverIP, ushort serverPort, MainWindowViewModel? viewModel = null)
    {
        if (viewModel != null)
        {
            _mainViewModel = viewModel;
        }

        if (isEnabled)
        {
            StatusFooter.UpdateStatus("OpenRGB", IntegrationStatus.Connecting);
            await ConnectToOpenRgbServerAsync(serverIP, serverPort, _mainViewModel);
        }
        else
        {
            StatusFooter.UpdateStatus("OpenRGB", IntegrationStatus.Off);
            await cts.CancelAsync();
            try
            {
                updateTask.Wait();
            }
            catch (AggregateException ex)
            {
                foreach (var innerException in ex.InnerExceptions)
                {
                    if (_mainViewModel != null)
                    {
                        _mainViewModel.OpenRgbStatus = $"Task error: {innerException.Message}";
                    }
                }
            }
            finally
            {
                cts.Dispose();
            }
        }
    }

    private void OnDeviceLisUpdate(object o, EventArgs e)
    {
        // Fetch latest controller data
        var devices = client.GetAllControllerData();

        // Clear all device/zone lists and state under lock to prevent race conditions / stale mappings
        lock (Lock)
        {
            OffList.Clear();
            LightPodList.Clear();
            StrobeList.Clear();
            FoggerList.Clear();
            LightPodStates.Clear();
            OffZones.Clear();
            LightPodZones.Clear();
            StrobeZones.Clear();
            FoggerZones.Clear();
            LightPodZoneStates.Clear();
        }

        // Marshal UI visual collection clearing and repopulation atomically to the UI thread
        Dispatcher.UIThread.InvokeAsync(() => 
        {
            MainWindowViewModel.ClearOpenRgbVisualList();
            if (_mainViewModel != null)
            {
                _mainViewModel.DeviceCategories.Clear();
                _mainViewModel.ClearDevicesWithZones();
            }
            
            foreach (var dev in devices)
            {
                OpenRgbDeviceInserted?.Invoke(dev);
                if (_mainViewModel != null)
                {
                    _mainViewModel.DeviceCategories.Add(new DeviceCategory(dev, 0, _mainViewModel));
                    _mainViewModel.DevicesWithZones.Add(new DeviceWithZones(dev, _mainViewModel));
                }
            }
        });
    }

    private void OnStageKitEvent(StageKitTalker.CommandId commandId, byte parameter)
    {
        try
        {
            switch (commandId)
            {
                case StageKitTalker.CommandId.BlueLeds:
                    UpdateLightPodColor(parameter, new Color(0, 0, 255), 0);
                    break;

                case StageKitTalker.CommandId.RedLeds:
                    UpdateLightPodColor(parameter, new Color(255, 0, 0), 8);
                    break;

                case StageKitTalker.CommandId.GreenLeds:
                    UpdateLightPodColor(parameter, new Color(0, 255, 0), 16);
                    break;

                case StageKitTalker.CommandId.YellowLeds:
                    UpdateLightPodColor(parameter, new Color(255, 255, 0), 24);
                    break;

                case StageKitTalker.CommandId.FogOn:
                    StartBreathingEffect();
                    break;

                case StageKitTalker.CommandId.FogOff:
                    StopBreathingEffect();
                    break;

                case StageKitTalker.CommandId.StrobeOff:
                    StopStrobeEffect();
                    break;

                case StageKitTalker.CommandId.StrobeSlow:
                    StartStrobeEffect(1, UdpIntake.BeatsPerMinute.Value);
                    break;

                case StageKitTalker.CommandId.StrobeMedium:
                    StartStrobeEffect(2, UdpIntake.BeatsPerMinute.Value);
                    break;

                case StageKitTalker.CommandId.StrobeFast:
                    StartStrobeEffect(3, UdpIntake.BeatsPerMinute.Value);
                    break;

                case StageKitTalker.CommandId.StrobeFastest:
                    StartStrobeEffect(4, UdpIntake.BeatsPerMinute.Value);
                    break;

                case StageKitTalker.CommandId.DisableAll:
                    StopStrobeEffect();
                    StopBreathingEffect();
                    UpdateLightPodColor(parameter, new Color(0, 0, 0), 0);
                    UpdateLightPodColor(parameter, new Color(0, 0, 0), 8);
                    UpdateLightPodColor(parameter, new Color(0, 0, 0), 16);
                    UpdateLightPodColor(parameter, new Color(0, 0, 0), 24);
                    break;


                default:
                    throw new ArgumentOutOfRangeException(nameof(commandId), commandId, null);
            }
        }
        catch (KeyNotFoundException ex)
        {
            Console.WriteLine($"Key not found: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }

    private void StartStrobeEffect(int speed, float bpm)
    {
        StopStrobeEffect();
        int interval;
        switch (speed)
        {
            case 1: // Slow (16th note)
                interval = CalculateDelay(16, bpm);
                break;
            case 2: // Medium (24th note)
                interval = CalculateDelay(24, bpm);
                break;
            case 3: // Fast (32nd note)
                interval = CalculateDelay(32, bpm);
                break;
            case 4: // Fastest (64th note)
                interval = CalculateDelay(64, bpm);
                break;
            default: // Off
                return;
        }

        cts = new CancellationTokenSource();
        updateTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                Device[] strobeDevices;
                ZoneInfo[] strobeZones;
                lock (Lock)
                {
                    strobeDevices = StrobeList.ToArray();
                    strobeZones = StrobeZones.Values.ToArray();
                }

                // Support both legacy device-based and new zone-based strobes
                foreach (var device in strobeDevices)
                {
                    ToggleDeviceLeds(device, true);
                }
                
                foreach (var zoneInfo in strobeZones)
                {
                    ToggleZoneLeds(zoneInfo, true);
                }

                await Task.Delay(interval);

                lock (Lock)
                {
                    strobeDevices = StrobeList.ToArray();
                    strobeZones = StrobeZones.Values.ToArray();
                }

                foreach (var device in strobeDevices)
                {
                    ToggleDeviceLeds(device, false);
                }
                
                foreach (var zoneInfo in strobeZones)
                {
                    ToggleZoneLeds(zoneInfo, false);
                }

                await Task.Delay(interval);
            }
        }, cts.Token);
    }

    private void StopStrobeEffect()
    {
        cts.Cancel();
    }

    private void StartBreathingEffect()
    {
        StopBreathingEffect();
        cts = new CancellationTokenSource();
        updateTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                for (byte brightness = 0; brightness <= 255; brightness += 5)
                {
                    SetDeviceBrightness(brightness);
                    SetZoneBrightness(brightness);
                    await Task.Delay(30);
                }

                for (byte brightness = 255; brightness >= 0; brightness -= 5)
                {
                    SetDeviceBrightness(brightness);
                    SetZoneBrightness(brightness);
                    await Task.Delay(30);
                }
            }
        }, cts.Token);
    }

    private void StopBreathingEffect()
    {
        cts.Cancel();
    }

    private void SetDeviceBrightness(byte brightness)
    {
        Device[] foggerDevices;
        lock (Lock)
        {
            foggerDevices = FoggerList.ToArray();
        }

        foreach (var device in foggerDevices)
        {
            var colors = Enumerable.Repeat(new Color(brightness, brightness, brightness), device.Leds.Length).ToArray();
            client.UpdateLeds(device.Index, colors);
        }
    }
    
    private void SetZoneBrightness(byte brightness)
    {
        ZoneInfo[] foggerZones;
        lock (Lock)
        {
            foggerZones = FoggerZones.Values.ToArray();
        }

        foreach (var zoneInfo in foggerZones)
        {
            var color = new Color(brightness, brightness, brightness);
            var colors = Enumerable.Repeat(color, (int)zoneInfo.Zone.LedCount).ToArray();
            client.UpdateZoneLeds(zoneInfo.Device.Index, zoneInfo.ZoneIndex, colors);
        }
    }

    private void UpdateLightPodColor(byte parameter, Color color, int areaOffset)
    {
        Device[] lightPodDevices;
        KeyValuePair<string, ZoneInfo>[] lightPodZones;
        lock (Lock)
        {
            if (LightPodList.Count == 0 && LightPodZones.Count == 0)
            {
                return;
            }
            lightPodDevices = LightPodList.ToArray();
            lightPodZones = LightPodZones.ToArray();
        }

        const int numAreas = 32;
        
        // Legacy device-based lightpods
        foreach (var device in lightPodDevices)
        {
            // Adjust the number of LEDs per area, ensuring at least one LED per area
            var keysPerArea = Math.Max(1, device.Leds.Length / numAreas);
            Color[]? colors;
            lock (Lock)
            {
                if (!LightPodStates.TryGetValue(device.Index, out colors)) continue;
            }

            for (int area = areaOffset; area < areaOffset + 8; area++)
            {
                for (int key = 0; key < keysPerArea; key++)
                {
                    var ledIndex = area * keysPerArea + key;
                    if (ledIndex >= device.Leds.Length) continue;

                    colors[ledIndex] = (parameter & (1 << (area - areaOffset))) != 0 ? color : new Color(0, 0, 0);
                }
            }

            // Update LEDs for this device
            client.UpdateLeds(device.Index, new Span<Color>(colors));
        }
        
        // New zone-based lightpods
        foreach (var kvp in lightPodZones)
        {
            var zoneInfo = kvp.Value;
            var zoneKey = kvp.Key;
            
            var zoneLedCount = (int)zoneInfo.Zone.LedCount;
            var keysPerArea = Math.Max(1, zoneLedCount / numAreas);
            Color[]? colors;
            lock (Lock)
            {
                if (!LightPodZoneStates.TryGetValue(zoneKey, out colors)) continue;
            }

            for (int area = areaOffset; area < areaOffset + 8; area++)
            {
                for (int key = 0; key < keysPerArea; key++)
                {
                    var ledIndex = area * keysPerArea + key;
                    if (ledIndex >= zoneLedCount) continue;
                    colors[ledIndex] = (parameter & (1 << (area - areaOffset))) != 0 ? color : new Color(0, 0, 0);
                }
            }

            // Update LEDs for this zone
            client.UpdateZoneLeds(zoneInfo.Device.Index, zoneInfo.ZoneIndex, new Span<Color>(colors));
        }
    }


    private void ToggleDeviceLeds(Device device, bool turnOn)
    {
        var color = turnOn ? new Color(255, 255, 255) : new Color(0, 0, 0);
        var colors = Enumerable.Repeat(color, device.Leds.Length).ToArray();
        client.UpdateLeds(device.Index, colors);
    }
    
    private void ToggleZoneLeds(ZoneInfo zoneInfo, bool turnOn)
    {
        var color = turnOn ? new Color(255, 255, 255) : new Color(0, 0, 0);
        var colors = Enumerable.Repeat(color, (int)zoneInfo.Zone.LedCount).ToArray();
        client.UpdateZoneLeds(zoneInfo.Device.Index, zoneInfo.ZoneIndex, colors);
    }

    private int CalculateDelay(int noteValue, float bpm)
    {
        return (int)(60000.0 / bpm * 4 / noteValue);
    }
}
