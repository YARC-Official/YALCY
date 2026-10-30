using YALCY.Lighting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YALCY.Diagnostics;
using YALCY.ViewModels;

namespace YALCY.Integrations.Lifx;

public sealed partial class LifxTalker
{
    private LifxOptions Options => _mainViewModel?.LifxOptions ?? SettingsManager.LifxOptions;
    private readonly SemaphoreSlim _discoveryGate = new(1, 1);
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private CancellationTokenSource? _rediscoveryCancellation, _discoveryCancellation;
    private readonly Dictionary<string, (bool Power, Dictionary<int, LifxHsbk> Colors)> _originalStates = new();
    private readonly ManualStrobeFlasher _songFlasher = new(ex => AppLog.Write(LogLevel.Error, "LIFX", $"Song-color output failed: {ex.Message}"));
    private (string Color, int Strobe, int Brightness, float Bpm)? _lastSongState;
    private LifxHsbk _songColor;
    private LifxWholeLightOutput? _wholeLightOutput;
    private int _busyCount;
    private void SetBusy(bool busy)
    {
        var count = busy ? Interlocked.Increment(ref _busyCount) : Interlocked.Decrement(ref _busyCount);
        _mainViewModel?.SetLifxBusy(count > 0);
    }

    private bool IsIncluded(LifxLanDeviceModel device)
    {
        if (!device.HasState) return false;
        return Options.Includes(device.Serial, device.Label, device.Address.ToString());
    }

