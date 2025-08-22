using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using HueApi;
using HueApi.ColorConverters;
using HueApi.Entertainment;
using HueApi.Entertainment.Extensions;
using HueApi.Entertainment.Models;
using HueApi.Models;
using HueApi.Models.Exceptions;
using YALCY.Integrations.StageKit;
using YALCY.Usb;
using YALCY.ViewModels;
using YALCY.Views.Components;

namespace YALCY.Integrations.Hue;

public class HueTalker : IDisposable
{
    private static StreamingGroup? _streamingGroup;
    private static EntertainmentLayer? _baseEntLayer;
    private static EntertainmentLayer? _effectLayer;
    private static HueResponse<EntertainmentConfiguration>? _entArea;
    private StreamingHueClient? _client;
    private CancellationTokenSource? _cancellationTokenSource;

    public async Task EnableHue(bool isEnabled, string? bridgeIp)
    {
        Console.WriteLine("EnableHue called.");
        // Access the MainViewModel instance
        var app = (App)Application.Current!;
        var mainViewModel = app.MainViewModel;

        if (isEnabled)
        {
            Console.WriteLine("Enabling Hue.");
            StatusFooter.UpdateStatus("Hue", IntegrationStatus.Connecting);
            var (isValid, statusMessage) = Helpers.IpValidator(bridgeIp);

            mainViewModel.HueIpStatus = statusMessage;

            if (!isValid)
            {
                return;
            }


            _client = await CreateStreamingClientAsync(mainViewModel.HueBridgeIp, mainViewModel.HueAuthResult.Username, mainViewModel.HueAuthResult.StreamingClientKey);

            if (_client == null)
            {
                mainViewModel.HueMessage = "Failed to create streaming client.";
                return;
            }

            try
            {
                // Get the entertainment group
                var all = await _client.LocalHueApi.GetEntertainmentConfigurationsAsync();
                var group = all.Data.FirstOrDefault(g => g.Metadata?.Name.ToLower() == "yarg");

                if (group == null)
                {
                    mainViewModel.HueEntertainmentGroupStatus = "Entertainment Group Status: No Entertainment Group found named 'YARG', use your Hue app to make it.";
                    return;
                }
                else
                {
                    mainViewModel.HueEntertainmentGroupStatus = "Entertainment Group Status: Found 'YARG' entertainment group.";
                }

                // Create a streaming group
                _streamingGroup = new StreamingGroup(group.Channels);

                // Connect to the streaming group
                await _client.ConnectAsync(group.Id);

                // Initialize the CancellationTokenSource for ongoing operations
                _cancellationTokenSource = new CancellationTokenSource();

                // Create new base layer
                _entArea = await _client.LocalHueApi.GetEntertainmentConfigurationAsync(group.Id);

                mainViewModel.HueStreamingActiveStatus = (_entArea.Data.First().Status == EntertainmentConfigurationStatus.active ? "Streaming Active Status: Streaming is active" : "Streaming Active Status: Streaming is not active");

                _baseEntLayer = _streamingGroup.GetNewLayer(isBaseLayer: true);
                _effectLayer = _streamingGroup.GetNewLayer();

                _baseEntLayer.SetState(_cancellationTokenSource.Token, new RGBColor("FFFFFF"), 1);
                UsbDeviceMonitor.OnStageKitCommand += SendRequest;

                StatusFooter.UpdateStatus("Hue", IntegrationStatus.Connected);

                // Start auto-updating this entertainment group
                await _client.AutoUpdateAsync(_streamingGroup, _cancellationTokenSource.Token, 50, onlySendDirtyStates: false);
            }
            catch (UnauthorizedAccessException)
            {
                StatusFooter.UpdateStatus("Hue", IntegrationStatus.Error);
                mainViewModel.HueStreamingClientStatus = $"Streaming Client Status: Streaming Client not created. Unauthorized access. Remember to push the link button on the bridge before registering!";

                // Reset the HueAuthResult in case it's invalid
                mainViewModel.HueAuthResult.Username = "";
                mainViewModel.HueAuthResult.StreamingClientKey = "";
                mainViewModel.HueAuthResult.Ip = "";
            }
            catch (NullReferenceException)
            {
                StatusFooter.UpdateStatus("Hue", IntegrationStatus.Error);
                mainViewModel.HueStreamingClientStatus = "Streaming client status: Initialize streaming client failed: YALCY probably isn't registered with the bridge. Try registering again and remember to push the link button on the bridge first!";
                mainViewModel.HueEntertainmentGroupStatus = "Entertainment Group Status: Can't get entertainment group without a streaming client!";
                mainViewModel.HueStreamingActiveStatus = "Streaming is not active";
            }
            catch (HueEntertainmentException)
            {
                StatusFooter.UpdateStatus("Hue", IntegrationStatus.Error);
                mainViewModel.HueEntertainmentGroupStatus = "Entertainment Group Status: No Entertainment Group found. Create one in your Phillips Hue app and name it YARG.";
            }
            catch (Exception ex)
            {
                StatusFooter.UpdateStatus("Hue", IntegrationStatus.Error);
                mainViewModel.HueMessage = $"Error: {ex.Message}";
            }
        }
        else
        {
            Console.WriteLine("Disabling Hue.");
            // Cancel any ongoing operations
            _cancellationTokenSource?.Cancel();

            // Remove event handlers
            UsbDeviceMonitor.OnStageKitCommand -= SendRequest;
            StatusFooter.UpdateStatus("Hue", IntegrationStatus.Off);

            // Dispose of the client and cancellation token
            _cancellationTokenSource?.Dispose();
            _client?.Dispose();

            mainViewModel.HueStreamingActiveStatus = "Streaming Active Status: Streaming is stopped";
        }
    }

