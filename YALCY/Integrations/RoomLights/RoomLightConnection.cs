using System;
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

internal interface IRoomLightConnection : IDisposable
{
    Task CaptureAsync(CancellationToken token);
    Task SendAsync(RoomColor color, CancellationToken token);
    Task RestoreAsync(CancellationToken token);
}

// Govee replies always target local port 4002. One socket owns that port per integration.
internal sealed class GoveeSocket : IDisposable
{
    private readonly UdpClient _udp;
    private readonly SemaphoreSlim _query = new(1, 1);
    private readonly int _controlPort;
    public GoveeSocket(int replyPort = 4002, int controlPort = 4003)
    {
        _controlPort = controlPort;
        _udp = new UdpClient(AddressFamily.InterNetwork);
        try { _udp.Client.Bind(new IPEndPoint(IPAddress.Any, replyPort)); }
        catch { _udp.Dispose(); throw new IOException("Cannot listen on UDP 4002. Close other Govee LAN controllers and try again."); }
    }
    public async Task SendAsync(string address, JObject data, CancellationToken token) =>
        await _udp.SendAsync(Encoding.UTF8.GetBytes(data.ToString(Formatting.None)), new IPEndPoint(IPAddress.Parse(address), _controlPort), token);
    public async Task<JObject> StateAsync(string address, CancellationToken token)
    {
        await _query.WaitAsync(token);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(2000);
            await SendAsync(address, RoomLightProtocol.Govee("devStatus", new { }), timeout.Token);
            while (true)
            {
                var reply = await _udp.ReceiveAsync(timeout.Token);
                if (!reply.RemoteEndPoint.Address.Equals(IPAddress.Parse(address))) continue;
                try
                {
                    var json = JObject.Parse(Encoding.UTF8.GetString(reply.Buffer));
                    if ((string?)json["msg"]?["cmd"] == "devStatus" && json["msg"]?["data"] is JObject data && data["onOff"] != null)
                        return data;
                }
                catch (JsonException) { }
            }
        }
        finally { _query.Release(); }
    }
    public void Dispose() { _udp.Dispose(); _query.Dispose(); }
}

