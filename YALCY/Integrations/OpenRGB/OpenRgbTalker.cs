using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using OpenRGB.NET;
using YALCY.Integrations;
using YALCY.Integrations.StageKit;
using YALCY.Udp;
using YALCY.Usb;
using YALCY.ViewModels;
using YALCY.ViewModels.OpenRGB;
using YALCY.Views.Components;
using Device = OpenRGB.NET.Device;

namespace YALCY.Integrations.OpenRGB;

public enum OpenRgbBlendMode
{
    Discrete = 0,
    UniformWash = 1,
    SmoothGradient = 2
}

public class ZoneInfo
{
    public Device Device { get; set; } = null!;
    public int ZoneIndex { get; set; }
    public Zone Zone { get; set; } = null!;
}

public class OpenRgbTalker
{
    // Shared thread synchronization lock
    public readonly object Lock = new();

    // Legacy device-based collections (kept for backward compatibility with ViewModels)
    public List<Device> OffList { get; } = new();
    public List<Device> LightPodList { get; } = new();
    public List<Device> StrobeList { get; } = new();
    public List<Device> FoggerList { get; } = new();
    public Dictionary<int, Color[]> LightPodStates { get; } = new();

    // New zone-based collections
    public Dictionary<string, ZoneInfo> OffZones { get; } = new();
    public Dictionary<string, ZoneInfo> LightPodZones { get; } = new();
    public Dictionary<string, ZoneInfo> StrobeZones { get; } = new();
    public Dictionary<string, ZoneInfo> FoggerZones { get; } = new();
    public Dictionary<string, Color[]> LightPodZoneStates { get; } = new();
    public Dictionary<string, Color[]> LightPodZoneRawStates { get; } = new();
    public Dictionary<string, OpenRgbBlendMode> ZoneBlendModes { get; } = new();

    // Hybrid LightPod + Strobe tracking collections
    public HashSet<int> LightPodStrobeDevices { get; } = new();
    public HashSet<string> LightPodStrobeZones { get; } = new();
    private volatile bool _isStrobeActive;

    // Identification tracking to suppress background effect updates during test flashes
    private readonly HashSet<int> _identifyingDevices = new();
    private readonly HashSet<string> _identifyingZones = new();

    // Persistent device color framebuffers to prevent zones from overwriting each other with black
    private readonly Dictionary<int, Color[]> _deviceColorBuffers = new();

    // Non-blocking coalesced flush queue for LightPod LED updates
    private readonly object _lightPodSendLock = new();
    private bool _isLightPodSendPending = false;

    // Manual strobe flasher and fogger breathing cancellation tokens
    private readonly ManualStrobeFlasher _manualStrobeFlasher = new(ex => Console.WriteLine($"OpenRGB manual strobe error: {ex.Message}"));
    private CancellationTokenSource _fogCts = new();
    private Task _fogTask = Task.CompletedTask;

    // OpenRGB Client instance and client metadata
    private OpenRgbClient? client;
    private static readonly string ClientName = "YALCY";
    private static readonly bool AutoConnect = false;
    private static readonly int TimeoutMs = 1000;
    private static readonly uint ProtocolVersionNumber = 4;

    public event Action<Device>? OpenRgbDeviceInserted;
    public event Action<Device>? OpenRgbDeviceRemoved;

    private MainWindowViewModel? _mainViewModel;

    #region Identification Helpers

    public bool IsDeviceIdentifying(int deviceIndex)
    {
        lock (Lock)
        {
            return _identifyingDevices.Contains(deviceIndex);
        }
    }

    public bool IsZoneIdentifying(string zoneKey)
    {
        lock (Lock)
        {
            return _identifyingZones.Contains(zoneKey);
        }
    }

    #endregion

    #region Connection Management