    private async Task RediscoverLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(Options.RediscoverySeconds), token);
                if (Options.AutoDiscover && _isEnabled) await DiscoverDevicesAsync();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { AppLog.Write(LogLevel.Error, "LIFX", $"Automatic discovery stopped: {ex.Message}"); }
    }

    private void PreserveOriginalStates(IEnumerable<LifxLanDeviceModel> devices)
    {
        foreach (var device in devices)
        {
            if (!device.HasState) continue;
            if (!_isEnabled || !_originalStates.TryGetValue(device.Serial, out var original))
            {
                original = (device.IsPowered, device.Zones.ToDictionary(z => z.ZoneIndex, z => z.CurrentColor));
                _originalStates[device.Serial] = original;
            }
            foreach (var zone in device.Zones)
                zone.OriginalColor = original.Colors.GetValueOrDefault(zone.ZoneIndex, zone.CurrentColor);
        }
    }

    private void OnSongPacket(byte[] packet)
    {
        if (!_isEnabled || !Options.WholeLightMode || _mainViewModel == null) return;
        try
        {
            var udp = _mainViewModel.UdpIntake;
            var cue = udp.LightingCue.Value;
            var color = LifxPalette.SongColor(cue, udp.CurrentSongSection.Value);
            var strobe = (int)udp.StrobeState.Value;
            if (strobe != 24 && strobe is not (>= 20 and <= 23)) strobe = cue;
            if (strobe is not (>= 20 and <= 23)) strobe = 0;
            var brightness = LightingOutput.IsOutputSuppressed || cue is 8 or 9 or 10 ? 0 : Options.Brightness;
            var state = (color, strobe, brightness, Udp.UdpIntake.BeatsPerMinute.Value);
            if (_lastSongState == state) return;
            _lastSongState = state;
            _songFlasher.Stop();
            _songColor = LifxPalette.Color(color, brightness);
            if (strobe != 0 && brightness > 0)
            {
                var command = strobe switch
                {
                    20 => LightingCommand.StrobeFastest, 21 => LightingCommand.StrobeFast,
                    22 => LightingCommand.StrobeMedium, _ => LightingCommand.StrobeSlow
                };
                _songFlasher.Start(command, state.Item4, (on, token) =>
                {
                    if (_isEnabled && !token.IsCancellationRequested)
                        SendWholeColor(on && !LightingOutput.IsOutputSuppressed ? _songColor : _songColor.WithBrightness(0), 0);
                    return Task.CompletedTask;
                });
            }
            else SendWholeColor(_songColor, brightness == 0 ? 0 : DefaultTransitionMs, urgent: brightness == 0);
        }
        catch (Exception ex)
        {
            _lastSongState = null;
            AppLog.Write(LogLevel.Error, "LIFX", $"Could not apply song colors: {ex.Message}");
        }
    }

    private void SendWholeColor(LifxHsbk color, uint transition, bool urgent = false)
    {
        // No direct-send fallback: a late callback during shutdown must not
        // reopen the socket or undo restoration after the worker has stopped.
        var output = _wholeLightOutput;
        if (!_isEnabled || output == null) return;
        foreach (var device in SnapshotDevices())
            output.Submit(device, color, transition, Options.UpdateRate(device.Serial), urgent);
    }

    private void SendWholeDevice(LifxLanDeviceModel device, LifxHsbk color, uint transition, bool refresh)
    {
        lock (_socketLock)
        {
            if (LightingOutput.IsOutputSuppressed) { color = color.WithBrightness(0); transition = 0; }
            SendColor(device, color, transition);
            // A periodic refresh recovers a lost UDP power command or a restarted
            // bulb; ordinary color changes do not redundantly toggle power.
            if (refresh || device.IsPowered != color.IsOn)
                SendLightPower(device, color.IsOn, transition);
            foreach (var zone in device.Zones) zone.CurrentColor = color;
            device.IsPowered = color.IsOn;
        }
    }

    public Task TestColorAsync(string color, MainWindowViewModel? viewModel = null)
    {
        if (viewModel != null) _mainViewModel = viewModel;
        return TestDevicesAsync(color, null);
    }
    public Task IdentifyAsync(string serial) => TestDevicesAsync("Cyan", serial);

    private async Task TestDevicesAsync(string color, string? serial)
    {
        if (!await _lifecycleGate.WaitAsync(0)) return;
        SetBusy(true);
        try { await TestColorCoreAsync(color, serial); }
        finally { SetBusy(false); _lifecycleGate.Release(); }
    }

    private async Task TestColorCoreAsync(string color, string? serial)
    {
        if (_isEnabled || LightingOutput.IsOutputSuppressed)
        {
            UpdateViewModelStatus("LIFX status: Test unavailable.", "Disable LIFX output and release blackout before testing a light.");
            return;
        }
        await DiscoverDevicesAsync();
        if (!await _discoveryGate.WaitAsync(0)) return;
        try
        {
            if (_isEnabled || LightingOutput.IsOutputSuppressed) return;
            EnsureCommandClient();
            List<LifxLanDeviceModel> devices;
            lock (_deviceLock)
                devices = _devices.Where(d => d.HasState && (serial == null ? IsIncluded(d)
                    : string.Equals(d.Serial, serial, StringComparison.OrdinalIgnoreCase))).ToList();
            if (devices.Count == 0)
            {
                UpdateViewModelStatus("LIFX status: No ready lights to test.", "Select a discovered light that answered the color/power query, then try again.");
                return;
            }
            UpdateViewModelStatus(serial == null ? $"LIFX status: Testing {devices.Count} light(s)." : $"LIFX status: Identifying {devices[0].Label}.",
                "Showing the test color for two seconds, then restoring the previous colors and power.");
            try
            {
                foreach (var device in devices)
                    SendWholeDevice(device, LifxPalette.Color(color, Options.Brightness), DefaultTransitionMs, true);
                // Check blackout during the test, not just before it starts.
                for (var i = 0; i < 20 && !LightingOutput.IsOutputSuppressed; i++) await Task.Delay(100);
            }
            finally { RestoreOriginalDevices(devices, allZones: true, includeExcluded: true); }
            UpdateViewModelStatus("LIFX status: Test finished.", LightingOutput.IsOutputSuppressed
                ? "Blackout is active; tested lights have been turned off." : "Previous light colors and power restored. Enable LIFX output when ready.");
        }
        catch (Exception ex) { UpdateViewModelStatus("LIFX status: Color test failed.", ex.Message); }
        finally { _discoveryGate.Release(); }
    }

    private void RestoreOriginalDevices(IEnumerable<LifxLanDeviceModel> devices, bool allZones, bool includeExcluded = false)
    {
        lock (_socketLock)
        {
            foreach (var device in devices.Where(d => d.HasState && (includeExcluded || IsIncluded(d))))
            {
                if (!_originalStates.TryGetValue(device.Serial, out var original)) continue;
                try
                {
                    if (LightingOutput.IsOutputSuppressed)
                    {
                        SendLightPower(device, false, 0);
                        device.IsPowered = false;
                        continue;
                    }
                    foreach (var zone in device.Zones.Where(z => allZones || z.AssignedStageLight != LifxStageAssignments.Unassigned))
                    {
                        var color = original.Colors.GetValueOrDefault(zone.ZoneIndex, zone.OriginalColor);
                        if (device.Zones.Count == 1) SendColor(device, color, DefaultTransitionMs);
                        else SendColorZones(device, zone.ZoneIndex, zone.ZoneIndex, color, DefaultTransitionMs, MultiZoneApply.Apply);
                        zone.CurrentColor = color;
                    }
                    if (allZones || device.Zones.Any(z => z.AssignedStageLight != LifxStageAssignments.Unassigned))
                    {
                        SendLightPower(device, original.Power, DefaultTransitionMs);
                        device.IsPowered = original.Power;
                    }
                }
                catch (Exception ex) { AppLog.Write(LogLevel.Error, "LIFX", $"Could not restore '{device.Label}': {ex.Message}"); }
            }
        }
    }
}