    public async Task RegisterHueBridgeAsync(string? bridgeIp)
    {
        // Access the MainViewModel instance
        var app = (App)Application.Current!;
        var mainViewModel = app.MainViewModel;

        var (isValid, statusMessage) = Helpers.IpValidator(bridgeIp);

        mainViewModel.HueIpStatus = statusMessage;

        if (!isValid)
        {
            return;
        }

        if (string.IsNullOrEmpty(mainViewModel.HueAuthResult.Username) && string.IsNullOrEmpty(mainViewModel.HueAuthResult.StreamingClientKey) &&
            string.IsNullOrEmpty(mainViewModel.HueAuthResult.Ip))
        {
            try
            {
                mainViewModel.HueRegisterStatus = "Registering Status: Registering with the bridge... (Up to 30 seconds)";
                mainViewModel.HueAuthResult = await LocalHueApi.RegisterAsync(mainViewModel.HueBridgeIp, "YALCY", "YACLY-DEVICE", true);
            }
            catch (LinkButtonNotPressedException ex)
            {
                mainViewModel.HueRegisterStatus = $"Registering Status: Link button not pressed exception: {ex.Message}";
                StatusFooter.UpdateStatus("Hue", IntegrationStatus.Error);
            }
            catch (Exception ex)
            {
                mainViewModel.HueRegisterStatus = $"Registering Status: Probably wrong bridge IP address: {ex.Message}";
                StatusFooter.UpdateStatus("Hue", IntegrationStatus.Error);
            }
        }
        else
        {
            mainViewModel.HueRegisterStatus = "Registering Status: Already registered with the bridge";
        }

        await EnableHue(mainViewModel.HueEnabledSetting.IsEnabled, bridgeIp);
    }

    private async Task<StreamingHueClient?> CreateStreamingClientAsync(string? bridgeIp, string username, string streamingClientKey)
    {
        try
        {
            var mainViewModel = ((App)Application.Current!).MainViewModel;
            mainViewModel.HueStreamingClientStatus = "Streaming Client Status: Attempting to create streaming client...";

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25)); // Set timeout
            var client = await Task.Run(() => new StreamingHueClient(bridgeIp, username, streamingClientKey), cts.Token);

            mainViewModel.HueStreamingClientStatus = "Streaming Client Status: Streaming Client created.";
            return client;
        }
        catch (OperationCanceledException)
        {
            var mainViewModel = ((App)Application.Current!).MainViewModel;
            mainViewModel.HueStreamingClientStatus = "Streaming Client Status: Operation timed out.";
            StatusFooter.UpdateStatus("Hue", IntegrationStatus.Error);
            return null;
        }
        catch (Exception e)
        {
            var mainViewModel = ((App)Application.Current!).MainViewModel;
            mainViewModel.HueStreamingClientStatus = $"Streaming Client Status: Error - {e.Message}";
            StatusFooter.UpdateStatus("Hue", IntegrationStatus.Error);
            return null;
        }
    }

    private static void SendRequest(StageKitTalker.CommandId commandId, byte parameter)
    {
        if (_entArea != null && _entArea.Data.First().Status != EntertainmentConfigurationStatus.active)
        {
            return;
        }

        RGBColor color;
        switch (commandId)
        {
            case StageKitTalker.CommandId.BlueLeds:
                color = new RGBColor("0000FF");
                break;

            case StageKitTalker.CommandId.GreenLeds:
                color = new RGBColor("00FF00");
                break;

            case StageKitTalker.CommandId.RedLeds:
                color = new RGBColor("FF0000");
                break;

            case StageKitTalker.CommandId.YellowLeds:
                color = new RGBColor("FFFF00");
                break;

            case StageKitTalker.CommandId.DisableAll:
                color = new RGBColor("000000");
                break;

            default:
                color = new RGBColor("000000");
                break;
        }

        for (int i = 0; i < 8; i++)
        {
            var light = _baseEntLayer?.FirstOrDefault(x => x.Id == i);
            if (light == null)
            {
                continue;
            }
            if ((parameter & (1 << i)) != 0)
            {
                light.SetBrightness(CancellationToken.None, 1);
                light.SetColor(CancellationToken.None, color);
            }
            else
            {
                light.SetBrightness(CancellationToken.None, 1);
                light.SetColor(CancellationToken.None, new RGBColor("000000"));
            }
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _client?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}