    public async Task ConnectToOpenRgbServerAsync(string serverIp, ushort serverPort, MainWindowViewModel? viewModel = null)
    {
        if (viewModel != null)
        {
            _mainViewModel = viewModel;
        }

        if (_mainViewModel == null)
        {
            Console.WriteLine("OpenRgbTalker: No ViewModel provided.");
            return;
        }

        var mainViewModel = _mainViewModel;
        Console.WriteLine("Connecting to OpenRGB server...");

        var (isValid, statusMessage) = Helpers.IpValidator(serverIp);
        mainViewModel.OpenRgbStatus = statusMessage;
        if (!isValid) return;

        try
        {
            ClearAllCollections();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                MainWindowViewModel.ClearOpenRgbVisualList();
                mainViewModel.DeviceCategories.Clear();
                mainViewModel.ClearDevicesWithZones();
            });

            client = new OpenRgbClient(serverIp, serverPort, ClientName, AutoConnect, TimeoutMs, ProtocolVersionNumber);
            client.Connect();

            mainViewModel.OpenRgbStatus = "OpenRGB status: Connected to OpenRGB";
            var devices = client.GetAllControllerData();

            client.DeviceListUpdated += OnDeviceListUpdate;
            OpenRgbDeviceInserted += MainWindowViewModel.OnOpenRgbDeviceInserted;
            OpenRgbDeviceRemoved += MainWindowViewModel.OnOpenRgbDeviceRemoved;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var device in devices)
                {
                    OpenRgbDeviceInserted?.Invoke(device);
                    mainViewModel.DeviceCategories.Add(new DeviceCategory(device, 0, mainViewModel));
                    mainViewModel.DevicesWithZones.Add(new DeviceWithZones(device, mainViewModel));
                }
            });

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
            UsbDeviceMonitor.OnStageKitCommand -= OnStageKitEvent;
            _manualStrobeFlasher.Stop(SetStrobeFlashStateAsync);

            StopBreathingEffect();

            try
            {
                if (_fogTask != null && !_fogTask.IsCompleted)
                {
                    await _fogTask;
                }
            }
            catch (Exception ex)
            {
                if (_mainViewModel != null)
                {
                    _mainViewModel.OpenRgbStatus = $"Task error: {ex.Message}";
                }
            }
            finally
            {
                client?.Dispose();
                client = null;
            }
        }
    }

    private void OnDeviceListUpdate(object? sender, EventArgs e)
    {
        if (client == null) return;
        var devices = client.GetAllControllerData();

        ClearAllCollections();

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

    private void ClearAllCollections()
    {
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
            LightPodStrobeDevices.Clear();
            LightPodStrobeZones.Clear();
            _deviceColorBuffers.Clear();
        }
    }

    #endregion

    #region Low-Level Thread-Safe Framebuffer & Socket Helpers

    public Color[] GetDeviceColorBuffer(Device device)
    {
        lock (Lock)
        {
            if (!_deviceColorBuffers.TryGetValue(device.Index, out var buffer) || buffer.Length != device.Leds.Length)
            {
                buffer = new Color[device.Leds.Length];
                _deviceColorBuffers[device.Index] = buffer;
            }
            return buffer;
        }
    }

    public static int GetZoneStartLedIndex(Device device, int zoneIndex)
    {
        int start = 0;
        for (int i = 0; i < zoneIndex && i < device.Zones.Length; i++)
        {
            start += (int)device.Zones[i].LedCount;
        }
        return start;
    }

    public void UpdateZoneLedsSafely(Device device, int zoneIndex, Span<Color> zoneColors)
    {
        if (client == null || device == null) return;
        try
        {
            int start = GetZoneStartLedIndex(device, zoneIndex);
            Color[] fullDeviceColors;

            lock (Lock)
            {
                var buffer = GetDeviceColorBuffer(device);
                for (int i = 0; i < zoneColors.Length && (start + i) < buffer.Length; i++)
                {
                    buffer[start + i] = zoneColors[i];
                }
                fullDeviceColors = buffer.ToArray();
            }

            client.UpdateLeds(device.Index, fullDeviceColors);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdateZoneLedsSafely error: {ex.Message}");
        }
    }

    public void TurnOffDeviceLeds(Device device)
    {
        if (client == null) return;
        ToggleDeviceLeds(device, false);
    }

    public void TurnOffZoneLeds(Device device, int zoneIndex, Zone zone)
    {
        if (client == null) return;
        ToggleZoneLeds(new ZoneInfo { Device = device, ZoneIndex = zoneIndex, Zone = zone }, false);
    }

    private void ToggleDeviceLeds(Device device, bool turnOn)
    {
        try
        {
            if (client == null) return;
            var color = turnOn ? new Color(255, 255, 255) : new Color(0, 0, 0);
            var colors = Enumerable.Repeat(color, device.Leds.Length).ToArray();

            lock (Lock)
            {
                var buffer = GetDeviceColorBuffer(device);
                Array.Copy(colors, buffer, buffer.Length);
            }

            client.UpdateLeds(device.Index, colors);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ToggleDeviceLeds error: {ex.Message}");
        }
    }

    private void ToggleZoneLeds(ZoneInfo zoneInfo, bool turnOn)
    {
        try
        {
            if (client == null) return;
            var color = turnOn ? new Color(255, 255, 255) : new Color(0, 0, 0);
            var colors = Enumerable.Repeat(color, (int)zoneInfo.Zone.LedCount).ToArray();
            UpdateZoneLedsSafely(zoneInfo.Device, zoneInfo.ZoneIndex, colors);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ToggleZoneLeds error: {ex.Message}");
        }
    }

    #endregion

    #region StageKit Event Dispatching

    private byte _stageKitBlueParam;
    private byte _stageKitRedParam;
    private byte _stageKitGreenParam;
    private byte _stageKitYellowParam;

    private void OnStageKitEvent(StageKitTalker.CommandId commandId, byte parameter)
    {
        try
        {
            switch (commandId)
            {
                case StageKitTalker.CommandId.BlueLeds:
                    _stageKitBlueParam = parameter;
                    UpdateLightPodColor(parameter, new Color(0, 0, 255), 0);
                    break;

                case StageKitTalker.CommandId.RedLeds:
                    _stageKitRedParam = parameter;
                    UpdateLightPodColor(parameter, new Color(255, 0, 0), 8);
                    break;

                case StageKitTalker.CommandId.GreenLeds:
                    _stageKitGreenParam = parameter;
                    UpdateLightPodColor(parameter, new Color(0, 255, 0), 16);
                    break;

                case StageKitTalker.CommandId.YellowLeds:
                    _stageKitYellowParam = parameter;
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
                case StageKitTalker.CommandId.StrobeMedium:
                case StageKitTalker.CommandId.StrobeFast:
                case StageKitTalker.CommandId.StrobeFastest:
                    StartStrobeEffect(commandId, UdpIntake.BeatsPerMinute.Value);
                    break;

                case StageKitTalker.CommandId.DisableAll:
                    StopStrobeEffect();
                    StopBreathingEffect();
                    SetDeviceBrightness(0);
                    SetZoneBrightness(0);
                    _stageKitBlueParam = 0;
                    _stageKitRedParam = 0;
                    _stageKitGreenParam = 0;
                    _stageKitYellowParam = 0;
                    UpdateLightPodColor(0, new Color(0, 0, 0), 0);
                    UpdateLightPodColor(0, new Color(0, 0, 0), 8);
                    UpdateLightPodColor(0, new Color(0, 0, 0), 16);
                    UpdateLightPodColor(0, new Color(0, 0, 0), 24);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OnStageKitEvent error: {ex.Message}");
        }
    }

    #endregion

    #region Strobe & Fogger Effects

    private void StartStrobeEffect(StageKitTalker.CommandId commandId, float bpm)
    {
        StopStrobeEffect();

        if (_mainViewModel?.OpenRgbStrobeMode == StrobeOutputModes.ManualFlash)
        {
            _manualStrobeFlasher.Start(commandId, bpm, SetStrobeFlashStateAsync);
        }
        else
        {
            // Default Strobe Command mode
            SetStrobeFlashStateAsync(true, CancellationToken.None);
        }
    }

    private void StopStrobeEffect()
    {
        _manualStrobeFlasher.Stop(SetStrobeFlashStateAsync);
        SetStrobeFlashStateAsync(false, CancellationToken.None);
    }

    private Task SetStrobeFlashStateAsync(bool isOn, CancellationToken cancellationToken)
    {
        if (UsbDeviceMonitor.IsOutputSuppressed)
        {
            isOn = false;
        }

        _isStrobeActive = isOn;
        Device[] strobeDevices;
        ZoneInfo[] strobeZones;
        lock (Lock)
        {
            strobeDevices = StrobeList.ToArray();
            strobeZones = StrobeZones.Values.ToArray();
        }

        foreach (var device in strobeDevices)
        {
            if (IsDeviceIdentifying(device.Index)) continue;

            if (LightPodStrobeDevices.Contains(device.Index))
            {
                Color[]? lpColors;
                lock (Lock)
                {
                    LightPodStates.TryGetValue(device.Index, out lpColors);
                    lpColors = lpColors?.ToArray();
                }
                if (lpColors != null)
                {
                    Color[] blended = new Color[lpColors.Length];
                    for (int i = 0; i < lpColors.Length; i++)
                    {
                        var c = lpColors[i];
                        blended[i] = (c.R != 0 || c.G != 0 || c.B != 0) ? c : (isOn ? new Color(255, 255, 255) : new Color(0, 0, 0));
                    }
                    client?.UpdateLeds(device.Index, blended);
                }
            }
            else
            {
                ToggleDeviceLeds(device, isOn);
            }
        }

        foreach (var zoneInfo in strobeZones)
        {
            var zoneKey = $"{zoneInfo.Device.Index}_{zoneInfo.ZoneIndex}";
            if (IsZoneIdentifying(zoneKey) || IsDeviceIdentifying(zoneInfo.Device.Index)) continue;

            if (LightPodStrobeZones.Contains(zoneKey))
            {
                Color[]? lpColors;
                lock (Lock)
                {
                    LightPodZoneStates.TryGetValue(zoneKey, out lpColors);
                    lpColors = lpColors?.ToArray();
                }
                if (lpColors != null)
                {
                    Color[] blended = new Color[lpColors.Length];
                    for (int i = 0; i < lpColors.Length; i++)
                    {
                        var c = lpColors[i];
                        blended[i] = (c.R != 0 || c.G != 0 || c.B != 0) ? c : (isOn ? new Color(255, 255, 255) : new Color(0, 0, 0));
                    }
                    UpdateZoneLedsSafely(zoneInfo.Device, zoneInfo.ZoneIndex, blended);
                }
            }
            else
            {
                ToggleZoneLeds(zoneInfo, isOn);
            }
        }

        return Task.CompletedTask;
    }

    private void StartBreathingEffect()
    {
        StopBreathingEffect();
        _fogCts = new CancellationTokenSource();
        var token = _fogCts.Token;
        _fogTask = Task.Run(async () =>
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    for (int brightness = 0; brightness <= 255; brightness += 5)
                    {
                        SetDeviceBrightness((byte)brightness);
                        SetZoneBrightness((byte)brightness);
                        await Task.Delay(30, token);
                    }

                    for (int brightness = 255; brightness >= 0; brightness -= 5)
                    {
                        SetDeviceBrightness((byte)brightness);
                        SetZoneBrightness((byte)brightness);
                        await Task.Delay(30, token);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }, token);
    }

    private void StopBreathingEffect()
    {
        try
        {
            if (_fogCts != null && !_fogCts.IsCancellationRequested)
            {
                _fogCts.Cancel();
            }
        }
        catch (ObjectDisposedException) { }
    }

    private void SetDeviceBrightness(byte brightness)
    {
        if (brightness > 0 && UsbDeviceMonitor.IsOutputSuppressed)
        {
            return;
        }

        Device[] foggerDevices;
        lock (Lock)
        {
            foggerDevices = FoggerList.ToArray();
        }

        foreach (var device in foggerDevices)
        {
            try
            {
                if (IsDeviceIdentifying(device.Index)) continue;
                var colors = Enumerable.Repeat(new Color(brightness, brightness, brightness), device.Leds.Length).ToArray();

                lock (Lock)
                {
                    var buffer = GetDeviceColorBuffer(device);
                    Array.Copy(colors, buffer, buffer.Length);
                }

                client?.UpdateLeds(device.Index, colors);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SetDeviceBrightness error: {ex.Message}");
            }
        }
    }

    private void SetZoneBrightness(byte brightness)
    {
        if (brightness > 0 && UsbDeviceMonitor.IsOutputSuppressed)
        {
            return;
        }

        ZoneInfo[] foggerZones;
        lock (Lock)
        {
            foggerZones = FoggerZones.Values.ToArray();
        }

        foreach (var zoneInfo in foggerZones)
        {
            try
            {
                var zoneKey = $"{zoneInfo.Device.Index}_{zoneInfo.ZoneIndex}";
                if (IsZoneIdentifying(zoneKey) || IsDeviceIdentifying(zoneInfo.Device.Index)) continue;
                var color = new Color(brightness, brightness, brightness);
                var colors = Enumerable.Repeat(color, (int)zoneInfo.Zone.LedCount).ToArray();
                UpdateZoneLedsSafely(zoneInfo.Device, zoneInfo.ZoneIndex, colors);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SetZoneBrightness error: {ex.Message}");
            }
        }
    }

    #endregion

    // Custom per-LED StageKit area mapping (Key: $"{deviceIndex}_{zoneIndex}" or $"dev_{deviceIndex}")
    public Dictionary<string, int[]> CustomLedMappings { get; } = new();

    #region LightPod High-Performance Coalesced Flush Engine

    private void UpdateLightPodColor(byte parameter, Color color, int areaOffset)
    {
        Device[] lightPodDevices;
        KeyValuePair<string, ZoneInfo>[] lightPodZones;
        lock (Lock)
        {
            if (LightPodList.Count == 0 && LightPodZones.Count == 0) return;
            lightPodDevices = LightPodList.ToArray();
            lightPodZones = LightPodZones.ToArray();
        }

        const int numAreas = 32;

        // Legacy device-based lightpods: update memory state instantly (0ms)
        foreach (var device in lightPodDevices)
        {
            var keysPerArea = Math.Max(1, device.Leds.Length / numAreas);
            Color[]? colors;
            int[]? customMap;
            lock (Lock)
            {
                if (!LightPodStates.TryGetValue(device.Index, out colors)) continue;
                CustomLedMappings.TryGetValue($"dev_{device.Index}", out customMap);
            }

            if (customMap != null && customMap.Length == device.Leds.Length)
            {
                for (int ledIndex = 0; ledIndex < device.Leds.Length; ledIndex++)
                {
                    int area = customMap[ledIndex];
                    if (area < 0)
                    {
                        colors[ledIndex] = new Color(0, 0, 0);
                    }
                    else if (area >= areaOffset && area < areaOffset + 8)
                    {
                        colors[ledIndex] = (parameter & (1 << (area - areaOffset))) != 0 ? color : new Color(0, 0, 0);
                    }
                }
            }
            else
            {
                for (int area = areaOffset; area < areaOffset + 8; area++)
                {
                    for (int key = 0; key < keysPerArea; key++)
                    {
                        var ledIndex = area * keysPerArea + key;
                        if (ledIndex >= device.Leds.Length) continue;
                        colors[ledIndex] = (parameter & (1 << (area - areaOffset))) != 0 ? color : new Color(0, 0, 0);
                    }
                }
            }
        }

        // New zone-based lightpods: update memory state instantly (0ms)
        foreach (var kvp in lightPodZones)
        {
            var zoneInfo = kvp.Value;
            var zoneKey = kvp.Key;

            var keysPerArea = Math.Max(1, (int)zoneInfo.Zone.LedCount / numAreas);
            Color[]? colors;
            Color[]? rawColors;
            int[]? customMap;
            OpenRgbBlendMode blendMode = OpenRgbBlendMode.Discrete;

            lock (Lock)
            {
                if (!LightPodZoneStates.TryGetValue(zoneKey, out colors)) continue;
                if (!LightPodZoneRawStates.TryGetValue(zoneKey, out rawColors) || rawColors.Length != colors.Length)
                {
                    rawColors = new Color[colors.Length];
                    LightPodZoneRawStates[zoneKey] = rawColors;
                }
                CustomLedMappings.TryGetValue(zoneKey, out customMap);
                ZoneBlendModes.TryGetValue(zoneKey, out blendMode);
            }

            if (customMap != null && customMap.Length == (int)zoneInfo.Zone.LedCount)
            {
                for (int ledIndex = 0; ledIndex < (int)zoneInfo.Zone.LedCount; ledIndex++)
                {
                    int area = customMap[ledIndex];
                    if (area < 0)
                    {
                        rawColors[ledIndex] = new Color(0, 0, 0);
                    }
                    else if (area >= areaOffset && area < areaOffset + 8)
                    {
                        rawColors[ledIndex] = (parameter & (1 << (area - areaOffset))) != 0 ? color : new Color(0, 0, 0);
                    }
                }
            }
            else
            {
                for (int area = areaOffset; area < areaOffset + 8; area++)
                {
                    for (int key = 0; key < keysPerArea; key++)
                    {
                        var ledIndex = area * keysPerArea + key;
                        if (ledIndex >= zoneInfo.Zone.LedCount) continue;
                        rawColors[ledIndex] = (parameter & (1 << (area - areaOffset))) != 0 ? color : new Color(0, 0, 0);
                    }
                }
            }

            ApplyZoneBlending(rawColors, colors, blendMode);
        }

        // Offload TCP socket flush to background task with frame coalescing
        TriggerLightPodFlush();
    }

    private void ApplyZoneBlending(Color[] raw, Color[] output, OpenRgbBlendMode mode)
    {
        if (raw.Length != output.Length || raw.Length == 0) return;

        if (mode == OpenRgbBlendMode.Discrete)
        {
            Array.Copy(raw, output, raw.Length);
            return;
        }

        // Calculate the 8 StageKit Pod mixed colors (Pod 1 to Pod 8) from active B, R, G, Y
        Span<Color> podColors = stackalloc Color[8];
        int activePodCount = 0;

        for (int k = 0; k < 8; k++)
        {
            bool isB = (_stageKitBlueParam & (1 << k)) != 0;
            bool isR = (_stageKitRedParam & (1 << k)) != 0;
            bool isG = (_stageKitGreenParam & (1 << k)) != 0;
            bool isY = (_stageKitYellowParam & (1 << k)) != 0;

            int r = 0, g = 0, b = 0, count = 0;
            if (isB) { b += 255; count++; }
            if (isR) { r += 255; count++; }
            if (isG) { g += 255; count++; }
            if (isY) { r += 255; g += 255; count++; } // Yellow has R + G

            if (count == 0)
            {
                podColors[k] = new Color(0, 0, 0); // Black / Desligado
            }
            else
            {
                int max = Math.Max(r, Math.Max(g, b));
                double scale = max > 0 ? 255.0 / max : 1.0;
                podColors[k] = new Color(
                    (byte)Math.Clamp(r * scale, 0, 255),
                    (byte)Math.Clamp(g * scale, 0, 255),
                    (byte)Math.Clamp(b * scale, 0, 255)
                );
                activePodCount++;
            }
        }

        if (mode == OpenRgbBlendMode.UniformWash)
        {
            if (activePodCount == 0)
            {
                Array.Clear(output, 0, output.Length);
                return;
            }

            int sumR = 0, sumG = 0, sumB = 0;
            for (int k = 0; k < 8; k++)
            {
                var pc = podColors[k];
                if (pc.R != 0 || pc.G != 0 || pc.B != 0)
                {
                    sumR += pc.R;
                    sumG += pc.G;
                    sumB += pc.B;
                }
            }

            int max = Math.Max(sumR, Math.Max(sumG, sumB));
            double scale = max > 0 ? 255.0 / max : 1.0;
            var washColor = new Color(
                (byte)Math.Clamp(sumR * scale, 0, 255),
                (byte)Math.Clamp(sumG * scale, 0, 255),
                (byte)Math.Clamp(sumB * scale, 0, 255)
            );

            for (int i = 0; i < output.Length; i++)
            {
                output[i] = washColor;
            }
        }
        else if (mode == OpenRgbBlendMode.SmoothGradient)
        {
            if (activePodCount == 0)
            {
                Array.Clear(output, 0, output.Length);
                return;
            }

            int n = output.Length;
            if (n == 1)
            {
                output[0] = podColors[0];
                return;
            }

            // Distribute the 8 Pod Colors evenly across the N LEDs of the strip with smooth linear interpolation
            for (int i = 0; i < n; i++)
            {
                double pos = (double)i / (n - 1) * 7.0; // Continuous range [0.0 .. 7.0]
                int k0 = (int)pos;
                int k1 = Math.Min(7, k0 + 1);
                double t = pos - k0;

                var c0 = podColors[k0];
                var c1 = podColors[k1];

                byte r = (byte)Math.Clamp(c0.R + (c1.R - c0.R) * t, 0, 255);
                byte g = (byte)Math.Clamp(c0.G + (c1.G - c0.G) * t, 0, 255);
                byte b = (byte)Math.Clamp(c0.B + (c1.B - c0.B) * t, 0, 255);

                output[i] = new Color(r, g, b);
            }
        }
    }

    public void ResetLightPodColors()
    {
        lock (Lock)
        {
            _stageKitBlueParam = 0;
            _stageKitRedParam = 0;
            _stageKitGreenParam = 0;
            _stageKitYellowParam = 0;
            foreach (var colors in LightPodStates.Values)
            {
                Array.Clear(colors, 0, colors.Length);
            }
            foreach (var colors in LightPodZoneStates.Values)
            {
                Array.Clear(colors, 0, colors.Length);
            }
            foreach (var colors in LightPodZoneRawStates.Values)
            {
                Array.Clear(colors, 0, colors.Length);
            }
        }
        TriggerLightPodFlush();
    }

    private void TriggerLightPodFlush()
    {
        lock (_lightPodSendLock)
        {
            if (_isLightPodSendPending) return;
            _isLightPodSendPending = true;
        }

        Task.Run(async () =>
        {
            try
            {
                // Coalesce multi-color byte updates in the same frame (15ms window ~60 FPS)
                await Task.Delay(15);

                Device[] lightPodDevices;
                KeyValuePair<string, ZoneInfo>[] lightPodZones;
                lock (Lock)
                {
                    lightPodDevices = LightPodList.ToArray();
                    lightPodZones = LightPodZones.ToArray();
                }

                foreach (var device in lightPodDevices)
                {
                    try
                    {
                        if (IsDeviceIdentifying(device.Index)) continue;
                        Color[]? colors;
                        lock (Lock)
                        {
                            if (!LightPodStates.TryGetValue(device.Index, out colors)) continue;
                            colors = colors.ToArray();
                        }

                        if (LightPodStrobeDevices.Contains(device.Index) && _isStrobeActive)
                        {
                            Color[] blended = new Color[colors.Length];
                            for (int i = 0; i < colors.Length; i++)
                            {
                                var c = colors[i];
                                blended[i] = (c.R != 0 || c.G != 0 || c.B != 0) ? c : new Color(255, 255, 255);
                            }
                            colors = blended;
                        }

                        client?.UpdateLeds(device.Index, colors);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"FlushLightPodLeds device error: {ex.Message}");
                    }
                }

                foreach (var kvp in lightPodZones)
                {
                    try
                    {
                        var zoneInfo = kvp.Value;
                        var zoneKey = kvp.Key;
                        if (IsZoneIdentifying(zoneKey) || IsDeviceIdentifying(zoneInfo.Device.Index)) continue;

                        Color[]? colors;
                        lock (Lock)
                        {
                            if (!LightPodZoneStates.TryGetValue(zoneKey, out colors)) continue;
                            colors = colors.ToArray();
                        }

                        if (LightPodStrobeZones.Contains(zoneKey) && _isStrobeActive)
                        {
                            Color[] blended = new Color[colors.Length];
                            for (int i = 0; i < colors.Length; i++)
                            {
                                var c = colors[i];
                                blended[i] = (c.R != 0 || c.G != 0 || c.B != 0) ? c : new Color(255, 255, 255);
                            }
                            colors = blended;
                        }

                        UpdateZoneLedsSafely(zoneInfo.Device, zoneInfo.ZoneIndex, colors);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"FlushLightPodLeds zone error: {ex.Message}");
                    }
                }
            }
            finally
            {
                lock (_lightPodSendLock)
                {
                    _isLightPodSendPending = false;
                }
            }
        });
    }

    #endregion

    #region Visual Identification

    public void IdentifyDevice(Device device)
    {
        if (client == null) return;
        lock (Lock)
        {
            if (_identifyingDevices.Contains(device.Index)) return;
            _identifyingDevices.Add(device.Index);
        }

        Task.Run(async () =>
        {
            try
            {
                var whiteColors = Enumerable.Repeat(new Color(255, 255, 255), device.Leds.Length).ToArray();
                var blackColors = Enumerable.Repeat(new Color(0, 0, 0), device.Leds.Length).ToArray();

                for (int i = 0; i < 4; i++)
                {
                    client?.UpdateLeds(device.Index, whiteColors);
                    await Task.Delay(180);
                    client?.UpdateLeds(device.Index, blackColors);
                    await Task.Delay(180);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Identify device error: {ex.Message}");
            }
            finally
            {
                lock (Lock)
                {
                    _identifyingDevices.Remove(device.Index);
                }
            }
        });
    }

    public void IdentifyZone(Device device, int zoneIndex, Zone zone)
    {
        if (client == null) return;
        var key = $"{device.Index}_{zoneIndex}";
        lock (Lock)
        {
            if (_identifyingZones.Contains(key)) return;
            _identifyingZones.Add(key);
        }

        Task.Run(async () =>
        {
            try
            {
                var whiteColors = Enumerable.Repeat(new Color(255, 255, 255), (int)zone.LedCount).ToArray();
                var blackColors = Enumerable.Repeat(new Color(0, 0, 0), (int)zone.LedCount).ToArray();

                for (int i = 0; i < 4; i++)
                {
                    UpdateZoneLedsSafely(device, zoneIndex, whiteColors);
                    await Task.Delay(180);
                    UpdateZoneLedsSafely(device, zoneIndex, blackColors);
                    await Task.Delay(180);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Identify zone error: {ex.Message}");
            }
            finally
            {
                lock (Lock)
                {
                    _identifyingZones.Remove(key);
                }
            }
        });
    }

    #endregion
}
