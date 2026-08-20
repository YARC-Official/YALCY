using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Microsoft.Win32;

namespace YALCY.ViewModels;

public static class AccentHelper
{
    public static Dictionary<string, Color> GetWindowsAccentPalette()
    {
        var palette = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);

        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
                if (key?.GetValue("AccentPalette") is byte[] bytes && bytes.Length >= 28)
                {
                    // Bytes are 8 RGBA tuples (4 bytes each):
                    // Index 0: Light3 (Lightest)
                    // Index 1: Light2
                    // Index 2: Light1
                    // Index 3: SystemAccentColor (Base)
                    // Index 4: Dark1
                    // Index 5: Dark2
                    // Index 6: Dark3
                    palette["SystemAccentColorLight3"] = Color.FromArgb(255, bytes[0], bytes[1], bytes[2]);
                    palette["SystemAccentColorLight2"] = Color.FromArgb(255, bytes[4], bytes[5], bytes[6]);
                    palette["SystemAccentColorLight1"] = Color.FromArgb(255, bytes[8], bytes[9], bytes[10]);
                    palette["SystemAccentColor"]       = Color.FromArgb(255, bytes[12], bytes[13], bytes[14]);
                    palette["SystemAccentColorDark1"]  = Color.FromArgb(255, bytes[16], bytes[17], bytes[18]);
                    palette["SystemAccentColorDark2"]  = Color.FromArgb(255, bytes[20], bytes[21], bytes[22]);
                    palette["SystemAccentColorDark3"]  = Color.FromArgb(255, bytes[24], bytes[25], bytes[26]);
                    return palette;
                }
            }
            catch { }

            try
            {
                using var dwmKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
                if (dwmKey?.GetValue("AccentColor") is int accentDword)
                {
                    // DWM AccentColor is ABGR format (0xAABBGGRR)
                    byte a = (byte)((accentDword >> 24) & 0xFF);
                    byte b = (byte)((accentDword >> 16) & 0xFF);
                    byte g = (byte)((accentDword >> 8) & 0xFF);
                    byte r = (byte)(accentDword & 0xFF);
                    var baseColor = Color.FromArgb(a == 0 ? (byte)255 : a, r, g, b);
                    GeneratePaletteFromBase(baseColor, palette);
                    return palette;
                }
            }
            catch { }
        }

        // Fallback to Avalonia PlatformSettings
        if (Application.Current?.PlatformSettings != null)
        {
            var colorValues = Application.Current.PlatformSettings.GetColorValues();
            if (colorValues.AccentColor1 is Color ac1)
            {
                GeneratePaletteFromBase(ac1, palette);
                return palette;
            }
        }

        // Fallback to Avalonia Application Resources
        if (Application.Current != null && Application.Current.TryGetResource("SystemAccentColor", null, out var res) && res is Color rc)
        {
            GeneratePaletteFromBase(rc, palette);
            return palette;
        }

        GeneratePaletteFromBase(Color.Parse("#107C41"), palette);
        return palette;
    }

    private static void GeneratePaletteFromBase(Color baseColor, Dictionary<string, Color> palette)
    {
        palette["SystemAccentColor"] = baseColor;
        palette["SystemAccentColorLight1"] = TintColor(baseColor, 0.25);
        palette["SystemAccentColorLight2"] = TintColor(baseColor, 0.50);
        palette["SystemAccentColorLight3"] = TintColor(baseColor, 0.75);
        palette["SystemAccentColorDark1"] = ShadeColor(baseColor, 0.20);
        palette["SystemAccentColorDark2"] = ShadeColor(baseColor, 0.40);
        palette["SystemAccentColorDark3"] = ShadeColor(baseColor, 0.60);
    }

    private static Color TintColor(Color c, double factor)
    {
        byte r = (byte)(c.R + (255 - c.R) * factor);
        byte g = (byte)(c.G + (255 - c.G) * factor);
        byte b = (byte)(c.B + (255 - c.B) * factor);
        return Color.FromArgb(c.A, r, g, b);
    }

    private static Color ShadeColor(Color c, double factor)
    {
        byte r = (byte)(c.R * (1 - factor));
        byte g = (byte)(c.G * (1 - factor));
        byte b = (byte)(c.B * (1 - factor));
        return Color.FromArgb(c.A, r, g, b);
    }

    public static Color GetAccentColor()
    {
        var palette = GetWindowsAccentPalette();
        if (palette.TryGetValue("SystemAccentColor", out var c))
        {
            return c;
        }
        return Color.Parse("#107C41");
    }

    public static Color GetContrastForegroundColor(Color backgroundColor)
    {
        double luminance = (0.299 * backgroundColor.R + 0.587 * backgroundColor.G + 0.114 * backgroundColor.B);
        return luminance > 140 ? Color.Parse("#000000") : Color.Parse("#FFFFFF");
    }

    public static IBrush GetAccentContrastForegroundBrush()
    {
        var accent = GetAccentColor();
        return new SolidColorBrush(GetContrastForegroundColor(accent));
    }

    public static void UpdateApplicationAccentColors(Application? app)
    {
        if (app == null) return;
        try
        {
            var palette = GetWindowsAccentPalette();
            if (!palette.TryGetValue("SystemAccentColor", out var baseAccent))
            {
                baseAccent = Color.Parse("#107C41");
            }

            var light1 = palette.GetValueOrDefault("SystemAccentColorLight1", TintColor(baseAccent, 0.25));
            var light2 = palette.GetValueOrDefault("SystemAccentColorLight2", TintColor(baseAccent, 0.50));
            var light3 = palette.GetValueOrDefault("SystemAccentColorLight3", TintColor(baseAccent, 0.75));
            var dark1 = palette.GetValueOrDefault("SystemAccentColorDark1", ShadeColor(baseAccent, 0.20));
            var dark2 = palette.GetValueOrDefault("SystemAccentColorDark2", ShadeColor(baseAccent, 0.40));
            var dark3 = palette.GetValueOrDefault("SystemAccentColorDark3", ShadeColor(baseAccent, 0.60));

            // Update accent resources
            app.Resources["SystemAccentColor"] = baseAccent;
            app.Resources["SystemAccentColorLight1"] = light1;
            app.Resources["SystemAccentColorLight2"] = light2;
            app.Resources["SystemAccentColorLight3"] = light3;
            app.Resources["SystemAccentColorDark1"] = dark1;
            app.Resources["SystemAccentColorDark2"] = dark2;
            app.Resources["SystemAccentColorDark3"] = dark3;

            app.Resources["SystemAccentContrastBrush"] = new SolidColorBrush(GetContrastForegroundColor(baseAccent));

            bool isDark = string.Equals(app.ActualThemeVariant?.ToString(), "Dark", StringComparison.OrdinalIgnoreCase);

            if (isDark)
            {
                app.Resources["AppAccentText"] = new SolidColorBrush(light2);
                app.Resources["AppAccentBadgeBackground"] = new SolidColorBrush(Color.FromArgb(50, baseAccent.R, baseAccent.G, baseAccent.B));
                app.Resources["AppAccentBadgeBorder"] = new SolidColorBrush(Color.FromArgb(120, baseAccent.R, baseAccent.G, baseAccent.B));
                app.Resources["AppAccentBadgeForeground"] = new SolidColorBrush(light2);
            }
            else
            {
                app.Resources["AppAccentText"] = new SolidColorBrush(dark1);
                app.Resources["AppAccentBadgeBackground"] = new SolidColorBrush(Color.FromArgb(30, baseAccent.R, baseAccent.G, baseAccent.B));
                app.Resources["AppAccentBadgeBorder"] = new SolidColorBrush(Color.FromArgb(80, baseAccent.R, baseAccent.G, baseAccent.B));
                app.Resources["AppAccentBadgeForeground"] = new SolidColorBrush(dark2);
            }
        }
        catch { }
    }
}
