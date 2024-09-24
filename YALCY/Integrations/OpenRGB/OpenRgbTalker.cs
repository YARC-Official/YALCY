using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using OpenRGB.NET;
using YALCY.Integrations.StageKit;
using YALCY.Udp;
using YALCY.Usb;
using YALCY.ViewModels;
using Device = OpenRGB.NET.Device;

namespace YALCY.Integrations.OpenRGB;

public class OpenRgbTalker
{
    private CancellationTokenSource cts = new();
    private Task updateTask = Task.CompletedTask;

    public List<Device> OffList = new();
    public List<Device> LightPodList = new();
    public List<Device> StrobeList = new();
    public List<Device> FoggerList = new();
    public Dictionary<int, Color[]> LightPodStates = new Dictionary<int, Color[]>();
    private static string name = "YALCY";
    private static bool autoConnect = false; // can't catch exceptions in constructor
    private static int timeoutMs = 1000;
    private static uint protocolVersionNumber = 4;
    private OpenRgbClient client;

    public event Action<Device> OpenRgbDeviceInserted;
    public event Action<Device> OpenRgbDeviceRemoved;

    App app;
    MainWindowViewModel mainViewModel;

    public async Task ConnectToOpenRgbServerAsync(string serverIp, ushort serverPort)
    {
        app = (App)Application.Current!;
        mainViewModel = app.MainViewModel;

        Console.WriteLine("Connecting to OpenRGB server");

        var (isValid, statusMessage) = Helpers.IpValidator(serverIp);

        mainViewModel.OpenRgbStatus = statusMessage;
        if (!isValid)
        {
            return;
        }

        try
        {
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
                OffList.Add(device);
                OpenRgbDeviceInserted?.Invoke(device);
                mainViewModel.DeviceCategories.Add(new DeviceCategory(device, 0));
            }

            UsbDeviceMonitor.OnStageKitCommand += OnStageKitEvent;
        }
        catch (Exception ex)
        {
            mainViewModel.OpenRgbStatus = $"OpenRGB status: {ex.Message}";
        }
    }

    public async Task EnableOpenRgbTalker(bool isEnabled, string serverIP, ushort serverPort)
    {
        if (isEnabled)
        {
            await ConnectToOpenRgbServerAsync(serverIP, serverPort);
        }
        else
        {
            await cts.CancelAsync();
            try
            {
                updateTask.Wait();
            }
            catch (AggregateException ex)
            {
                foreach (var innerException in ex.InnerExceptions)
                {
                    mainViewModel.OpenRgbStatus = $"Task error: {innerException.Message}";
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
        // I thought EventsArgs would be useful to see if it was an add or remove event but not sure if
        // that is true.
        var devices = client.GetAllControllerData();
        OffList.Clear();
        Dispatcher.UIThread.InvokeAsync(MainWindowViewModel.ClearOpenRgbVisualList);
        foreach (var dev in devices)
        {
            OffList.Add(dev);
            OpenRgbDeviceInserted?.Invoke(dev);
        }
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
                foreach (var device in StrobeList)
                {
                    ToggleDeviceLeds(device, true);
                }

                await Task.Delay(interval);
                foreach (var device in StrobeList)
                {
                    ToggleDeviceLeds(device, false);
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
                    await Task.Delay(30);
                }

                for (byte brightness = 255; brightness >= 0; brightness -= 5)
                {
                    SetDeviceBrightness(brightness);
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
        foreach (var device in FoggerList)
        {
            var colors = Enumerable.Repeat(new Color(brightness, brightness, brightness), device.Leds.Length).ToArray();
            client.UpdateLeds(device.Index, colors);
        }
    }

    private void UpdateLightPodColor(byte parameter, Color color, int areaOffset)
    {
        if (LightPodList.Count == 0)
        {
            return;
        }

        const int numAreas = 32;
        foreach (var device in LightPodList)
        {
            // Adjust the number of LEDs per area, ensuring at least one LED per area
            var keysPerArea = Math.Max(1, device.Leds.Length / numAreas);
            var colors = LightPodStates[device.Index];

            for (int area = areaOffset; area < areaOffset + 8; area++)
            {
                for (int key = 0; key < keysPerArea; key++)
                {
                    var ledIndex = area * keysPerArea + key;
                    if (ledIndex >= device.Leds.Length) continue;

                    if ((parameter & (1 << (area - areaOffset))) != 0)
                    {
                        colors[ledIndex] = color;
                    }
                    else
                    {
                        colors[ledIndex] = new Color(0, 0, 0);
                    }
                }
            }

            // Update LEDs for this device
            client.UpdateLeds(device.Index, new Span<Color>(colors));
        }
    }


    private void ToggleDeviceLeds(Device device, bool turnOn)
    {
        var color = turnOn ? new Color(255, 255, 255) : new Color(0, 0, 0);
        var colors = Enumerable.Repeat(color, device.Leds.Length).ToArray();
        client.UpdateLeds(device.Index, colors);
    }

    private int CalculateDelay(int noteValue, float bpm)
    {
        return (int)(60000.0 / bpm * 4 / noteValue);
    }
}
