// implementation based on https://lan.developer.lifx.com
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YALCY.Integrations;
using YALCY.Integrations.StageKit;
using YALCY.Udp;
using YALCY.Usb;
using YALCY.ViewModels;
using YALCY.Views.Components;

namespace YALCY.Integrations.Lifx;

public sealed class LifxTalker : IDisposable
{
    private const int LifxLanPort = 56700;
    private const int HeaderSize = 36;
    private const int DiscoveryWindowMs = 1200;
    private const int DetailsWindowMs = 1200;
    private const int ZoneWindowMs = 1200;
    private const uint DefaultTransitionMs = 150;
    private const ushort FullPower = ushort.MaxValue;
    private const ushort DefaultKelvin = 3500; //It looks like different devices have different ranges of supported kelvin values, so I just picked a default middle value. Could hardcode a full list later.
    private const ushort FullColorValue = ushort.MaxValue;

    // These are the only Stage Kit colors we ever send to LIFX, so keep them as direct raw HSBK presets.
    //Hue = int(round(0x10000 * hue) / 360)) % 0x10000
    private static readonly LifxHsbk StageKitRed = new(0x0000, FullColorValue, FullColorValue, DefaultKelvin); //Hue = 0
    private static readonly LifxHsbk StageKitYellow = new(0x2AAB, FullColorValue, FullColorValue, DefaultKelvin); // Hue = 60
    private static readonly LifxHsbk StageKitGreen = new(0x5555, FullColorValue, FullColorValue, DefaultKelvin); // Hue = 120
    private static readonly LifxHsbk StageKitBlue = new(0xAAAB, FullColorValue, FullColorValue, DefaultKelvin); // Hue = 240
    private static readonly LifxHsbk StrobeWhite = new(0xFFFF, 0, FullColorValue, DefaultKelvin);
    private static readonly LifxHsbk StrobeOffish = new(0x0000, 0, 1, DefaultKelvin);

    private readonly object _deviceLock = new();
    private readonly object _socketLock = new();
    private readonly uint _sourceId = CreateSourceId();
    private int _sequenceCounter;

    private MainWindowViewModel? _mainViewModel;
    private UdpClient? _commandClient;
    private List<LifxLanDeviceModel> _devices = new();
    private bool _isEnabled;
    private bool _isSubscribedToStageKit;
    private bool _discoveryCompleted;
    private readonly ManualStrobeFlasher _manualStrobeFlasher = new(ex => Console.WriteLine($"LIFX manual strobe error: {ex.Message}"));

    public async Task EnableLifxLan(bool isEnabled, MainWindowViewModel? viewModel = null)
    {
        if (viewModel != null)
        {
            _mainViewModel = viewModel;
        }

        if (isEnabled)
        {
            _isEnabled = true;
            EnsureCommandClient();
            SubscribeToStageKit();
            UpdateViewModelStatus("LIFX status: Discovering devices...", string.Empty);
            StatusFooter.UpdateStatus("LIFX", IntegrationStatus.Connecting);
            await DiscoverDevicesAsync(_mainViewModel);
            _discoveryCompleted = true;
            SaveColors(_devices);
            return;
        }

        _isEnabled = false;
        _manualStrobeFlasher.Stop(SetManualStrobeStateAsync);

        if (_discoveryCompleted)
        {
            RestoreColors(_devices);
        }

        UnsubscribeFromStageKit();

        lock (_socketLock)
        {
            _commandClient?.Dispose();
            _commandClient = null;
        }

        StatusFooter.UpdateStatus("LIFX", IntegrationStatus.Off);
        UpdateViewModelStatus("LIFX status: Disabled.", string.Empty);
    }

