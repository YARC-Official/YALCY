using System.Collections.Generic;

namespace YALCY.ViewModels.OpenRGB;

public class StageKitAreaOption
{
    public int AreaIndex { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#888888";

    public static List<StageKitAreaOption> AllOptions { get; } = CreateAllOptions();

    public static string GetColorHex(int areaIndex) => areaIndex switch
    {
        >= 0 and < 8 => "#1D4ED8",   // Bright Blue
        >= 8 and < 16 => "#B91C1C",  // Bright Red
        >= 16 and < 24 => "#15803D", // Bright Green
        >= 24 and < 32 => "#A16207", // Bright Yellow
        _ => "#18181B"                // Dark Key Face (Off)
    };

    public static string GetBorderHex(int areaIndex) => areaIndex switch
    {
        >= 0 and < 8 => "#60A5FA",
        >= 8 and < 16 => "#FCA5A5",
        >= 16 and < 24 => "#86EFAC",
        >= 24 and < 32 => "#FEF08A",
        _ => "#3F3F46"
    };

    public static string GetTextHex(int areaIndex) => areaIndex switch
    {
        >= 0 and < 8 => "#FFFFFF",
        >= 8 and < 16 => "#FFFFFF",
        >= 16 and < 24 => "#FFFFFF",
        >= 24 and < 32 => "#FFFFFF",
        _ => "#FFFFFF"
    };

    public static string GetDisplayName(int areaIndex) => areaIndex switch
    {
        >= 0 and < 8 => $"Blue {areaIndex + 1}",
        >= 8 and < 16 => $"Red {areaIndex - 7}",
        >= 16 and < 24 => $"Green {areaIndex - 15}",
        >= 24 and < 32 => $"Yellow {areaIndex - 23}",
        _ => "Off"
    };

    private static List<StageKitAreaOption> CreateAllOptions()
    {
        var options = new List<StageKitAreaOption>
        {
            new StageKitAreaOption { AreaIndex = -1, DisplayName = "⬛ Off", ColorHex = "#555555" }
        };

        for (int i = 0; i < 8; i++)
            options.Add(new StageKitAreaOption { AreaIndex = i, DisplayName = $"🔵 Blue {i + 1}", ColorHex = "#3B82F6" });

        for (int i = 0; i < 8; i++)
            options.Add(new StageKitAreaOption { AreaIndex = 8 + i, DisplayName = $"🔴 Red {i + 1}", ColorHex = "#EF4444" });

        for (int i = 0; i < 8; i++)
            options.Add(new StageKitAreaOption { AreaIndex = 16 + i, DisplayName = $"🟢 Green {i + 1}", ColorHex = "#10B981" });

        for (int i = 0; i < 8; i++)
            options.Add(new StageKitAreaOption { AreaIndex = 24 + i, DisplayName = $"🟡 Yellow {i + 1}", ColorHex = "#F59E0B" });

        return options;
    }
}
