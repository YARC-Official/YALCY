using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using YALCY.Integrations.StageKit;

namespace YALCY.Integrations.Lifx;

internal static class LifxStageAssignments
{
    public const string Unassigned = "Unassigned";

    private static readonly string[] ColorNames = { "Blue", "Green", "Yellow", "Red", "Strobe" };
    private static readonly IReadOnlyList<string> AllOptionsInternal = BuildAllOptions();

    public static IReadOnlyList<string> AllOptions => AllOptionsInternal;
    public static IReadOnlyList<string> AssignableOptions { get; } = AllOptionsInternal.Skip(1).ToArray();

    public static string Normalize(string? assignment)
    {
        if (string.IsNullOrWhiteSpace(assignment))
        {
            return Unassigned;
        }

        var canonical = AllOptionsInternal.FirstOrDefault(option =>
            string.Equals(option, assignment, StringComparison.OrdinalIgnoreCase));

        return canonical ?? Unassigned;
    }

    public static bool TryGetAssignmentLabel(StageKitTalker.CommandId commandId, int slotIndex, out string label)
    {
        if (!TryGetColorName(commandId, out var colorName) || slotIndex < 0 || slotIndex >= 8)
        {
            label = string.Empty;
            return false;
        }

        label = $"{colorName} {slotIndex + 1}";
        return true;
    }

    public static bool TryGetAssignmentSlot(string? assignment, StageKitTalker.CommandId commandId, out int slotIndex)
    {
        slotIndex = -1;
        if (!TryGetColorName(commandId, out var colorName))
        {
            return false;
        }

        for (var i = 0; i < 8; i++)
        {
            if (string.Equals(assignment, $"{colorName} {i + 1}", StringComparison.OrdinalIgnoreCase))
            {
                slotIndex = i;
                return true;
            }
        }

        return false;
    }

    public static Queue<string> CreateDefaultAssignments(IEnumerable<string> usedAssignments)
    {
        var used = new HashSet<string>(usedAssignments.Select(Normalize), StringComparer.OrdinalIgnoreCase);
        var available = AssignableOptions
            .Where(option => !used.Contains(option))
            .ToList();

        return new Queue<string>(available);
    }

    private static bool TryGetColorName(StageKitTalker.CommandId commandId, out string colorName)
    {
        colorName = commandId switch
        {
            StageKitTalker.CommandId.BlueLeds => "Blue",
            StageKitTalker.CommandId.GreenLeds => "Green",
            StageKitTalker.CommandId.YellowLeds => "Yellow",
            StageKitTalker.CommandId.RedLeds => "Red",
            StageKitTalker.CommandId.StrobeFastest => "Strobe",
            StageKitTalker.CommandId.StrobeFast => "Strobe",
            StageKitTalker.CommandId.StrobeMedium => "Strobe",
            StageKitTalker.CommandId.StrobeSlow => "Strobe",
            StageKitTalker.CommandId.StrobeOff => "Strobe",
            _ => string.Empty
        };

        return colorName.Length > 0;
    }

    private static IReadOnlyList<string> BuildAllOptions()
    {
        var options = new List<string> { Unassigned };
        foreach (var colorName in ColorNames)
        {
            if (colorName == "Strobe")
            {
                // Only one strobe option
                options.Add(colorName + " 1");
                continue;
            }
            
            for (var i = 1; i <= 8; i++)
            {
                options.Add($"{colorName} {i}");
            }
        }

        return options;
    }
}

internal sealed class LifxLanDeviceModel
{
    public LifxLanDeviceModel(byte[] target, string serial, IPAddress address, int port)
    {
        Target = target;
        Serial = serial;
        Address = address;
        Port = port;
        Zones = new List<LifxZoneModel>();
        BaseColor = LifxHsbk.Off(3500);
    }

    public byte[] Target { get; }
    public string Serial { get; }
    public IPAddress Address { get; }
    public int Port { get; }
    public string Label { get; set; } = string.Empty;
    public bool IsPowered { get; set; }
    public LifxHsbk BaseColor { get; set; }
    public int ExpectedZoneCount { get; set; } = 1;
    public List<LifxZoneModel> Zones { get; }
}

internal sealed class LifxZoneModel
{
    public LifxZoneModel(int zoneIndex, LifxHsbk currentColor)
    {
        ZoneIndex = zoneIndex;
        CurrentColor = currentColor;
        AssignedStageLight = LifxStageAssignments.Unassigned;
    }

    public int ZoneIndex { get; }
    public string AssignedStageLight { get; set; }
    public LifxHsbk CurrentColor { get; set; }
}

internal readonly record struct LifxHsbk(ushort Hue, ushort Saturation, ushort Brightness, ushort Kelvin)
{
    public bool IsOn => Brightness > 0;

    public static LifxHsbk Off(ushort kelvin)
    {
        return new LifxHsbk(0, 0, 0, kelvin);
    }

    public LifxHsbk WithBrightness(ushort brightness)
    {
        return this with { Brightness = brightness };
    }

    public static LifxHsbk FromPayload(ReadOnlySpan<byte> payload)
    {
        return new LifxHsbk(
            System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload[0..2]),
            System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload[2..4]),
            System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload[4..6]),
            System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload[6..8]));
    }
}
