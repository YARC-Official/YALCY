using System;

namespace YALCY.Integrations.StageKit;

public static class StageKitColorBlender
{
    /// <summary>
    /// Computes the blended RGB color for a single StageKit Pod given the active status of Blue, Red, Green, and Yellow LEDs.
    /// Returns the normalized (R, G, B) and a flag indicating if any LED was active.
    /// </summary>
    public static (byte R, byte G, byte B, bool IsActive) BlendPod(bool isB, bool isR, bool isG, bool isY)
    {
        if (!isB && !isR && !isG && !isY)
        {
            return (0, 0, 0, false);
        }

        int r = 0, g = 0, b = 0;
        if (isB) { b += 255; }
        if (isR) { r += 255; }
        if (isG) { g += 255; }
        if (isY) { r += 255; g += 255; } // Yellow has Red + Green

        int max = Math.Max(r, Math.Max(g, b));
        double scale = max > 0 ? 255.0 / max : 1.0;

        byte cr = (byte)Math.Clamp(r * scale, 0, 255);
        byte cg = (byte)Math.Clamp(g * scale, 0, 255);
        byte cb = (byte)Math.Clamp(b * scale, 0, 255);

        return (cr, cg, cb, true);
    }
}
