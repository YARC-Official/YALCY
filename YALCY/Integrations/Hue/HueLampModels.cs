using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace YALCY.Integrations.Hue;

/// <summary>
/// One channel of the "YARG" entertainment area, as discovered from the bridge.
/// </summary>
public sealed class HueLampModel
{
    public HueLampModel(int channelId, string name, double x, double y)
    {
        ChannelId = channelId;
        Name = name;
        X = x;
        Y = y;
    }

    public int ChannelId { get; }
    public string Name { get; }
    public double X { get; }
    public double Y { get; }
}

public sealed class HueLampAssignmentSetting
{
    public int ChannelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = HueLampRoles.Auto;
}

/// <summary>
/// What a lamp does:
/// Auto      – shares the 8 Stage Kit positions with all other "Auto" lamps (folded in channel order)
/// Light N   – shows exactly Stage Kit position N
/// Lights N+M – shows two opposite positions (handy with 4 pattern lamps)
/// Strobe    – only flashes on strobe cues, otherwise shows the idle colour/brightness
/// Off       – not used by YALCY
/// </summary>
public static class HueLampRoles
{
    public const string Auto = "Auto";
    public const string Strobe = "Strobe";
    public const string Off = "Off";

    private static readonly IReadOnlyList<string> AllOptionsInternal = BuildAllOptions();

    public static IReadOnlyList<string> AllOptions => AllOptionsInternal;

    public static string Normalize(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return Auto;
        }

        return AllOptionsInternal.FirstOrDefault(option =>
            string.Equals(option, role.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Auto;
    }

    /// <summary>
    /// Returns the Stage Kit position bitmask for fixed roles ("Light 3", "Lights 1+5").
    /// Returns false for Auto, Strobe and Off.
    /// </summary>
    public static bool TryGetFixedPositions(string? role, out byte positions)
    {
        positions = 0;
        var normalized = Normalize(role);

        for (var i = 0; i < 8; i++)
        {
            if (normalized == $"Light {i + 1}")
            {
                positions = (byte)(1 << i);
                return true;
            }
        }

        for (var i = 0; i < 4; i++)
        {
            if (normalized == $"Lights {i + 1}+{i + 5}")
            {
                positions = (byte)((1 << i) | (1 << (i + 4)));
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> BuildAllOptions()
    {
        var options = new List<string> { Auto };
        for (var i = 1; i <= 8; i++)
        {
            options.Add($"Light {i}");
        }

        for (var i = 1; i <= 4; i++)
        {
            options.Add($"Lights {i}+{i + 4}");
        }

        options.Add(Strobe);
        options.Add(Off);
        return options;
    }
}

public static class HueColorHelper
{
    private static readonly Regex HexPattern = new("^#?[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    public const string DefaultStrobeIdleColor = "FFFFFF";
    public const int DefaultStrobeIdleBrightness = 0;

    /// <summary>
    /// Returns a clean "RRGGBB" string, or null if the input isn't a valid 6-digit hex colour.
    /// </summary>
    public static string? NormalizeHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return HexPattern.IsMatch(trimmed) ? trimmed.TrimStart('#').ToUpperInvariant() : null;
    }
}
