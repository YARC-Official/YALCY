using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace YALCY.Integrations.RoomLights;

internal static class RoomLightProtocol
{
    public static JObject Govee(string command, object data) => JObject.FromObject(new { msg = new { cmd = command, data } });
    public static JObject Wiz(string method, object parameters) => JObject.FromObject(new { method, @params = parameters });
    public static IEnumerable<JObject> GoveeColor(RoomColor color)
    {
        if (color.IsOff) { yield return Govee("turn", new { value = 0 }); yield break; }
        yield return Govee("colorwc", new { color = new { r = (int)color.R, g = (int)color.G, b = (int)color.B }, colorTemInKelvin = 0 });
        yield return Govee("brightness", new { value = color.Brightness });
        yield return Govee("turn", new { value = 1 });
    }
    public static JObject WizColor(RoomColor color) => color.IsOff
        ? Wiz("setPilot", new { state = false })
        : Wiz("setPilot", new { state = true, r = (int)color.R, g = (int)color.G, b = (int)color.B,
            c = 0, w = 0, dimming = color.Brightness });

    public static JObject NanoleafColor(RoomColor color)
    {
        if (color.IsOff) return JObject.FromObject(new { on = new { value = false } });
        var r = color.R / 255.0; var g = color.G / 255.0; var b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b)); var min = Math.Min(r, Math.Min(g, b)); var delta = max - min;
        var hue = delta == 0 ? 0 : max == r ? 60 * ((g - b) / delta % 6) : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
        if (hue < 0) hue += 360;
        return JObject.FromObject(new { on = new { value = true }, hue = new { value = (int)Math.Round(hue) % 360 },
            sat = new { value = max == 0 ? 0 : (int)Math.Round(delta / max * 100) },
            brightness = new { value = (int)Math.Round(color.Brightness * max), duration = 0 } });
    }

    public static JObject WizRestore(JObject state)
    {
        var result = new JObject();
        foreach (var key in new[] { "state", "dimming" }) if (state[key] != null) result[key] = state[key]!.DeepClone();
        var scene = (int?)state["sceneId"] ?? 0;
        var keys = scene > 0 ? new[] { "sceneId", "speed" } : (int?)state["temp"] > 0
            ? new[] { "temp" } : new[] { "r", "g", "b", "c", "w" };
        foreach (var key in keys) if (state[key] != null) result[key] = state[key]!.DeepClone();
        return Wiz("setPilot", result);
    }

    public static JObject NanoleafRestore(JObject state)
    {
        var result = new JObject();
        var mode = (string?)state["colorMode"];
        var keys = mode == "ct" ? new[] { "on", "brightness", "ct" } : new[] { "on", "brightness", "hue", "sat" };
        foreach (var key in keys)
            if (state[key]?["value"] != null) result[key] = new JObject { ["value"] = state[key]!["value"]!.DeepClone() };
        return result;
    }
}
