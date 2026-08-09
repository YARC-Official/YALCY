using System;
using System.Collections.Generic;
using System.Linq;

namespace YALCY.Integrations.HomeAssistant;

public sealed class HomeAssistantLightModel
{
    public HomeAssistantLightModel(string entityId, string friendlyName, string state)
    {
        EntityId = entityId;
        FriendlyName = friendlyName;
        State = state;
    }

    public string EntityId { get; }
    public string FriendlyName { get; }
    public string State { get; }
}

public sealed class HomeAssistantAssignmentSetting
{
    public string EntityId { get; set; } = string.Empty;
    public string StageLight { get; set; } = HomeAssistantStageAssignments.Unassigned;
}

public static class HomeAssistantStageAssignments
{
    public const string Unassigned = "Unassigned";
    public const string Strobe = "Strobe";

    private static readonly IReadOnlyList<string> AllOptionsInternal = BuildAllOptions();

    public static IReadOnlyList<string> AllOptions => AllOptionsInternal;

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

    public static bool TryGetSlot(string? assignment, out int slotIndex)
    {
        slotIndex = -1;
        var normalized = Normalize(assignment);

        for (var i = 0; i < 8; i++)
        {
            if (string.Equals(normalized, $"Light {i + 1}", StringComparison.OrdinalIgnoreCase))
            {
                slotIndex = i;
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> BuildAllOptions()
    {
        var options = new List<string> { Unassigned };
        for (var i = 1; i <= 8; i++)
        {
            options.Add($"Light {i}");
        }

        options.Add(Strobe);
        return options;
    }
}
