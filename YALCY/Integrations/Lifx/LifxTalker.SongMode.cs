using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YALCY.Diagnostics;
using YALCY.Integrations.StageKit;
using YALCY.Usb;

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

    private bool IsIncluded(LifxLanDeviceModel device)
    {
        if (!device.HasState) return false;
        var filters = Options.IncludeLights.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return filters.Length == 0 || filters.Any(f => string.Equals(f, device.Label, StringComparison.OrdinalIgnoreCase)
            || string.Equals(f, device.Serial, StringComparison.OrdinalIgnoreCase) || f == device.Address.ToString());
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
            var brightness = UsbDeviceMonitor.IsOutputSuppressed || cue is 8 or 9 or 10 ? 0 : Options.Brightness;
            var state = (color, strobe, brightness, Udp.UdpIntake.BeatsPerMinute.Value);
            if (_lastSongState == state) return;
            _lastSongState = state;
            _songFlasher.Stop();
            _songColor = LifxPalette.Color(color, brightness);
            if (strobe != 0 && brightness > 0)
            {
                var command = strobe switch
                {
                    20 => StageKitTalker.CommandId.StrobeFastest, 21 => StageKitTalker.CommandId.StrobeFast,
                    22 => StageKitTalker.CommandId.StrobeMedium, _ => StageKitTalker.CommandId.StrobeSlow
                };
                _songFlasher.Start(command, state.Item4, (on, token) =>
                {
                    if (_isEnabled && !token.IsCancellationRequested)
                        SendWholeColor(on && !UsbDeviceMonitor.IsOutputSuppressed ? _songColor : _songColor.WithBrightness(0), 0);
                    return Task.CompletedTask;
                });
            }
            else SendWholeColor(_songColor, brightness == 0 ? 0 : DefaultTransitionMs);
        }
        catch (Exception ex)
        {
            _lastSongState = null;
            AppLog.Write(LogLevel.Error, "LIFX", $"Could not apply song colors: {ex.Message}");
        }
    }

    private void SendWholeColor(LifxHsbk color, uint transition)
    {
        var devices = SnapshotDevices();
        lock (_socketLock)
        {
            foreach (var device in devices)
            {
                SendColor(device, color, transition);
                SendLightPower(device, color.IsOn, transition);
                foreach (var zone in device.Zones) zone.CurrentColor = color;
                device.IsPowered = color.IsOn;
            }
        }
    }

    public async Task TestColorAsync(string color)
    {
        if (!await _lifecycleGate.WaitAsync(0)) return;
        try { await TestColorCoreAsync(color); }
        finally { _lifecycleGate.Release(); }
    }

    private async Task TestColorCoreAsync(string color)
    {
        if (_isEnabled)
        {
            AppLog.Write(LogLevel.Warning, "LIFX", "Disable LIFX output before testing a color.");
            return;
        }
        await DiscoverDevicesAsync();
        if (!await _discoveryGate.WaitAsync(0)) return;
        try
        {
            if (_isEnabled) return;
            EnsureCommandClient();
            var devices = SnapshotDevices();
            if (devices.Count == 0) return;
            AppLog.Write(LogLevel.Information, "LIFX", $"Testing {color} on {devices.Count} device(s) for two seconds; original colors and power will be restored.");
            try
            {
                SendWholeColor(LifxPalette.Color(color, Options.Brightness), DefaultTransitionMs);
                await Task.Delay(2000);
            }
            finally { RestoreOriginalDevices(devices, allZones: true); }
        }
        catch (Exception ex) { AppLog.Write(LogLevel.Error, "LIFX", $"Color test failed: {ex.Message}"); }
        finally { _discoveryGate.Release(); }
    }

    private void RestoreOriginalDevices(IEnumerable<LifxLanDeviceModel> devices, bool allZones)
    {
        lock (_socketLock)
        {
            foreach (var device in devices.Where(IsIncluded))
            {
                if (!_originalStates.TryGetValue(device.Serial, out var original)) continue;
                try
                {
                    foreach (var zone in device.Zones.Where(z => allZones || z.AssignedStageLight != LifxStageAssignments.Unassigned))
                    {
                        var color = original.Colors.GetValueOrDefault(zone.ZoneIndex, zone.OriginalColor);
                        if (device.Zones.Count == 1) SendColor(device, color, DefaultTransitionMs);
                        else SendColorZones(device, zone.ZoneIndex, zone.ZoneIndex, color, DefaultTransitionMs, MultiZoneApply.Apply);
                        zone.CurrentColor = color;
                    }
                    if (allZones || device.Zones.Any(z => z.AssignedStageLight != LifxStageAssignments.Unassigned))
                        SendLightPower(device, original.Power, DefaultTransitionMs);
                }
                catch (Exception ex) { AppLog.Write(LogLevel.Error, "LIFX", $"Could not restore '{device.Label}': {ex.Message}"); }
            }
        }
    }
}
