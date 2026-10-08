using System;

namespace YALCY.Integrations.WLED;

/// <summary>
/// Supported network protocols for streaming real-time pixel data to WLED controllers.
/// </summary>
public enum WledProtocol
{
    /// <summary>
    /// Distributed Display Protocol (DDP) with 3 channels per pixel (RGB) on UDP port 4048.
    /// Standard protocol for WS2812B/WS2811/WS2815 strips (Most common / Recommended).
    /// </summary>
    DdpRgb = 0,

    /// <summary>
    /// Distributed Display Protocol (DDP) with 4 channels per pixel (RGBW) on UDP port 4048.
    /// For SK6812/WS2814 strips with dedicated physical white diodes.
    /// Features a 10-byte binary header with frame sequence tracking and push synchronization.
    /// </summary>
    DdpRgbw = 1,

    /// <summary>
    /// WLED Realtime UDP Direct RGB (DRGB) protocol on UDP port 21324.
    /// Raw binary protocol: Byte 0 = 2 (DRGB), Byte 1 = timeout seconds, followed by [R, G, B, ...].
    /// </summary>
    RealtimeDrgb = 2,

    /// <summary>
    /// WLED Realtime UDP Direct RGBW (DRGBW) protocol on UDP port 21324.
    /// Raw binary protocol: Byte 0 = 3 (DRGBW), Byte 1 = timeout seconds, followed by [R, G, B, W, ...].
    /// Extremely fast and simple to parse on ESP32 microcontrollers.
    /// </summary>
    RealtimeDrgbw = 3
}

/// <summary>
/// Hardware parameters auto-detected from WLED HTTP JSON API (/json/info).
/// </summary>
public sealed record WledDeviceInfo(string Name, int LedCount, bool IsRgbw, string Version);


/// <summary>
/// Defines how YALCY's 32 StageKit virtual lights (8 Blue, 8 Red, 8 Green, 8 Yellow)
/// are mapped across the physical LEDs of the WLED strip.
/// </summary>
public enum WledLayoutMode
{
    /// <summary>
    /// Tiled continuous repeat: The 32 StageKit LEDs are repeated in sequence (0..31, 0..31...)
    /// or in ping-pong mirror across the full length of the strip.
    /// </summary>
    TiledRepeat32 = 0,

    /// <summary>
    /// Proportional scaling: The 32 StageKit LEDs are stretched across the entire strip length,
    /// scaling each virtual area to cover multiple physical LEDs evenly.
    /// </summary>
    ProportionalStretch = 1,
    Mirror32 = 1,

    /// <summary>
    /// 4 distinct stage lighting pods along the strip:
    /// Quadrant 1 = Blue pod, Quadrant 2 = Red pod, Quadrant 3 = Green pod, Quadrant 4 = Yellow pod.
    /// Supports configurable physical gap LEDs and pod color order.
    /// </summary>
    FourQuadrantPods = 2,

    /// <summary>
    /// Symmetrical split from center outward:
    /// The stage lighting pods mirror outwards from the strip center towards both edges.
    /// Emulates concert stage truss lighting or drum riser illumination.
    /// </summary>
    CenterSplit = 3,

    /// <summary>
    /// Direct 1-to-1 index mapping:
    /// LED 0..31 match StageKit LEDs 0..31 directly. Extra LEDs on the strip can be blackout, repeated, or ambient.
    /// </summary>
    Direct1to1 = 4,

    /// <summary>
    /// Full stage color wash:
    /// All active StageKit colors are blended together and projected across the entire strip uniformly.
    /// </summary>
    FullWash = 5,

    /// <summary>
    /// Smooth gradient across strip:
    /// Interpolates smooth transitions from Pod 1 to 8 across the strip. Rotations flow like waves.
    /// </summary>
    SmoothGradient = 6
}

/// <summary>
/// Repeat behavior for the Tiled Repeat layout mode.
/// </summary>
public enum WledRepeatStyle
{
    ContinuousLoop = 0,
    PingPong = 1,
    InterleavedPodByPod = 2,
    InterleavedPingPong = 3,
    PairedInterleaved = 4,
    CrossCoolWarm = 5
}

/// <summary>
/// Interpolation mode for the Proportional Stretch layout mode.
/// </summary>
public enum WledStretchInterpolation
{
    PixelSharp = 0,
    SmoothBlend = 1
}

/// <summary>
/// Physical color arrangement of the 4 stage pods along the strip.
/// </summary>
public enum WledPodColorOrder
{
    BlueRedGreenYellow = 0,
    BlueGreenRedYellow = 1,
    RedBlueYellowGreen = 2,
    SymmetricalMirror = 3
}

/// <summary>
/// Direction for symmetrical center split mapping.
/// </summary>
public enum WledCenterSplitDirection
{
    InsideOut = 0,
    OutsideIn = 1
}

/// <summary>
/// Behavior for physical LEDs beyond index 31 in direct 1-to-1 mapping.
/// </summary>
public enum WledDirectExcessMode
{
    Blackout = 0,
    RepeatPattern = 1,
    AmbientWash = 2
}

/// <summary>
/// Color blending formula for full stage color wash mode.
/// </summary>
public enum WledWashBlendMode
{
    HarmonicAverage = 0,
    DominantColor = 1
}

