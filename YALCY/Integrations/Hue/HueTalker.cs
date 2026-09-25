using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HueApi;
using HueApi.ColorConverters;
using HueApi.Entertainment;
using HueApi.Entertainment.Extensions;
using HueApi.Entertainment.Models;
using HueApi.Models;
using HueApi.Models.Exceptions;
using YALCY.Integrations;
using YALCY.Integrations.StageKit;
using YALCY.Udp;
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
    private MainWindowViewModel? _mainViewModel;
    private readonly ManualStrobeFlasher _manualStrobeFlasher = new(ex => Console.WriteLine($"Hue manual strobe error: {ex.Message}"));

    public async Task EnableHue(bool isEnabled, string? bridgeIp, MainWindowViewModel? viewModel = null)
    {
        Console.WriteLine("EnableHue called.");

        if (viewModel != null)
        {
            _mainViewModel = viewModel;
        }

        if (_mainViewModel == null)
        {
            Console.WriteLine("HueTalker: No ViewModel provided and none cached.");
            return;
        }

        var mainViewModel = _mainViewModel;

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

                _baseEntLayer.SetState(_cancellationTokenSource.Token, new RGBColor("000000"), 0);
                lock (_ledStateLock)
                {
                    RenderLeds();
                    ApplyStrobeIdle();
                }

                UsbDeviceMonitor.OnStageKitCommand += SendRequest;

                StatusFooter.UpdateStatus("Hue", IntegrationStatus.Connected);
                _streamingActive = true;

                // YALCY only reacts to changes. Re-send the current cue/strobe so Hue doesn't wait for the next cue.
                mainViewModel.UdpIntake.ReplayOutputState();

                // Stream in the background. AutoUpdateAsync only returns when streaming stops; awaiting it here
                // blocked YALCY's startup sequence, so the UDP intake (started after Hue) never came up.
                var client = _client;
                var streamingGroup = _streamingGroup;
                var token = _cancellationTokenSource.Token;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await client.AutoUpdateAsync(streamingGroup, token, 50, onlySendDirtyStates: false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Hue was switched off.
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Hue streaming stopped: {ex.Message}");
                        StatusFooter.UpdateStatus("Hue", IntegrationStatus.Error);
                    }
                    finally
                    {
                        _streamingActive = false;
                    }
                });
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
            _streamingActive = false;
            // Cancel any ongoing operations
            _manualStrobeFlasher.Stop(SetManualStrobeStateAsync);
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

    public async Task RegisterHueBridgeAsync(string? bridgeIp, MainWindowViewModel? viewModel = null)
    {
        if (viewModel != null)
        {
            _mainViewModel = viewModel;
        }

        if (_mainViewModel == null)
        {
            Console.WriteLine("HueTalker: No ViewModel provided and none cached.");
            return;
        }

        var mainViewModel = _mainViewModel;

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

        await EnableHue(mainViewModel.HueEnabledSetting.IsEnabled, bridgeIp, mainViewModel);
    }

    private async Task<StreamingHueClient?> CreateStreamingClientAsync(string? bridgeIp, string username, string streamingClientKey)
    {
        try
        {
            if (_mainViewModel == null) return null;
            _mainViewModel.HueStreamingClientStatus = "Streaming Client Status: Attempting to create streaming client...";

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25)); // Set timeout
            var client = await Task.Run(() => new StreamingHueClient(bridgeIp, username, streamingClientKey), cts.Token);

            _mainViewModel.HueStreamingClientStatus = "Streaming Client Status: Streaming Client created.";
            return client;
        }
        catch (OperationCanceledException)
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.HueStreamingClientStatus = "Streaming Client Status: Operation timed out.";
            }
            StatusFooter.UpdateStatus("Hue", IntegrationStatus.Error);
            return null;
        }
        catch (Exception e)
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.HueStreamingClientStatus = $"Streaming Client Status: Error - {e.Message}";
            }
            StatusFooter.UpdateStatus("Hue", IntegrationStatus.Error);
            return null;
        }
    }

    private void SendRequest(StageKitTalker.CommandId commandId, byte parameter)
    {
        if (_entArea != null && _entArea.Data.First().Status != EntertainmentConfigurationStatus.active)
        {
            return;
        }

        switch (commandId)
        {
            case StageKitTalker.CommandId.StrobeSlow:
            case StageKitTalker.CommandId.StrobeMedium:
            case StageKitTalker.CommandId.StrobeFast:
            case StageKitTalker.CommandId.StrobeFastest:
                HandleStrobeCommand(commandId);
                return;

            case StageKitTalker.CommandId.StrobeOff:
                StopStrobe();
                return;
        }

        lock (_ledStateLock)
        {
            // Each Stage Kit colour command only describes ITS colour. Remember the latest mask per colour
            // instead of overwriting the whole rig, so parallel patterns (e.g. red + yellow + blue in Sweep)
            // stay visible together.
            switch (commandId)
            {
                case StageKitTalker.CommandId.BlueLeds:
                    _blueMask = parameter;
                    break;

                case StageKitTalker.CommandId.GreenLeds:
                    _greenMask = parameter;
                    break;

                case StageKitTalker.CommandId.YellowLeds:
                    _yellowMask = parameter;
                    break;

                case StageKitTalker.CommandId.RedLeds:
                    _redMask = parameter;
                    break;

                case StageKitTalker.CommandId.DisableAll:
                    StopStrobe();
                    _blueMask = _greenMask = _yellowMask = _redMask = 0;
                    break;

                default:
                    return;
            }

            RenderLeds();
        }
    }

    private readonly object _ledStateLock = new();
    private byte _blueMask;
    private byte _greenMask;
    private byte _yellowMask;
    private byte _redMask;
    private bool _strobeRunning;
    private volatile bool _streamingActive;
    private volatile bool _testStrobeActive;
    private int _testStrobeRunning;

    /// <summary>
    /// Flashes the strobe lamps (or all lamps if none are assigned) for 3 seconds at a fixed 120 BPM,
    /// independent of the game and of the strobe mode setting.
    /// </summary>
    public async Task TestStrobeAsync(MainWindowViewModel viewModel)
    {
        if (!_streamingActive || _baseEntLayer == null)
        {
            viewModel.SetHueLampStatus("Strobe test: Hue is not streaming. Turn Hue on and wait for 'Streaming is active'.");
            return;
        }

        if (Interlocked.Exchange(ref _testStrobeRunning, 1) == 1)
        {
            return;
        }

        try
        {
            viewModel.SetHueLampStatus(HasDedicatedStrobeLamps()
                ? "Strobe test: flashing the strobe lamps for 3 seconds..."
                : "Strobe test: no lamp is set to 'Strobe', flashing all lamps for 3 seconds...");

            _testStrobeActive = true;
            lock (_ledStateLock)
            {
                _strobeRunning = true;
            }

            _manualStrobeFlasher.Start(StageKitTalker.CommandId.StrobeSlow, 120f, SetManualStrobeStateAsync);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
        finally
        {
            _testStrobeActive = false;
            StopStrobe();
            Interlocked.Exchange(ref _testStrobeRunning, 0);
            viewModel.SetHueLampStatus("Strobe test: done.");
        }
    }

    private sealed class LampSettingsSnapshot
    {
        public static readonly LampSettingsSnapshot Empty =
            new(new Dictionary<int, string>(), HueColorHelper.DefaultStrobeIdleColor, 0);

        public LampSettingsSnapshot(IReadOnlyDictionary<int, string> roles, string idleColor, double idleBrightness)
        {
            Roles = roles;
            IdleColor = idleColor;
            IdleBrightness = idleBrightness;
        }

        public IReadOnlyDictionary<int, string> Roles { get; }
        public string IdleColor { get; }
        public double IdleBrightness { get; }

        public string RoleFor(int channelId)
        {
            return Roles.TryGetValue(channelId, out var role) ? role : HueLampRoles.Auto;
        }
    }

    private volatile LampSettingsSnapshot _lampSettings = LampSettingsSnapshot.Empty;

    /// <summary>
    /// Receives lamp roles and the strobe idle state from the UI. Safe to call at any time, also while streaming.
    /// </summary>
    public void ApplyLampSettings(IEnumerable<HueLampAssignmentSetting> assignments, string idleColorHex, double idleBrightness)
    {
        var roles = assignments
            .GroupBy(a => a.ChannelId)
            .ToDictionary(g => g.Key, g => HueLampRoles.Normalize(g.First().Role));

        _lampSettings = new LampSettingsSnapshot(
            roles,
            HueColorHelper.NormalizeHex(idleColorHex) ?? HueColorHelper.DefaultStrobeIdleColor,
            Math.Clamp(idleBrightness, 0, 1));

        lock (_ledStateLock)
        {
            if (HasDedicatedStrobeLamps())
            {
                // With dedicated strobe lamps the "flash everything" layer must not cover the pattern lamps.
                _effectLayer?.SetState(CancellationToken.None, new RGBColor("000000"), 0);
            }

            RenderLeds();
            if (!_strobeRunning)
            {
                ApplyStrobeIdle();
            }
        }
    }

    private bool HasDedicatedStrobeLamps()
    {
        var layer = _baseEntLayer;
        var settings = _lampSettings;
        return layer != null && layer.Any(light => settings.RoleFor(light.Id) == HueLampRoles.Strobe);
    }

    /// <summary>
    /// Renders the Stage Kit LED state onto all pattern lamps.
    /// "Auto" lamps share the 8 positions (folded in channel order, e.g. 4 lamps: 1+5, 2+6, 3+7, 4+8),
    /// "Light N" / "Lights N+M" lamps show exactly those positions, strobe and off lamps are skipped here.
    /// Must be called while holding _ledStateLock.
    /// </summary>
    private void RenderLeds()
    {
        var layer = _baseEntLayer;
        if (layer == null)
        {
            return;
        }

        var settings = _lampSettings;
        var lights = layer.OrderBy(x => x.Id).ToList();
        var autoLights = lights.Where(l => settings.RoleFor(l.Id) == HueLampRoles.Auto).ToList();

        foreach (var light in lights)
        {
            var role = settings.RoleFor(light.Id);
            if (role == HueLampRoles.Strobe)
            {
                continue;
            }

            if (role == HueLampRoles.Off)
            {
                light.SetState(CancellationToken.None, new RGBColor("000000"), 0);
                continue;
            }

            byte positions;
            if (!HueLampRoles.TryGetFixedPositions(role, out positions))
            {
                var autoIndex = autoLights.IndexOf(light);
                var autoCount = autoLights.Count;
                positions = 0;
                if (autoCount <= 8)
                {
                    for (var position = autoIndex; position < 8; position += autoCount)
                    {
                        positions |= (byte)(1 << position);
                    }
                }
                else
                {
                    positions = (byte)(1 << (autoIndex % 8));
                }
            }

            var (r, g, b, isActive) = StageKitColorBlender.BlendPod(
                (_blueMask & positions) != 0,
                (_redMask & positions) != 0,
                (_greenMask & positions) != 0,
                (_yellowMask & positions) != 0);

            light.SetState(CancellationToken.None, new RGBColor($"{r:X2}{g:X2}{b:X2}"), isActive ? 1 : 0);
        }
    }

    /// <summary>Puts dedicated strobe lamps into their idle colour/brightness. Must hold _ledStateLock.</summary>
    private void ApplyStrobeIdle()
    {
        var layer = _baseEntLayer;
        if (layer == null)
        {
            return;
        }

        var settings = _lampSettings;
        foreach (var light in layer.Where(l => settings.RoleFor(l.Id) == HueLampRoles.Strobe))
        {
            light.SetState(CancellationToken.None, new RGBColor(settings.IdleColor), settings.IdleBrightness);
        }
    }

    private void HandleStrobeCommand(StageKitTalker.CommandId commandId)
    {
        // Dedicated strobe lamps always flash. Without them, only flash everything in "Manual flash" mode.
        if (!HasDedicatedStrobeLamps() && _mainViewModel?.HueStrobeMode != StrobeOutputModes.ManualFlash)
        {
            StopStrobe();
            return;
        }

        lock (_ledStateLock)
        {
            _strobeRunning = true;
        }

        _manualStrobeFlasher.Start(commandId, HueSafeBpm(commandId, UdpIntake.BeatsPerMinute.Value), SetManualStrobeStateAsync);
    }

    /// <summary>
    /// Very short flash phases look irregular on Hue lamps (the stream runs at 50 fps, and the bridge and lamps add
    /// their own latency), so the tempo used for flashing is capped per speed to phases of at least ~75 ms
    /// (the beat grid is kept).
    /// </summary>
    private static float HueSafeBpm(StageKitTalker.CommandId commandId, float bpm)
    {
        var noteValue = commandId switch
        {
            StageKitTalker.CommandId.StrobeSlow => 16,
            StageKitTalker.CommandId.StrobeMedium => 24,
            StageKitTalker.CommandId.StrobeFast => 32,
            StageKitTalker.CommandId.StrobeFastest => 64,
            _ => 16
        };

        const double minPhaseMs = 75;
        var maxBpm = (float)(60000.0 * 4 / (noteValue * minPhaseMs));
        var effective = bpm > 0 ? bpm : 120f;

        // Halve the tempo until it fits, so flashes still land on the beat grid.
        while (effective > maxBpm)
        {
            effective /= 2f;
        }

        return effective;
    }

    private void StopStrobe()
    {
        _manualStrobeFlasher.Stop(SetManualStrobeStateAsync);
        lock (_ledStateLock)
        {
            _strobeRunning = false;
            ApplyStrobeIdle();
        }
    }

    private Task SetManualStrobeStateAsync(bool isOn, CancellationToken cancellationToken)
    {
        // A flash step that races with Stop() must not leave the lamps on.
        if (isOn && cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        // The safety blackout (no data from YARG) would swallow the test flashes, so the test bypasses it.
        if (UsbDeviceMonitor.IsOutputSuppressed && !_testStrobeActive)
        {
            isOn = false;
        }

        lock (_ledStateLock)
        {
            if (HasDedicatedStrobeLamps())
            {
                var settings = _lampSettings;
                foreach (var light in _baseEntLayer!.Where(l => settings.RoleFor(l.Id) == HueLampRoles.Strobe))
                {
                    light.SetState(CancellationToken.None, new RGBColor(isOn ? "FFFFFF" : "000000"), isOn ? 1 : 0);
                }

                return Task.CompletedTask;
            }

            var layer = _effectLayer ?? _baseEntLayer;
            layer?.SetState(CancellationToken.None, new RGBColor(isOn ? "FFFFFF" : "000000"), isOn ? 1 : 0);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads the channels of the "YARG" entertainment area and resolves a readable name per channel
    /// (entertainment service -> owning device -> device name). Does not need streaming to be active.
    /// </summary>
    public async Task DiscoverLampsAsync(MainWindowViewModel viewModel)
    {
        _mainViewModel ??= viewModel;

        var ip = viewModel.HueBridgeIp;
        var username = viewModel.HueAuthResult?.Username;
        if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(username))
        {
            viewModel.SetHueLampStatus("Lamps: register with the bridge first (IP + link button).");
            return;
        }

        try
        {
            viewModel.SetHueLampStatus("Lamps: reading entertainment areas from the bridge...");
            var api = new LocalHueApi(ip, username);

            var configurations = await api.GetEntertainmentConfigurationsAsync();
            var area = configurations.Data.FirstOrDefault(c =>
                string.Equals(c.Metadata?.Name, "yarg", StringComparison.OrdinalIgnoreCase));

            if (area == null)
            {
                viewModel.SetHueLampStatus("Lamps: no entertainment area named 'YARG' found. Create it in the Hue app.");
                return;
            }

            var services = (await api.GetEntertainmentServicesAsync()).Data;
            var devices = (await api.GetDevicesAsync()).Data;

            var deviceNames = devices.ToDictionary(d => d.Id, d => d.Metadata?.Name ?? "Unknown device");
            var serviceToDeviceName = services.ToDictionary(
                s => s.Id,
                s => s.Owner != null && deviceNames.TryGetValue(s.Owner.Rid, out var name) ? name : "Unknown device");

            var channelNames = area.Channels
                .OrderBy(c => c.ChannelId)
                .Select(c => new
                {
                    Channel = c,
                    Name = string.Join(" + ", c.Members
                        .Select(m => m.Service != null && serviceToDeviceName.TryGetValue(m.Service.Rid, out var n)
                            ? n
                            : "Unknown device")
                        .Distinct())
                })
                .ToList();

            // Gradient strips and similar devices expose several channels; number their segments.
            var lamps = new List<HueLampModel>();
            foreach (var group in channelNames.GroupBy(c => c.Name))
            {
                var segments = group.ToList();
                for (var i = 0; i < segments.Count; i++)
                {
                    var name = segments.Count > 1 ? $"{segments[i].Name} (segment {i + 1})" : segments[i].Name;
                    var position = segments[i].Channel.Position;
                    lamps.Add(new HueLampModel(segments[i].Channel.ChannelId, name, position.X, position.Y));
                }
            }

            viewModel.SetHueLamps(lamps);
        }
        catch (Exception ex)
        {
            viewModel.SetHueLampStatus($"Lamps: discovery failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _manualStrobeFlasher.Stop(SetManualStrobeStateAsync);
        _cancellationTokenSource?.Cancel();
        _client?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}
