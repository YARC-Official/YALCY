using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;

namespace YALCY.Integrations.GlowWorm;

internal static class GlowWormProtocol
{
    internal const int ChunkSize = 140;
    internal static IEnumerable<byte[]> Encode(byte[] rgb, byte brightness)
    {
        if (rgb.Length == 0 || rgb.Length % 3 != 0) throw new ArgumentException("Expected nonempty RGB triples.");
        var count = rgb.Length / 3;
        var chunks = (count + ChunkSize - 1) / ChunkSize;
        for (var chunk = 0; chunk < chunks; chunk++)
        {
            var text = new StringBuilder(FormattableString.Invariant($"DPsoftware,{count},{brightness},{chunks},{chunk}"));
            for (var i = chunk * ChunkSize; i < Math.Min(count, (chunk + 1) * ChunkSize); i++)
            {
                var p = i * 3;
                text.Append(',').Append(((rgb[p] << 16) | (rgb[p + 1] << 8) | rgb[p + 2]).ToString(CultureInfo.InvariantCulture));
            }
            yield return Encoding.ASCII.GetBytes(text.ToString());
        }
    }

    internal static Uri ControlUri(GlowWormOptions options, byte? streamBrightness = null)
    {
        var payload = JsonConvert.SerializeObject(new { state = "ON", effect = "Bias light", ffeffect = "Bias light",
            brightness = streamBrightness ?? 255 * options.Lighting.Brightness / 100, color = new { r = 0, g = 0, b = 0 }, allInstances = true });
        return new UriBuilder("http", options.Address, options.HttpPort, "set") { Query = "payload=" + Uri.EscapeDataString(payload) }.Uri;
    }
}