    public async Task DiscoverDevicesAsync(MainWindowViewModel? viewModel = null)
    {
        if (viewModel != null)
        {
            _mainViewModel = viewModel;
        }

        var shouldUpdateFooter = _isEnabled;

        if (shouldUpdateFooter)
        {
            StatusFooter.UpdateStatus("LIFX", IntegrationStatus.Connecting);
        }

        UpdateViewModelStatus("LIFX status: Discovering devices...", string.Empty);

        try
        {
            var devices = await DiscoverDevicesCoreAsync();

            lock (_deviceLock)
            {
                _devices = devices;
            }

            UpdateViewModelDevices(devices);

            if (devices.Count == 0)
            {
                UpdateViewModelStatus(
                    "LIFX status: No LIFX devices discovered on the local network.",
                    "Make sure your bulbs are powered on and reachable on the same LAN segment.");

                if (shouldUpdateFooter)
                {
                    StatusFooter.UpdateStatus("LIFX", IntegrationStatus.Error);
                }

                return;
            }

            var zoneCount = devices.Sum(device => device.Zones.Count);
            UpdateViewModelStatus(
                $"LIFX status: Discovered {devices.Count} device(s) with {zoneCount} controllable zone(s).",
                "Each zone can be mapped to one Stage Kit light below.");

            if (shouldUpdateFooter)
            {
                StatusFooter.UpdateStatus("LIFX", IntegrationStatus.Connected);
            }
        }
        catch (Exception ex)
        {
            UpdateViewModelStatus("LIFX status: Discovery failed.", $"Error: {ex.Message}");

            if (shouldUpdateFooter)
            {
                StatusFooter.UpdateStatus("LIFX", IntegrationStatus.Error);
            }
        }
    }

