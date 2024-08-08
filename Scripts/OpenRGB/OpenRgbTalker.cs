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
                        if (device.Type == DeviceType.Keyboard)
                        {
                            Keyboards.Add(device);
                            // Initialize the state array for each keyboard
                            keyboardStates[device.Index] = Enumerable.Repeat(new Color(0, 0, 0), device.Leds.Length).ToArray();
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
            }
        }

        private void UpdateKeyboardColor(byte parameter, Color color, int areaOffset)
        {
            if (!Keyboards.Any())
            {
                return;
            }

            var keyboard = Keyboards[0]; // Assuming you have only one keyboard connected
            var numAreas = 32;
            var keysPerArea = keyboard.Leds.Length / numAreas;

            // Retrieve the current state array for this keyboard
            var colors = keyboardStates[keyboard.Index];

            // Update the relevant areas with the specified color
            for (int area = areaOffset; area < areaOffset + 8; area++)
            {
                for (int key = 0; key < keysPerArea; key++)
                {
                    int ledIndex = area * keysPerArea + key;
                    if (ledIndex < keyboard.Leds.Length)
                    {
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
            }

            // Create a span of the updated LED colors and send it to the device
            client.UpdateLeds(keyboard.Index, new Span<Color>(colors));
        }
    }
}
