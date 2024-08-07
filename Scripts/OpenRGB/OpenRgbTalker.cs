using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenRGB.NET;
using OpenRGB.NET.Utils;

namespace YALCY.Scripts.OpenRGB
{
    public class OpenRgbTalker
    {
        private CancellationTokenSource cts = new();
        private Task updateTask = Task.CompletedTask;

        public void EnableOpenRgbTalker(bool isEnabled)
        {
            if (isEnabled)
            {
                try
                {
                    using var client = new OpenRgbClient();

                    client.Connect();

                    Console.WriteLine("Connected to OpenRGB");

                    var plugins = client.GetPlugins();

                    var devices = client.GetAllControllerData();

                    var profiles = client.GetProfiles();

                    Console.WriteLine("Found devices:");
                    foreach (var device in devices)
                    {
                        Console.WriteLine(device.Name);
                    }

                    Console.WriteLine("Starting animation");

                    const int fps = 60;

                    updateTask = Task.Run(() =>
                    {
                        var deviceColors = new Color[devices.Length][];
                        for (var index = 0; index < devices.Length; index++)
                        {
                            var arr = ColorUtils.GetHueRainbow(devices[index].Leds.Length).ToArray();
                            deviceColors[index] = arr.Concat(arr).ToArray();
                        }
                        var colorOffsets = Enumerable.Range(0, devices.Length).Select(x => x).ToArray();
                        while (!cts.IsCancellationRequested)
                        {
                            for (var index = 0; index < devices.Length; index++)
                            {
                                var colors = deviceColors[index];
                                if (colors.Length == 0)
                                    continue;

                                var slice = colors.AsSpan().Slice(colorOffsets[index]++ % devices[index].Leds.Length, devices[index].Leds.Length);
                                client.UpdateLeds(index, slice);
                            }

                            Thread.Sleep(1000 / fps);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    // Additional error handling logic can go here
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
                        Console.WriteLine($"Task error: {innerException.Message}");
                    }
                }
                finally
                {
                    cts.Dispose();
                }
            }
        }
    }
}