/// <summary>
/// Dictates how the dedicated 4th (White) channel behaves across rhythm and strobe effects.
/// </summary>
public enum WledWhiteStrobeMode
{
    /// <summary>
    /// Full / Rhythm + Strobe (0): Dedicated White (W) diode is used for both live rhythm impulses
    /// (snare drum flashes, beat pulses, Star Power) AND concert strobe blinders.
    /// </summary>
    FullRhythmAndStrobe = 0,
    PureWhiteOnly = 0,

    /// <summary>
    /// Strobe Only (1): Dedicated White (W) diode is strictly reserved for strobe flashes / blinders.
    /// Drum hits, snare flashes, and beat pulses do NOT activate the White diode.
    /// </summary>
    StrobeOnly = 1,
    RgbPlusWhite = 1,

    /// <summary>
    /// Disabled / Nothing (2): Dedicated White (W) diode is completely turned off (W=0).
    /// All effects and strobes operate strictly in standard 3-channel RGB.
    /// </summary>
    Disabled = 2,
    RgbOnly = 2
}

/// <summary>
/// Represents a single pixel color with 4 independent 8-bit channels: Red, Green, Blue, and White.
/// </summary>
public readonly struct WledRgbwColor : IEquatable<WledRgbwColor>
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public byte W { get; }

    public WledRgbwColor(byte r, byte g, byte b, byte w = 0)
    {
        R = r;
        G = g;
        B = b;
        W = w;
    }

    // Common lighting presets
    public static readonly WledRgbwColor Black = new(0, 0, 0, 0);
    public static readonly WledRgbwColor PureWhite = new(0, 0, 0, 255);
    public static readonly WledRgbwColor MaxWhite = new(255, 255, 255, 255);
    public static readonly WledRgbwColor RgbWhite = new(255, 255, 255, 0);

    // StageKit standard 4-pod colors
    public static readonly WledRgbwColor StageKitBlue = new(0, 0, 255, 0);
    public static readonly WledRgbwColor StageKitRed = new(255, 0, 0, 0);
    public static readonly WledRgbwColor StageKitGreen = new(0, 255, 0, 0);
    public static readonly WledRgbwColor StageKitYellow = new(255, 255, 0, 0);

    /// <summary>
    /// Scales RGB channels by <paramref name="masterBrightness"/> (0.0 to 1.0)
    /// and White channel by <paramref name="whiteBrightness"/> (0.0 to 1.0).
    /// </summary>
    public WledRgbwColor ApplyBrightness(float masterBrightness, float whiteBrightness)
    {
        masterBrightness = Math.Clamp(masterBrightness, 0f, 1f);
        whiteBrightness = Math.Clamp(whiteBrightness, 0f, 1f);

        return new WledRgbwColor(
            (byte)Math.Round(R * masterBrightness),
            (byte)Math.Round(G * masterBrightness),
            (byte)Math.Round(B * masterBrightness),
            (byte)Math.Round(W * whiteBrightness)
        );
    }

    /// <summary>
    /// Linearly interpolates between two colors.
    /// </summary>
    public static WledRgbwColor Lerp(WledRgbwColor a, WledRgbwColor b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new WledRgbwColor(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t),
            (byte)Math.Round(a.W + (b.W - a.W) * t)
        );
    }

    /// <summary>
    /// Blends two colors by taking the maximum of each individual channel.
    /// </summary>
    public static WledRgbwColor BlendMax(WledRgbwColor a, WledRgbwColor b)
    {
        return new WledRgbwColor(
            Math.Max(a.R, b.R),
            Math.Max(a.G, b.G),
            Math.Max(a.B, b.B),
            Math.Max(a.W, b.W)
        );
    }

    /// <summary>
    /// Adds color channels together, clamping at 255.
    /// </summary>
    public static WledRgbwColor BlendAdd(WledRgbwColor a, WledRgbwColor b)
    {
        return new WledRgbwColor(
            (byte)Math.Min(255, a.R + b.R),
            (byte)Math.Min(255, a.G + b.G),
            (byte)Math.Min(255, a.B + b.B),
            (byte)Math.Min(255, a.W + b.W)
        );
    }

    /// <summary>
    /// Returns a new color with an overridden White (W) channel value.
    /// </summary>
    public WledRgbwColor WithWhite(byte whiteValue) => new(R, G, B, whiteValue);

    public bool Equals(WledRgbwColor other) => R == other.R && G == other.G && B == other.B && W == other.W;
    public override bool Equals(object? obj) => obj is WledRgbwColor other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(R, G, B, W);
    public static bool operator ==(WledRgbwColor left, WledRgbwColor right) => left.Equals(right);
    public static bool operator !=(WledRgbwColor left, WledRgbwColor right) => !left.Equals(right);

    public override string ToString() => $"[R={R}, G={G}, B={B}, W={W}]";
}

/// <summary>
/// Real-time transmission telemetry and performance statistics.
/// </summary>
public sealed class WledStatistics
{
    public long TotalPacketsSent { get; internal set; }
    public double PacketsPerSecond { get; internal set; }
    public DateTime? LastPacketTime { get; internal set; }
    public string LastError { get; internal set; } = string.Empty;
    public bool IsConnected { get; internal set; }
}