    private async Task<List<LifxLanDeviceModel>> DiscoverDevicesCoreAsync()
    {
        using var discoveryClient = CreateUdpClient();
        var discoveredDevices = new Dictionary<string, LifxLanDeviceModel>(StringComparer.OrdinalIgnoreCase);

        var getServicePacket = BuildPacket(2, null, ReadOnlySpan<byte>.Empty, tagged: true, ackRequired: false, resRequired: false);
        foreach (var endpoint in GetBroadcastEndpoints())
        {
            await discoveryClient.SendAsync(getServicePacket, getServicePacket.Length, endpoint);
        }

        using (var discoveryTimeout = new CancellationTokenSource(DiscoveryWindowMs))
        {
            while (true)
            {
                try
                {
                    var result = await discoveryClient.ReceiveAsync(discoveryTimeout.Token);
                    ParseDiscoveryPacket(result.Buffer, result.RemoteEndPoint, discoveredDevices);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        if (discoveredDevices.Count == 0)
        {
            return new List<LifxLanDeviceModel>();
        }

        foreach (var device in discoveredDevices.Values)
        {
            var endpoint = new IPEndPoint(device.Address, device.Port);
            var getColorPacket = BuildPacket(101, device.Target, ReadOnlySpan<byte>.Empty, tagged: false, ackRequired: false, resRequired: false);
            await discoveryClient.SendAsync(getColorPacket, getColorPacket.Length, endpoint);
        }

        using (var detailsTimeout = new CancellationTokenSource(DetailsWindowMs))
        {
            while (true)
            {
                try
                {
                    var result = await discoveryClient.ReceiveAsync(detailsTimeout.Token);
                    ParseDetailsPacket(result.Buffer, discoveredDevices);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        foreach (var device in discoveredDevices.Values)
        {
            var endpoint = new IPEndPoint(device.Address, device.Port);
            var getZonesPacket = BuildGetColorZonesPacket(device.Target);
            await discoveryClient.SendAsync(getZonesPacket, getZonesPacket.Length, endpoint);
        }

        using (var zoneTimeout = new CancellationTokenSource(ZoneWindowMs))
        {
            while (true)
            {
                try
                {
                    var result = await discoveryClient.ReceiveAsync(zoneTimeout.Token);
                    ParseZonePacket(result.Buffer, discoveredDevices);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        EnsureDeviceZones(discoveredDevices.Values);
        ApplyZoneAssignments(discoveredDevices.Values);

        return discoveredDevices.Values
            .OrderBy(device => device.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(device => device.Serial, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void SubscribeToStageKit()
    {
        if (_isSubscribedToStageKit)
        {
            return;
        }

        UsbDeviceMonitor.OnStageKitCommand += OnStageKitEvent;
        _isSubscribedToStageKit = true;
    }

    private void UnsubscribeFromStageKit()
    {
        if (!_isSubscribedToStageKit)
        {
            return;
        }

        UsbDeviceMonitor.OnStageKitCommand -= OnStageKitEvent;
        _isSubscribedToStageKit = false;
    }

    private void OnStageKitEvent(StageKitTalker.CommandId commandId, byte parameter)
    {
        try
        {
            switch (commandId)
            {
                case StageKitTalker.CommandId.BlueLeds:
                    ApplyColorState(commandId, parameter, GetStageKitColor(commandId));
                    break;

                case StageKitTalker.CommandId.GreenLeds:
                    ApplyColorState(commandId, parameter, GetStageKitColor(commandId));
                    break;

                case StageKitTalker.CommandId.RedLeds:
                    ApplyColorState(commandId, parameter, GetStageKitColor(commandId));
                    break;

                case StageKitTalker.CommandId.YellowLeds:
                    ApplyColorState(commandId, parameter, GetStageKitColor(commandId));
                    break;
                
                case StageKitTalker.CommandId.StrobeFastest:
                    HandleStrobeState(commandId, 4);
                    break;
                case StageKitTalker.CommandId.StrobeFast:
                    HandleStrobeState(commandId, 3);
                    break;
                case StageKitTalker.CommandId.StrobeMedium:
                    HandleStrobeState(commandId, 2);
                    break;
                case StageKitTalker.CommandId.StrobeSlow:
                    HandleStrobeState(commandId, 1);
                    break;
                case StageKitTalker.CommandId.StrobeOff:
                    _manualStrobeFlasher.Stop(SetManualStrobeStateAsync);
                    if (_mainViewModel?.LifxStrobeMode != StrobeOutputModes.ManualFlash)
                    {
                        ApplyStrobeState(commandId, 1, 1000, true);
                    }
                    break;
                case StageKitTalker.CommandId.DisableAll:
                    _manualStrobeFlasher.Stop(SetManualStrobeStateAsync);
                    DisableAssignedZones();
                    break;
            }
        }
        catch (Exception ex)
        {
            UpdateViewModelStatus("LIFX status: Error while sending cue data.", $"Error: {ex.Message}");

            if (_isEnabled)
            {
                StatusFooter.UpdateStatus("LIFX", IntegrationStatus.Error);
            }
        }
    }

    private void HandleStrobeState(StageKitTalker.CommandId commandId, int speed)
    {
        if (_mainViewModel?.LifxStrobeMode == StrobeOutputModes.ManualFlash)
        {
            _manualStrobeFlasher.Start(commandId, UdpIntake.BeatsPerMinute.Value, SetManualStrobeStateAsync);
            return;
        }

        _manualStrobeFlasher.Stop(SetManualStrobeStateAsync);
        ApplyStrobeState(commandId, 1, CalculateStrobeSpeed(speed));
    }

    private void ApplyStrobeState(StageKitTalker.CommandId commandId, byte activeSlotsMask, int periodMs, bool cancel = false)
    {
        var devices = SnapshotDevices();
        if (devices.Count == 0)
        {
            return;
        }
        
        EnsureCommandClient();

        lock (_socketLock)
        {
            foreach (var device in devices)
            {
                foreach (var zone in device.Zones)
                {
                    if (!LifxStageAssignments.TryGetAssignmentSlot(zone.AssignedStageLight, commandId, out var slotIndex))
                    {
                        continue;
                    }
                    
                    var shouldEnable = (activeSlotsMask & (1 << slotIndex)) != 0;
                    if (!shouldEnable)
                    {
                        continue;
                    }
                    
                    SendStrobe(device, (uint) periodMs, cancel);
                }
            }
        }
    }

    private Task SetManualStrobeStateAsync(bool isOn, CancellationToken cancellationToken)
    {
        var devices = SnapshotDevices();
        if (devices.Count == 0)
        {
            return Task.CompletedTask;
        }

        EnsureCommandClient();

        lock (_socketLock)
        {
            foreach (var device in devices)
            {
                var changedZones = new List<int>();

                foreach (var zone in device.Zones)
                {
                    if (!string.Equals(zone.AssignedStageLight, "Strobe 1", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var nextColor = isOn ? StrobeWhite : zone.CurrentColor.WithBrightness(0);
                    if (zone.CurrentColor == nextColor)
                    {
                        continue;
                    }

                    zone.CurrentColor = nextColor;
                    changedZones.Add(zone.ZoneIndex);
                }

                if (changedZones.Count > 0)
                {
                    SendDeviceState(device, changedZones);
                }
            }
        }

        return Task.CompletedTask;
    }

    private void ApplyColorState(StageKitTalker.CommandId commandId, byte activeSlotsMask, LifxHsbk activeColor)
    {
        var devices = SnapshotDevices();
        if (devices.Count == 0)
        {
            return;
        }

        EnsureCommandClient();

        lock (_socketLock)
        {
            foreach (var device in devices)
            {
                var changedZones = new List<int>();

                foreach (var zone in device.Zones)
                {
                    if (!LifxStageAssignments.TryGetAssignmentSlot(zone.AssignedStageLight, commandId, out var slotIndex))
                    {
                        continue;
                    }

                    var shouldEnable = (activeSlotsMask & (1 << slotIndex)) != 0;
                    var nextColor = shouldEnable
                        ? activeColor
                        // Keep hue/saturation when dimming to black so the transition doesn't wash through Kelvin white.
                        : zone.CurrentColor.WithBrightness(0);

                    if (zone.CurrentColor == nextColor)
                    {
                        continue;
                    }

                    zone.CurrentColor = nextColor;
                    changedZones.Add(zone.ZoneIndex);
                }

                if (changedZones.Count > 0)
                {
                    SendDeviceState(device, changedZones);
                }
            }
        }
    }

    private void DisableAssignedZones()
    {
        var devices = SnapshotDevices();
        if (devices.Count == 0)
        {
            return;
        }

        EnsureCommandClient();

        lock (_socketLock)
        {
            foreach (var device in devices)
            {
                var changedZones = new List<int>();

                foreach (var zone in device.Zones)
                {
                    if (string.Equals(zone.AssignedStageLight, LifxStageAssignments.Unassigned, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var nextColor = zone.CurrentColor.WithBrightness(0);
                    if (zone.CurrentColor == nextColor)
                    {
                        continue;
                    }

                    zone.CurrentColor = nextColor;
                    changedZones.Add(zone.ZoneIndex);
                }

                if (changedZones.Count > 0)
                {
                    SendDeviceState(device, changedZones);
                }
            }
        }
    }

    private void SendDeviceState(LifxLanDeviceModel device, IReadOnlyList<int> changedZoneIndices)
    {
        if (device.Zones.Count <= 1)
        {
            SendSingleZoneState(device);
            return;
        }

        var shouldBePowered = device.Zones.Any(zone => zone.CurrentColor.IsOn);
        if (shouldBePowered)
        {
            SendLightPower(device, true, DefaultTransitionMs);
        }

        var changedSegments = BuildChangedSegments(device, changedZoneIndices);
        for (var i = 0; i < changedSegments.Count; i++)
        {
            var apply = i == changedSegments.Count - 1
                ? MultiZoneApply.Apply
                : MultiZoneApply.NoApply;

            var segment = changedSegments[i];
            SendColorZones(device, segment.StartIndex, segment.EndIndex, segment.Color, DefaultTransitionMs, apply);
        }

        if (!shouldBePowered)
        {
            SendLightPower(device, false, DefaultTransitionMs);
        }

        device.IsPowered = shouldBePowered;
    }

    private void SendSingleZoneState(LifxLanDeviceModel device)
    {
        var zone = device.Zones.FirstOrDefault();
        if (zone == null)
        {
            return;
        }

        var shouldBePowered = zone.CurrentColor.IsOn;

        if (shouldBePowered)
        {
            SendLightPower(device, true, DefaultTransitionMs);
        }

        SendColor(device, zone.CurrentColor, DefaultTransitionMs);

        if (!shouldBePowered)
        {
            SendLightPower(device, false, DefaultTransitionMs);
        }

        device.IsPowered = shouldBePowered;
        device.BaseColor = zone.CurrentColor;
    }

    private void SendStrobe(LifxLanDeviceModel device, uint periodMs, bool cancel)
    {
        var activeColor = cancel ? StrobeOffish : StrobeWhite;
        SendStrobeWaveform(device, activeColor, periodMs, cancel);
        if (cancel)
        {
            SendColor(device, StrobeOffish, DefaultTransitionMs);
        }
    }
    
    private void SendStrobeWaveform(LifxLanDeviceModel device, LifxHsbk color, uint periodMs, bool cancel)
    {
        float cycles = cancel ? 0 : 1e30f;
        
        Span<byte> payload = stackalloc byte[21];
        payload[0] = 0;
        // 1 means the effect is transient, when we disable the strobe the light will revert to its previous state
        payload[1] = (byte)1;
        
        BinaryPrimitives.WriteUInt16LittleEndian(payload[2..4], color.Hue);
        BinaryPrimitives.WriteUInt16LittleEndian(payload[4..6], color.Saturation);
        BinaryPrimitives.WriteUInt16LittleEndian(payload[6..8], color.Brightness);
        BinaryPrimitives.WriteUInt16LittleEndian(payload[8..10], color.Kelvin);
        
        BinaryPrimitives.WriteUInt32LittleEndian(payload[10..14], periodMs);
        
        BinaryPrimitives.WriteSingleLittleEndian(payload[14..18], cycles);
        
        // 50% duty cycle
        BinaryPrimitives.WriteInt16LittleEndian(payload[18..20], 0);

        payload[20] = 4;
        SendPacket(device, 103, payload);
    }
    
    private void SendColor(LifxLanDeviceModel device, LifxHsbk color, uint durationMs)
    {
        Span<byte> payload = stackalloc byte[13];
        payload[0] = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(payload[1..3], color.Hue);
        BinaryPrimitives.WriteUInt16LittleEndian(payload[3..5], color.Saturation);
        BinaryPrimitives.WriteUInt16LittleEndian(payload[5..7], color.Brightness);
        BinaryPrimitives.WriteUInt16LittleEndian(payload[7..9], color.Kelvin);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[9..13], durationMs);
        SendPacket(device, 102, payload);
    }

    private void SendColorZones(
        LifxLanDeviceModel device,
        int startIndex,
        int endIndex,
        LifxHsbk color,
        uint durationMs,
        MultiZoneApply apply)
    {
        Span<byte> payload = stackalloc byte[15];
        payload[0] = (byte)startIndex;
        payload[1] = (byte)endIndex;
        BinaryPrimitives.WriteUInt16LittleEndian(payload[2..4], color.Hue);
        BinaryPrimitives.WriteUInt16LittleEndian(payload[4..6], color.Saturation);
        BinaryPrimitives.WriteUInt16LittleEndian(payload[6..8], color.Brightness);
        BinaryPrimitives.WriteUInt16LittleEndian(payload[8..10], color.Kelvin);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[10..14], durationMs);
        payload[14] = (byte)apply;
        SendPacket(device, 501, payload);
    }

    private void SendLightPower(LifxLanDeviceModel device, bool turnOn, uint durationMs)
    {
        Span<byte> payload = stackalloc byte[6];
        BinaryPrimitives.WriteUInt16LittleEndian(payload[0..2], turnOn ? FullPower : (ushort)0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[2..6], durationMs);
        SendPacket(device, 117, payload);
    }

    private void SendPacket(LifxLanDeviceModel device, ushort messageType, ReadOnlySpan<byte> payload)
    {
        if (_commandClient == null)
        {
            return;
        }

        var packet = BuildPacket(messageType, device.Target, payload, tagged: false, ackRequired: false, resRequired: false);
        _commandClient.Send(packet, packet.Length, new IPEndPoint(device.Address, device.Port));
    }

    private void EnsureCommandClient()
    {
        lock (_socketLock)
        {
            _commandClient ??= CreateUdpClient();
        }
    }

    private static UdpClient CreateUdpClient()
    {
        var client = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        client.EnableBroadcast = true;
        return client;
    }

    private static IEnumerable<IPEndPoint> GetBroadcastEndpoints()
    {
        var endpoints = new HashSet<IPEndPoint>(new IPEndPointComparer())
        {
            new(IPAddress.Broadcast, LifxLanPort)
        };

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (var unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (unicastAddress.Address.AddressFamily != AddressFamily.InterNetwork ||
                    unicastAddress.IPv4Mask == null)
                {
                    continue;
                }

                var broadcastAddress = GetBroadcastAddress(unicastAddress.Address, unicastAddress.IPv4Mask);
                endpoints.Add(new IPEndPoint(broadcastAddress, LifxLanPort));
            }
        }

        return endpoints;
    }

    private static IPAddress GetBroadcastAddress(IPAddress address, IPAddress mask)
    {
        var addressBytes = address.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();
        var broadcastBytes = new byte[addressBytes.Length];

        for (var i = 0; i < addressBytes.Length; i++)
        {
            broadcastBytes[i] = (byte)(addressBytes[i] | ~maskBytes[i]);
        }

        return new IPAddress(broadcastBytes);
    }

    private void ParseDiscoveryPacket(byte[] buffer, IPEndPoint remoteEndPoint, IDictionary<string, LifxLanDeviceModel> devices)
    {
        if (buffer.Length < HeaderSize + 5)
        {
            return;
        }

        var messageType = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(32, 2));
        if (messageType != 3 || buffer[36] != 1)
        {
            return;
        }

        var port = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(37, 4)));
        var target = buffer.AsSpan(8, 6).ToArray();
        var serial = Convert.ToHexString(target).ToLowerInvariant();

        devices[serial] = new LifxLanDeviceModel(target, serial, remoteEndPoint.Address, port)
        {
            Label = serial
        };
    }

    private static void ParseDetailsPacket(byte[] buffer, IDictionary<string, LifxLanDeviceModel> devices)
    {
        if (buffer.Length < HeaderSize)
        {
            return;
        }

        var messageType = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(32, 2));
        if (messageType != 107 || buffer.Length < HeaderSize + 52)
        {
            return;
        }

        var serial = Convert.ToHexString(buffer.AsSpan(8, 6)).ToLowerInvariant();
        if (!devices.TryGetValue(serial, out var device))
        {
            return;
        }

        var payload = buffer.AsSpan(HeaderSize);
        device.BaseColor = LifxHsbk.FromPayload(payload[0..8]);
        device.IsPowered = BinaryPrimitives.ReadUInt16LittleEndian(payload[10..12]) > 0;
        device.Label = DecodeString(payload[12..44], serial);
    }

    private static void ParseZonePacket(byte[] buffer, IDictionary<string, LifxLanDeviceModel> devices)
    {
        if (buffer.Length < HeaderSize + 10)
        {
            return;
        }

        var serial = Convert.ToHexString(buffer.AsSpan(8, 6)).ToLowerInvariant();
        if (!devices.TryGetValue(serial, out var device))
        {
            return;
        }

        var messageType = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(32, 2));
        var payload = buffer.AsSpan(HeaderSize);

        switch (messageType)
        {
            case 503:
                UpsertZone(device, payload[0], payload[1], LifxHsbk.FromPayload(payload[2..10]));
                break;

            case 506:
                if (buffer.Length < HeaderSize + 66)
                {
                    return;
                }

                var zoneCount = payload[0];
                var zoneStart = payload[1];
                for (var i = 0; i < 8; i++)
                {
                    var zoneIndex = zoneStart + i;
                    if (zoneIndex >= zoneCount)
                    {
                        break;
                    }

                    var colorOffset = 2 + (i * 8);
                    UpsertZone(device, zoneCount, zoneIndex, LifxHsbk.FromPayload(payload.Slice(colorOffset, 8)));
                }
                break;
        }
    }

    private static void UpsertZone(LifxLanDeviceModel device, int zoneCount, int zoneIndex, LifxHsbk color)
    {
        device.ExpectedZoneCount = Math.Max(device.ExpectedZoneCount, Math.Max(1, zoneCount));

        var existingZone = device.Zones.FirstOrDefault(zone => zone.ZoneIndex == zoneIndex);
        if (existingZone == null)
        {
            device.Zones.Add(new LifxZoneModel(zoneIndex, color));
            return;
        }

        existingZone.CurrentColor = color;
    }

    private void EnsureDeviceZones(IEnumerable<LifxLanDeviceModel> devices)
    {
        foreach (var device in devices)
        {
            var expectedZoneCount = Math.Max(1, device.ExpectedZoneCount);
            for (var zoneIndex = 0; zoneIndex < expectedZoneCount; zoneIndex++)
            {
                if (device.Zones.Any(zone => zone.ZoneIndex == zoneIndex))
                {
                    continue;
                }

                var fallbackColor = zoneIndex == 0 ? device.BaseColor : LifxHsbk.Off(GetDeviceKelvin(device));
                device.Zones.Add(new LifxZoneModel(zoneIndex, fallbackColor));
            }

            device.Zones.Sort((left, right) => left.ZoneIndex.CompareTo(right.ZoneIndex));
        }
    }

    private void ApplyZoneAssignments(IEnumerable<LifxLanDeviceModel> devices)
    {
        var savedAssignments = GetSavedZoneAssignments();
        var defaultAssignments = LifxStageAssignments.CreateDefaultAssignments(
            savedAssignments.Values.SelectMany(zoneMap => zoneMap.Values));

        foreach (var device in devices
                     .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Serial, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var zone in device.Zones.OrderBy(item => item.ZoneIndex))
            {
                if (savedAssignments.TryGetValue(device.Serial, out var zoneAssignments) &&
                    zoneAssignments.TryGetValue(zone.ZoneIndex, out var assignment))
                {
                    zone.AssignedStageLight = assignment;
                }
                else if (defaultAssignments.Count > 0)
                {
                    zone.AssignedStageLight = defaultAssignments.Dequeue();
                }
                else
                {
                    zone.AssignedStageLight = LifxStageAssignments.Unassigned;
                }
            }
        }
    }
    
    private void SaveColors(IEnumerable<LifxLanDeviceModel> devices)
    {
        foreach (var device in devices)
        {
            foreach (var zone in device.Zones)
            {
                zone.OriginalColor = zone.CurrentColor;
            }
        }
    }

    private void RestoreColors(IEnumerable<LifxLanDeviceModel> devices)
    {
        var zoneList = new List<int>();
        foreach (var device in devices)
        {
            zoneList.Clear();
            foreach (var zone in device.Zones)
            {
                if (zone.AssignedStageLight != LifxStageAssignments.Unassigned)
                {
                    zone.CurrentColor = zone.OriginalColor;
                    zoneList.Add(zone.ZoneIndex);
                }
            }

            SendDeviceState(device, zoneList);
        }
    }

    private Dictionary<string, Dictionary<int, string>> GetSavedZoneAssignments()
    {
        var assignments = _mainViewModel?.GetLifxZoneAssignments() ?? SettingsManager.LifxZoneAssignments;
        var result = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var assignment in assignments)
        {
            if (string.IsNullOrWhiteSpace(assignment.Serial) || assignment.ZoneIndex < 0)
            {
                continue;
            }

            var serial = assignment.Serial.Trim().ToLowerInvariant();
            if (!result.TryGetValue(serial, out var zoneMap))
            {
                zoneMap = new Dictionary<int, string>();
                result[serial] = zoneMap;
            }

            zoneMap[assignment.ZoneIndex] = LifxStageAssignments.Normalize(assignment.StageLight);
        }

        return result;
    }

    private static string DecodeString(ReadOnlySpan<byte> bytes, string fallback)
    {
        var terminatorIndex = bytes.IndexOf((byte)0);
        var length = terminatorIndex >= 0 ? terminatorIndex : bytes.Length;

        if (length <= 0)
        {
            return fallback;
        }

        var value = Encoding.UTF8.GetString(bytes[..length]);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private List<LifxLanDeviceModel> SnapshotDevices()
    {
        lock (_deviceLock)
        {
            return _devices.ToList();
        }
    }

    private void UpdateViewModelDevices(IReadOnlyList<LifxLanDeviceModel> devices)
    {
        if (_mainViewModel == null)
        {
            return;
        }

        var visualDevices = devices
            .Select(device => new LifxDeviceViewModel(device))
            .ToList();

        _mainViewModel.SetLifxDevices(visualDevices);
    }

    private void UpdateViewModelStatus(string status, string message)
    {
        if (_mainViewModel == null)
        {
            return;
        }

        _mainViewModel.SetLifxStatus(status, message);
    }

    private byte NextSequence()
    {
        return unchecked((byte)Interlocked.Increment(ref _sequenceCounter));
    }

    private byte[] BuildGetColorZonesPacket(byte[] target)
    {
        Span<byte> payload = stackalloc byte[2];
        payload[0] = 0;
        payload[1] = byte.MaxValue;
        return BuildPacket(502, target, payload, tagged: false, ackRequired: false, resRequired: false);
    }

    private static LifxHsbk GetStageKitColor(StageKitTalker.CommandId commandId)
    {
        return commandId switch
        {
            StageKitTalker.CommandId.BlueLeds => StageKitBlue,
            StageKitTalker.CommandId.GreenLeds => StageKitGreen,
            StageKitTalker.CommandId.YellowLeds => StageKitYellow,
            StageKitTalker.CommandId.RedLeds => StageKitRed,
            _ => LifxHsbk.Off(DefaultKelvin)
        };
    }

    private byte[] BuildPacket(
        ushort messageType,
        byte[]? target,
        ReadOnlySpan<byte> payload,
        bool tagged,
        bool ackRequired,
        bool resRequired)
    {
        var packet = new byte[HeaderSize + payload.Length];
        var size = (ushort)packet.Length;

        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(0, 2), size);

        var frame = (ushort)(0x400 | 0x1000 | (tagged ? 0x2000 : 0));
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2, 2), frame);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4, 4), _sourceId);

        if (target != null)
        {
            target.AsSpan(0, Math.Min(6, target.Length)).CopyTo(packet.AsSpan(8, 6));
        }

        packet[22] = (byte)((resRequired ? 0x01 : 0x00) | (ackRequired ? 0x02 : 0x00));
        packet[23] = NextSequence();
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(32, 2), messageType);

        payload.CopyTo(packet.AsSpan(HeaderSize));
        return packet;
    }

    private static uint CreateSourceId()
    {
        return (uint)Random.Shared.Next(2, int.MaxValue);
    }

    private static ushort GetDeviceKelvin(LifxLanDeviceModel device)
    {
        return device.BaseColor.Kelvin > 0 ? device.BaseColor.Kelvin : DefaultKelvin;
    }

    private static ushort GetZoneKelvin(LifxZoneModel zone)
    {
        return zone.CurrentColor.Kelvin > 0 ? zone.CurrentColor.Kelvin : DefaultKelvin;
    }

    private static List<ZoneSegment> BuildChangedSegments(LifxLanDeviceModel device, IReadOnlyList<int> changedZoneIndices)
    {
        var changedZoneSet = new HashSet<int>(changedZoneIndices);
        var orderedZones = device.Zones
            .Where(zone => changedZoneSet.Contains(zone.ZoneIndex))
            .OrderBy(zone => zone.ZoneIndex)
            .ToList();

        var segments = new List<ZoneSegment>();
        if (orderedZones.Count == 0)
        {
            return segments;
        }

        var currentSegmentStart = orderedZones[0].ZoneIndex;
        var currentSegmentEnd = orderedZones[0].ZoneIndex;
        var currentColor = orderedZones[0].CurrentColor;

        for (var i = 1; i < orderedZones.Count; i++)
        {
            var zone = orderedZones[i];
            var isContiguous = zone.ZoneIndex == currentSegmentEnd + 1;
            var sameColor = zone.CurrentColor == currentColor;

            if (isContiguous && sameColor)
            {
                currentSegmentEnd = zone.ZoneIndex;
                continue;
            }

            segments.Add(new ZoneSegment(currentSegmentStart, currentSegmentEnd, currentColor));
            currentSegmentStart = zone.ZoneIndex;
            currentSegmentEnd = zone.ZoneIndex;
            currentColor = zone.CurrentColor;
        }

        segments.Add(new ZoneSegment(currentSegmentStart, currentSegmentEnd, currentColor));
        return segments;
    }
    
    private int CalculateStrobeSpeed(int speed)
    {
        var bpm = UdpIntake.BeatsPerMinute.Value;
        int noteValue = speed switch
        {
            1 => 16,  // Slow (16th note)
            2 => 24,  // Medium (24th note)
            3 => 32,  // Fast (32nd note)
            4 => 64,  // Fastest (64th note)
            _ => 16   // Default to slow
        };

        if (bpm <= 0) return 100;
        return (int)(60000.0 / bpm * 4 / noteValue);
    }

    public void Dispose()
    {
        UnsubscribeFromStageKit();
        _manualStrobeFlasher.Stop(SetManualStrobeStateAsync);
        RestoreColors(_devices);

        lock (_socketLock)
        {
            _commandClient?.Dispose();
            _commandClient = null;
        }
    }

    private sealed class IPEndPointComparer : IEqualityComparer<IPEndPoint>
    {
        public bool Equals(IPEndPoint? x, IPEndPoint? y)
        {
            if (x == null || y == null)
            {
                return x == y;
            }

            return x.Port == y.Port && x.Address.Equals(y.Address);
        }

        public int GetHashCode(IPEndPoint obj)
        {
            return HashCode.Combine(obj.Address, obj.Port);
        }
    }

    private readonly record struct ZoneSegment(int StartIndex, int EndIndex, LifxHsbk Color);

    private enum MultiZoneApply : byte
    {
        NoApply = 0,
        Apply = 1,
        ApplyOnly = 2
    }
}
