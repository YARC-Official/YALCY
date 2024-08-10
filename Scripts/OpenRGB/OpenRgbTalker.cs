using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using OpenRGB.NET;
using YALCY.ViewModels;

namespace YALCY.Scripts.OpenRGB
{
    public class OpenRgbTalker
    {
        private CancellationTokenSource cts = new();
        private Task updateTask = Task.CompletedTask;

        List<Device> Keyboards = new List<Device>();
        List<Device> Towers = new List<Device>();
        List<Device> Extras = new List<Device>();
        private Dictionary<int, Color[]> keyboardStates = new Dictionary<int, Color[]>();

        private static string ip = "127.0.0.1"; // OpenRGB server IP
        private static int port = 6742;
        private static string name = "YALCY";
        private static bool autoConnect = false; // can't catch exceptions in constructor
        private static int timeoutMs = 1000;
        private static uint protocolVersionNumber = 4;
        private OpenRgbClient client = new OpenRgbClient(ip, port, name, autoConnect, timeoutMs, protocolVersionNumber);

        App app;
        MainWindowViewModel mainViewModel;

        public void EnableOpenRgbTalker(bool isEnabled)
        {
            if (isEnabled)
            {
                // Access the MainViewModel instance
                app = (App)Application.Current!;
                mainViewModel = app.MainViewModel;

                try
                {
                    client.Connect();

                    mainViewModel.OpenRgbStatus = "Connected to OpenRGB";

                    var plugins = client.GetPlugins();

                    var devices = client.GetAllControllerData();

                    var profiles = client.GetProfiles();

                    foreach (var device in devices)
                    {
                        switch (device.Type)
                        {
                            case DeviceType.Motherboard:
                            case DeviceType.Dram:
                            case DeviceType.Gpu:
                            case DeviceType.Cooler:
                                Towers.Add(device);
                                break;

                            case DeviceType.Keyboard:
                                Keyboards.Add(device);
                                keyboardStates[device.Index] = Enumerable.Repeat(new Color(0, 0, 0), device.Leds.Length).ToArray();
                                break;

                            case DeviceType.Ledstrip:
                            case DeviceType.Mouse:
                            case DeviceType.Mousemat:
                            case DeviceType.Headset:
                            case DeviceType.HeadsetStand:
                            case DeviceType.Gamepad:
                            case DeviceType.Light:
                            case DeviceType.Speaker:
                                Extras.Add(device);
                                break;

                            default:
                                break;
                        }
                    }
                    UsbDeviceMonitor.OnStageKitCommand += OnStageKitEvent;
                }
                catch (Exception ex)
                {
                    mainViewModel.OpenRgbStatus = $"An error occurred: {ex.Message}";
                }
            }
            else
            {
                cts.Cancel();
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

        private void OnStageKitEvent(StageKitTalker.CommandId commandId, byte parameter)
        {
            switch (commandId)
            {
                case StageKitTalker.CommandId.BlueLeds:
                    UpdateKeyboardColor(parameter, new Color(0, 0, 255), 0);
                    break;

                case StageKitTalker.CommandId.RedLeds:
                    UpdateKeyboardColor(parameter, new Color(255, 0, 0), 8);
                    break;

                case StageKitTalker.CommandId.GreenLeds:
                    UpdateKeyboardColor(parameter, new Color(0, 255, 0), 16);
                    break;

                case StageKitTalker.CommandId.YellowLeds:
                    UpdateKeyboardColor(parameter, new Color(255, 255, 0), 24);
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
                    StartStrobeEffect(1, UdpIntake.Buffer[(int)UdpIntake.ByteIndexName.BeatsPerMinute]);
                    break;

                case StageKitTalker.CommandId.StrobeMedium:
                    StartStrobeEffect(2, UdpIntake.Buffer[(int)UdpIntake.ByteIndexName.BeatsPerMinute]);
                    break;

                case StageKitTalker.CommandId.StrobeFast:
                    StartStrobeEffect(3, UdpIntake.Buffer[(int)UdpIntake.ByteIndexName.BeatsPerMinute]);
                    break;

                case StageKitTalker.CommandId.StrobeFastest:
                    StartStrobeEffect(4, UdpIntake.Buffer[(int)UdpIntake.ByteIndexName.BeatsPerMinute]);
                    break;

                case StageKitTalker.CommandId.DisableAll:
                    StopStrobeEffect();
                    StopBreathingEffect();
                    UpdateKeyboardColor(parameter, new Color(0, 0, 0), 0);
                    UpdateKeyboardColor(parameter, new Color(0, 0, 0), 8);
                    UpdateKeyboardColor(parameter, new Color(0, 0, 0), 16);
                    UpdateKeyboardColor(parameter, new Color(0, 0, 0), 24);
                    break;


                default:
                    throw new ArgumentOutOfRangeException(nameof(commandId), commandId, null);
            }
        }

        private void StartStrobeEffect(int speed, int bpm)
        {
            StopStrobeEffect();
            int interval;
            switch (speed)
            {
                case 1: // Slow (16th note)
                    interval = CalculateDelay(16, bpm );
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
                    foreach (var device in Towers)
                    {
                        ToggleDeviceLeds(device, true);
                    }
                    await Task.Delay(interval);
                    foreach (var device in Towers)
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

        private void ToggleDeviceLeds(Device device, bool turnOn)
        {
            var color = turnOn ? new Color(255, 255, 255) : new Color(0, 0, 0);
            var colors = Enumerable.Repeat(color, device.Leds.Length).ToArray();
            client.UpdateLeds(device.Index, colors);
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
            foreach (var device in Extras)
            {
                var colors = Enumerable.Repeat(new Color(brightness, brightness, brightness), device.Leds.Length).ToArray();
                client.UpdateLeds(device.Index, colors);
            }
        }

        private void UpdateKeyboardColor(byte parameter, Color color, int areaOffset)
        {
            if (Keyboards.Count == 0)
            {
                return;
            }

            const int numAreas = 32;
            foreach (var keyboard in Keyboards)
            {
                var keysPerArea = keyboard.Leds.Length / numAreas;
                var colors = keyboardStates[keyboard.Index];

                for (int area = areaOffset; area < areaOffset + 8; area++)
                {
                    for (int key = 0; key < keysPerArea; key++)
                    {
                        var ledIndex = area * keysPerArea + key;
                        if (ledIndex >= keyboard.Leds.Length) continue;
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
                client.UpdateLeds(keyboard.Index, new Span<Color>(colors));
            }
        }

        private int CalculateDelay(int noteValue, int bpm)
        {
            return (int)(60000.0 / bpm * 4 / noteValue);
        }
    }
}
