using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YALCY.Integrations.RoomLights;

internal static class RoomLightSetup
{
    public static async Task<IReadOnlyList<RoomLightDevice>> DiscoverAsync(RoomLightKind kind)
    {
        var found = new Dictionary<string, RoomLightDevice>();
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.EnableBroadcast = true;
        if (kind == RoomLightKind.Govee)
        {
            try { udp.Client.Bind(new IPEndPoint(IPAddress.Any, 4002)); }
            catch { throw new IOException("UDP port 4002 is busy. Close other Govee LAN controllers before scanning."); }
            udp.JoinMulticastGroup(IPAddress.Parse("239.255.255.250"));
        }
        else udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        var request = kind == RoomLightKind.Govee
            ? RoomLightProtocol.Govee("scan", new { account_topic = "reserve" })
            : RoomLightProtocol.Wiz("getPilot", new { });
        var endpoint = kind == RoomLightKind.Govee ? new IPEndPoint(IPAddress.Parse("239.255.255.250"), 4001)
            : new IPEndPoint(IPAddress.Broadcast, 38899);
        // Two bounded rounds tolerate a lost discovery request. Manual IP entry works across routed subnets.
        for (var round = 0; round < 2; round++)
        {
            using var timeout = new CancellationTokenSource(1500);
            await udp.SendAsync(Encoding.UTF8.GetBytes(request.ToString(Formatting.None)), endpoint, timeout.Token);
            try
            {
                while (true)
                {
                    var reply = await udp.ReceiveAsync(timeout.Token);
                    var device = ParseDiscovery(kind, reply.Buffer, reply.RemoteEndPoint.Address);
                    if (device != null) found[device.Address] = device;
                }
            }
            catch (OperationCanceledException) { }
        }
        return new List<RoomLightDevice>(found.Values);
    }
    internal static RoomLightDevice? ParseDiscovery(RoomLightKind kind, byte[] bytes, IPAddress source)
    {
        try
        {
            var json = JObject.Parse(Encoding.UTF8.GetString(bytes));
            if (kind == RoomLightKind.Govee && (string?)json["msg"]?["cmd"] == "scan" && json["msg"]?["data"] is JObject data)
                return new() { Name = $"Govee {(string?)data["sku"] ?? "light"}", Address = source.ToString() };
            if (kind == RoomLightKind.Wiz && (string?)json["method"] == "getPilot" && json["result"] is JObject result && result["state"] != null)
                return new() { Name = $"WiZ {(string?)result["mac"] ?? "light"}", Address = source.ToString() };
        }
        catch (JsonException) { }
        return null;
    }
    public static async Task<string> PairNanoleafAsync(string address)
    {
        // Reuse address validation without requiring an existing pairing token.
        var device = new RoomLightDevice { Address = address }.Snapshot(RoomLightKind.Wiz);
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) };
        using var response = await http.PostAsync($"http://{device.Address}:16021/api/v1/new", new StringContent(""));
        if (!response.IsSuccessStatusCode)
            throw new IOException($"Pairing returned HTTP {(int)response.StatusCode}. Hold the controller power button for 5–7 seconds until its indicator flashes, then try again.");
        var json = JObject.Parse(await response.Content.ReadAsStringAsync());
        var token = (string?)json["auth_token"];
        if (string.IsNullOrWhiteSpace(token)) throw new IOException("Nanoleaf did not return a pairing token.");
        return token;
    }
}
