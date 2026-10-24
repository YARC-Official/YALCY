using System;
using System.Net;
using Newtonsoft.Json;
using ReactiveUI;
using YALCY.Integrations.WledSerial;

namespace YALCY.Integrations.GlowWorm;

[JsonObject(MemberSerialization.OptIn)]
public sealed class GlowWormOptions : ReactiveObject
{
    private string _address = "";
    private int _port = 4210, _httpPort = 80, _fps = 30;
    private bool _autoControl = true;
    [JsonProperty] public string Address { get => _address; set => this.RaiseAndSetIfChanged(ref _address, value ?? ""); }
    [JsonProperty] public int Port { get => _port; set => this.RaiseAndSetIfChanged(ref _port, value); }
    [JsonProperty] public int HttpPort { get => _httpPort; set => this.RaiseAndSetIfChanged(ref _httpPort, value); }
    [JsonProperty] public int Fps { get => _fps; set => this.RaiseAndSetIfChanged(ref _fps, value); }
    [JsonProperty] public bool AutoControl { get => _autoControl; set => this.RaiseAndSetIfChanged(ref _autoControl, value); }
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public WledSerialOptions Lighting { get; set; } = new() { Port = "Glow Worm" };

    public GlowWormOptions Snapshot()
    {
        if (!IPAddress.TryParse(Address.Trim(), out var ip) || IPAddress.Any.Equals(ip) || IPAddress.IPv6Any.Equals(ip) || IPAddress.Broadcast.Equals(ip) || ip.IsIPv6Multicast)
            throw new ArgumentException("Enter the Glow Worm controller's IP address (for example, 192.168.1.50).");
        if (Port is < 1 or > 65535 || HttpPort is < 1 or > 65535) throw new ArgumentException("UDP and HTTP ports must be between 1 and 65535.");
        if (Fps is < 1 or > 60) throw new ArgumentException("Frame rate must be between 1 and 60 FPS.");
        if (Lighting == null) throw new ArgumentException("LED mapping settings are missing.");
        return new() { Address = ip.ToString(), Port = Port, HttpPort = HttpPort, Fps = Fps,
            AutoControl = AutoControl, Lighting = Lighting.Snapshot() };
    }
}