internal sealed class RoomLightConnection : IRoomLightConnection
{
    private readonly RoomLightKind _kind;
    private readonly RoomLightDevice _device;
    private readonly GoveeSocket? _govee;
    private readonly UdpClient? _wiz;
    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }) { Timeout = TimeSpan.FromSeconds(2) };
    private JObject? _original;
    private readonly int _httpPort;
    public RoomLightConnection(RoomLightKind kind, RoomLightDevice device, GoveeSocket? govee, int wizPort = 38899, int httpPort = 16021)
    {
        _kind = kind; _device = device; _govee = govee; _httpPort = httpPort;
        if (kind == RoomLightKind.Wiz) { _wiz = new UdpClient(); _wiz.Connect(device.Address, wizPort); }
    }
    private string BaseUrl => $"http://{_device.Address}:{_httpPort}/api/v1/{_device.Token}";
    public async Task CaptureAsync(CancellationToken token)
    {
        _original = _kind switch
        {
            RoomLightKind.Govee => await _govee!.StateAsync(_device.Address, token),
            RoomLightKind.Wiz => await WizRequestAsync(RoomLightProtocol.Wiz("getPilot", new { }), token),
            _ => await HttpAsync(HttpMethod.Get, "", null, token)
        };
        if (_kind == RoomLightKind.Wiz && _original["state"]?.Type != JTokenType.Boolean)
            throw new IOException("WiZ did not return a usable light state. Check LAN control and RGB compatibility.");
        if (_kind == RoomLightKind.Nanoleaf && _original["state"]?["on"]?["value"] == null)
            throw new IOException("Nanoleaf did not return a panel state. Use a compatible Panels OpenAPI controller.");
    }
    public async Task SendAsync(RoomColor color, CancellationToken token)
    {
        if (Lighting.LightingOutput.IsOutputSuppressed) color = RoomColor.Off;
        switch (_kind)
        {
            case RoomLightKind.Govee:
                foreach (var packet in RoomLightProtocol.GoveeColor(color))
                {
                    // Recheck between packets so blackout cannot be followed by a queued power-on.
                    if (Lighting.LightingOutput.IsOutputSuppressed && !color.IsOff)
                    { await _govee!.SendAsync(_device.Address, RoomLightProtocol.Govee("turn", new { value = 0 }), token); return; }
                    await _govee!.SendAsync(_device.Address, packet, token);
                }
                break;
            case RoomLightKind.Wiz: await WizRequestAsync(RoomLightProtocol.WizColor(color), token); break;
            default: await HttpAsync(HttpMethod.Put, "/state", RoomLightProtocol.NanoleafColor(color), token); break;
        }
    }
    public async Task RestoreAsync(CancellationToken token)
    {
        if (_original == null) { await SendAsync(RoomColor.Off, token); return; }
        switch (_kind)
        {
            case RoomLightKind.Govee:
                if (_original["color"] is JObject color)
                    await _govee!.SendAsync(_device.Address, RoomLightProtocol.Govee("colorwc", new { color,
                        colorTemInKelvin = (int?)_original["colorTemInKelvin"] ?? 0 }), token);
                if (_original["brightness"] != null)
                    await _govee!.SendAsync(_device.Address, RoomLightProtocol.Govee("brightness", new { value = (int)_original["brightness"]! }), token);
                await _govee!.SendAsync(_device.Address, RoomLightProtocol.Govee("turn", new { value = (int)_original["onOff"]! }), token);
                break;
            case RoomLightKind.Wiz: await WizRequestAsync(RoomLightProtocol.WizRestore(_original), token); break;
            default:
                var state = (JObject)_original["state"]!;
                await HttpAsync(HttpMethod.Put, "/state", RoomLightProtocol.NanoleafRestore(state), token);
                if ((string?)state["colorMode"] == "effect" && _original["effects"]?["select"] is JValue effect)
                {
                    await HttpAsync(HttpMethod.Put, "/effects", new JObject { ["select"] = effect.DeepClone() }, token);
                    await HttpAsync(HttpMethod.Put, "/state", new JObject { ["on"] = new JObject { ["value"] = state["on"]!["value"]!.DeepClone() } }, token);
                }
                break;
        }
    }
    private async Task<JObject> WizRequestAsync(JObject request, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(1500);
        await _wiz!.SendAsync(Encoding.UTF8.GetBytes(request.ToString(Formatting.None)), timeout.Token);
        while (true)
        {
            var reply = await _wiz.ReceiveAsync(timeout.Token);
            JObject json;
            try { json = JObject.Parse(Encoding.UTF8.GetString(reply.Buffer)); } catch (JsonException) { continue; }
            if ((string?)json["method"] != (string?)request["method"]) continue;
            if (json["error"] != null) throw new IOException("WiZ rejected the request. Check local control, firmware, and RGB support.");
            if (json["result"] is JObject result)
            {
                if ((bool?)result["success"] == false) throw new IOException("WiZ reported that the command failed.");
                return result;
            }
        }
    }
    private async Task<JObject> HttpAsync(HttpMethod method, string path, JObject? body, CancellationToken token)
    {
        using var request = new HttpRequestMessage(method, BaseUrl + path);
        if (body != null) request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, token);
        if (!response.IsSuccessStatusCode)
            throw new IOException($"Nanoleaf returned HTTP {(int)response.StatusCode}. Check pairing and Panels OpenAPI compatibility.");
        var text = await response.Content.ReadAsStringAsync(token);
        return string.IsNullOrWhiteSpace(text) ? new() : JObject.Parse(text);
    }
    public void Dispose() { _wiz?.Dispose(); _http.Dispose(); }
}
