using System;
using System.Collections.Generic;
using System.Linq;

namespace YALCY.Integrations.AutoGen;

public static class AutoStrobeModes
{
    public const string Off = "Off";
    public const string GeneratedOnly = "Only on generated venues";
    public const string GeneratedOrNoChartStrobes = "Generated venues + songs without chart strobes";
    public const string Always = "Always (in addition to chart strobes)";

    public static IReadOnlyList<string> All { get; } = new[] { Off, GeneratedOnly, GeneratedOrNoChartStrobes, Always };

    public static string Normalize(string? value) =>
        All.FirstOrDefault(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase)) ?? GeneratedOnly;
}

public static class AutoStrobeSpeeds
{
    public const string ByBpm = "By BPM";
    public const string Slow = "Slow (16ths)";
    public const string Medium = "Medium (24ths)";
    public const string Fast = "Fast (32nds)";
    public const string Fastest = "Fastest (64ths)";

    public static IReadOnlyList<string> All { get; } = new[] { ByBpm, Slow, Medium, Fast, Fastest };

    public static string Normalize(string? value) =>
        All.FirstOrDefault(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase)) ?? ByBpm;
}

/// <summary>
/// Persisted settings for strobes that YALCY generates itself. Plain object so it serializes cleanly.
/// </summary>
public sealed class AutoStrobeSettingsData
{
    public string Mode { get; set; } = AutoStrobeModes.GeneratedOnly;
    public string Speed { get; set; } = AutoStrobeSpeeds.ByBpm;

    // Double bass: a run of fast kicks
    public bool DoubleBassEnabled { get; set; } = true;
    public int DoubleBassMinKicks { get; set; } = 6;
    public int DoubleBassMaxGapMs { get; set; } = 0; // 0 = automatic (16th notes at the song's BPM)
    public int DoubleBassMinBpm { get; set; } = 0;
    public int DoubleBassReleaseMs { get; set; } = 250;

    // Star power
    public bool StarPowerEnabled { get; set; } = true;
    public bool StarPowerWhileActive { get; set; } = false;
    public int StarPowerBurstMs { get; set; } = 2000;

    // Lighting cues
    public bool CueFrenzy { get; set; } = true;
    public bool CueBigRockEnding { get; set; } = true;
    public bool CueFlareFast { get; set; } = true;
    public bool CueFlareSlow { get; set; } = false;
    public bool CueStomp { get; set; } = false;
    public bool CueDischord { get; set; } = false;

    // BonusFX (pyro / lightning events of the venue track)
    public bool BonusFxEnabled { get; set; } = true;
    public int BonusFxBurstMs { get; set; } = 600;

    // Crash cymbal on a downbeat
    public bool CrashDownbeatEnabled { get; set; } = true;
    public int CrashFlashMs { get; set; } = 250;

    public AutoStrobeSettingsData Clone() => (AutoStrobeSettingsData)MemberwiseClone();

    public AutoStrobeSettingsData Normalized()
    {
        var copy = Clone();
        copy.Mode = AutoStrobeModes.Normalize(Mode);
        copy.Speed = AutoStrobeSpeeds.Normalize(Speed);
        copy.DoubleBassMinKicks = Math.Clamp(DoubleBassMinKicks, 2, 32);
        copy.DoubleBassMaxGapMs = Math.Clamp(DoubleBassMaxGapMs, 0, 1000);
        copy.DoubleBassMinBpm = Math.Clamp(DoubleBassMinBpm, 0, 400);
        copy.DoubleBassReleaseMs = Math.Clamp(DoubleBassReleaseMs, 0, 3000);
        copy.StarPowerBurstMs = Math.Clamp(StarPowerBurstMs, 100, 20000);
        copy.BonusFxBurstMs = Math.Clamp(BonusFxBurstMs, 50, 10000);
        copy.CrashFlashMs = Math.Clamp(CrashFlashMs, 50, 5000);
        return copy;
    }
}
