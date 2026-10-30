using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using ReactiveUI;

namespace YALCY.Integrations.RoomLights;

public enum RoomLightKind { Govee, Wiz, Nanoleaf }

[JsonObject(MemberSerialization.OptIn)]
public sealed class RoomLightOptions : ReactiveObject
{
    private int _brightness = 75, _updates = 5;
    [JsonProperty] public int Brightness { get => _brightness; set => this.RaiseAndSetIfChanged(ref _brightness, value); }
    [JsonProperty] public int UpdatesPerSecond { get => _updates; set => this.RaiseAndSetIfChanged(ref _updates, value); }
    [JsonProperty] public bool RestoreOnDisable { get; set; } = true;
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public ObservableCollection<RoomLightDevice> Devices { get; set; } = new();

    public RoomLightOptions Snapshot(RoomLightKind kind)
    {
        if (Brightness is < 0 or > 100 || UpdatesPerSecond is < 1 or > 10)
            throw new ArgumentException("Use brightness 0–100 and an update rate of 1–10 per second.");
        var devices = (Devices ?? new()).Where(d => d != null && d.Selected).Select(d => d.Snapshot(kind)).ToArray();
        if (devices.Length == 0) throw new ArgumentException("Add and select at least one light first.");
        if (devices.Length > 64) throw new ArgumentException("Select at most 64 devices.");
        if (devices.Select(d => d.Address).Distinct().Count() != devices.Length)
            throw new ArgumentException("Each IP address may appear only once among selected devices.");
        return new() { Brightness = Brightness, UpdatesPerSecond = UpdatesPerSecond,
            RestoreOnDisable = RestoreOnDisable, Devices = new(devices) };
    }
}

[JsonObject(MemberSerialization.OptIn)]
public sealed class RoomLightDevice : ReactiveObject
{
    private string _name = "New light", _address = "", _token = "", _assignment = "All colors";
    private bool _selected;
    private string _status = "Not connected";
    [JsonProperty] public string Name { get => _name; set => this.RaiseAndSetIfChanged(ref _name, value ?? ""); }
    [JsonProperty] public string Address { get => _address; set => this.RaiseAndSetIfChanged(ref _address, value ?? ""); }
    [JsonProperty] public string Token { get => _token; set => this.RaiseAndSetIfChanged(ref _token, value ?? ""); }
    [JsonProperty] public string Assignment { get => _assignment; set => this.RaiseAndSetIfChanged(ref _assignment, value ?? "All colors"); }
    [JsonProperty] public bool Selected { get => _selected; set => this.RaiseAndSetIfChanged(ref _selected, value); }
    public string Status { get => _status; set => this.RaiseAndSetIfChanged(ref _status, value); }
    public static string[] Assignments { get; } = new[] { "All colors", "Blue bank", "Red bank", "Green bank", "Yellow bank" }
        .Concat(Enumerable.Range(1, 8).Select(i => $"Light {i}")).ToArray();
    public string[] AvailableAssignments => Assignments;
    public RoomLightDevice Snapshot(RoomLightKind kind)
    {
        if (!IPAddress.TryParse(Address.Trim(), out var ip) || ip.AddressFamily != AddressFamily.InterNetwork ||
            ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.Broadcast) || ip.GetAddressBytes()[0] >= 224)
            throw new ArgumentException($"Enter a device IPv4 address for {Name}.");
        if (!Assignments.Contains(Assignment)) throw new ArgumentException($"Choose a cue assignment for {Name}.");
        if (kind == RoomLightKind.Nanoleaf && (string.IsNullOrWhiteSpace(Token) || !Token.All(char.IsAsciiLetterOrDigit)))
            throw new ArgumentException($"Pair {Name} with Nanoleaf first.");
        return new() { Name = Name, Address = ip.ToString(), Token = Token, Assignment = Assignment, Selected = Selected };
    }
}

internal readonly record struct RoomColor(byte R, byte G, byte B, int Brightness)
{
    public bool IsOff => Brightness == 0 || (R == 0 && G == 0 && B == 0);
    public static RoomColor Off => new(0, 0, 0, 0);
}
