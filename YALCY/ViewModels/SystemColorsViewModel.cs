using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using ReactiveUI;

namespace YALCY.ViewModels;

public class SystemColorItem
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Color Color { get; set; }
    public string HexCode => $"#{Color.A:X2}{Color.R:X2}{Color.G:X2}{Color.B:X2}";
    public string HexRgb => $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}";
    public string RgbString => $"rgb({Color.R}, {Color.G}, {Color.B})";
    public IBrush ColorBrush => new SolidColorBrush(Color);
    public IBrush TextContrastBrush => (Color.R * 0.299 + Color.G * 0.587 + Color.B * 0.114) > 140 
        ? new SolidColorBrush(Color.Parse("#121212")) 
        : new SolidColorBrush(Color.Parse("#FFFFFF"));
}

public class SystemColorsViewModel : ReactiveObject
{
    private string _searchFilter = string.Empty;
    private string _selectedCategory = "All";
    private readonly List<SystemColorItem> _allColors = new();

    public ObservableCollection<SystemColorItem> FilteredColors { get; } = new();

    public string SearchFilter
    {
        get => _searchFilter;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchFilter, value);
            ApplyFilter();
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedCategory, value);
            ApplyFilter();
        }
    }

    public ObservableCollection<string> Categories { get; } = new()
    {
        "All",
        "Accent Palette",
        "Surfaces & Windows",
        "Controls & 3D Elements",
        "Highlights & Selection",
        "Captions & Borders",
        "Menus & ScrollBars"
    };

    public SystemColorsViewModel()
    {
        if (Application.Current?.PlatformSettings != null)
        {
            Application.Current.PlatformSettings.ColorValuesChanged += (s, e) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(RefreshColors);
            };
        }

        if (Application.Current != null)
        {
            Application.Current.ActualThemeVariantChanged += (s, e) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(RefreshColors);
            };
        }

        LoadColors();
        ApplyFilter();
    }

    public void RefreshColors()
    {
        LoadColors();
        ApplyFilter();
    }

    private void LoadColors()
    {
        _allColors.Clear();

        // 1. Accent Palette (Windows 11 / Avalonia Fluent)
        var palette = AccentHelper.GetWindowsAccentPalette();
        AddPaletteColor(palette, "SystemAccentColor", "AccentColor", "Accent Palette", "Primary OS accent color configured by the user in Windows Settings.");
        AddPaletteColor(palette, "SystemAccentColorLight1", "AccentColorLight1", "Accent Palette", "Light tint (1) of the system accent color.");
        AddPaletteColor(palette, "SystemAccentColorLight2", "AccentColorLight2", "Accent Palette", "Lighter tint (2) of the system accent color.");
        AddPaletteColor(palette, "SystemAccentColorLight3", "AccentColorLight3", "Accent Palette", "Lightest tint (3) of the system accent color.");
        AddPaletteColor(palette, "SystemAccentColorDark1", "AccentColorDark1", "Accent Palette", "Dark shade (1) of the system accent color.");
        AddPaletteColor(palette, "SystemAccentColorDark2", "AccentColorDark2", "Accent Palette", "Darker shade (2) of the system accent color.");
        AddPaletteColor(palette, "SystemAccentColorDark3", "AccentColorDark3", "Accent Palette", "Darkest shade (3) of the system accent color.");

        // 2. Highlights & Selection
        AddSystemDrawingColor("Highlight", "HighlightColor", "Highlights & Selection", "Background color of selected items in lists, grids, and text.");
        AddSystemDrawingColor("HighlightText", "HighlightTextColor", "Highlights & Selection", "Color of text for selected items.");
        AddSystemDrawingColor("HotTrack", "HotTrackColor", "Highlights & Selection", "Color used to designate a hot-tracked / hovered item.");
        AddSystemDrawingColor("Info", "InfoColor", "Highlights & Selection", "Background color for ToolTips and informational banners.");
        AddSystemDrawingColor("InfoText", "InfoTextColor", "Highlights & Selection", "Text color for ToolTips and informational banners.");
        AddSystemDrawingColor("GrayText", "GrayTextColor", "Highlights & Selection", "Color of disabled or placeholder text.");

        // 3. Surfaces & Windows
        AddSystemDrawingColor("Window", "WindowColor", "Surfaces & Windows", "Background color in the client area of a window.");
        AddSystemDrawingColor("WindowText", "WindowTextColor", "Surfaces & Windows", "Color of text in the client area of a window.");
        AddSystemDrawingColor("WindowFrame", "WindowFrameColor", "Surfaces & Windows", "Color of a top-level window frame.");
        AddSystemDrawingColor("Desktop", "DesktopColor", "Surfaces & Windows", "Color of the desktop background.");
        AddSystemDrawingColor("AppWorkspace", "AppWorkspaceColor", "Surfaces & Windows", "Background color of the application MDI workspace.");

        // 4. Controls & 3D Elements
        AddSystemDrawingColor("Control", "ControlColor", "Controls & 3D Elements", "Face color of standard 3D interactive controls and buttons.");
        AddSystemDrawingColor("ControlText", "ControlTextColor", "Controls & 3D Elements", "Color of text in standard 3D controls.");
        AddSystemDrawingColor("ControlLight", "ControlLightColor", "Controls & 3D Elements", "Light highlight edge color of a 3D display element.");
        AddSystemDrawingColor("ControlLightLight", "ControlLightLightColor", "Controls & 3D Elements", "Brightest highlight color of a 3D display element.");
        AddSystemDrawingColor("ControlDark", "ControlDarkColor", "Controls & 3D Elements", "Shadow color of a 3D display element.");
        AddSystemDrawingColor("ControlDarkDark", "ControlDarkDarkColor", "Controls & 3D Elements", "Deep shadow / outline color of a 3D display element.");

        // 5. Captions & Titlebars
        AddSystemDrawingColor("ActiveCaption", "ActiveCaptionColor", "Captions & Borders", "Background color of the active window's title bar.");
        AddSystemDrawingColor("ActiveCaptionText", "ActiveCaptionTextColor", "Captions & Borders", "Color of text in the active window's title bar.");
        AddSystemDrawingColor("GradientActiveCaption", "GradientActiveCaptionColor", "Captions & Borders", "Right-side gradient color of active window title bar.");
        AddSystemDrawingColor("InactiveCaption", "InactiveCaptionColor", "Captions & Borders", "Background color of an inactive window's title bar.");
        AddSystemDrawingColor("InactiveCaptionText", "InactiveCaptionTextColor", "Captions & Borders", "Color of text in an inactive window's title bar.");
        AddSystemDrawingColor("GradientInactiveCaption", "GradientInactiveCaptionColor", "Captions & Borders", "Right-side gradient color of inactive window title bar.");
        AddSystemDrawingColor("ActiveBorder", "ActiveBorderColor", "Captions & Borders", "Color of an active window's border.");
        AddSystemDrawingColor("InactiveBorder", "InactiveBorderColor", "Captions & Borders", "Color of an inactive window's border.");

        // 6. Menus & ScrollBars
        AddSystemDrawingColor("Menu", "MenuColor", "Menus & ScrollBars", "Background color of a menu bar or context menu.");
        AddSystemDrawingColor("MenuText", "MenuTextColor", "Menus & ScrollBars", "Color of text inside a menu.");
        AddSystemDrawingColor("MenuBar", "MenuBarColor", "Menus & ScrollBars", "Background color of a top-level menu bar.");
        AddSystemDrawingColor("MenuHighlight", "MenuHighlightColor", "Menus & ScrollBars", "Background color used to highlight an active menu item.");
        AddSystemDrawingColor("ScrollBar", "ScrollBarColor", "Menus & ScrollBars", "Background color of a scrollbar track.");
    }

    private void AddPaletteColor(Dictionary<string, Color> palette, string paletteKey, string displayName, string category, string description)
    {
        Color color = Color.Parse("#6366F1");
        if (palette.TryGetValue(paletteKey, out var c))
        {
            color = c;
        }
        else if (Application.Current != null && Application.Current.TryGetResource(paletteKey, null, out var res))
        {
            if (res is Color rc) color = rc;
            else if (res is SolidColorBrush scb) color = scb.Color;
        }

        _allColors.Add(new SystemColorItem
        {
            Name = displayName,
            Category = category,
            Description = description,
            Color = color
        });
    }

    private void AddSystemDrawingColor(string sysDrawingName, string displayName, string category, string description)
    {
        Color color = Color.Parse("#888888");
        try
        {
            var prop = typeof(System.Drawing.SystemColors).GetProperty(sysDrawingName);
            if (prop != null)
            {
                var val = prop.GetValue(null);
                if (val is System.Drawing.Color sdc)
                {
                    color = Color.FromArgb(sdc.A, sdc.R, sdc.G, sdc.B);
                }
            }
        }
        catch { }

        _allColors.Add(new SystemColorItem
        {
            Name = displayName,
            Category = category,
            Description = description,
            Color = color
        });
    }

    public void ApplyFilter()
    {
        var query = _searchFilter.Trim().ToLowerInvariant();
        var category = _selectedCategory;

        var filtered = _allColors.Where(item =>
        {
            bool matchesCategory = category == "All" || item.Category == category;
            bool matchesSearch = string.IsNullOrEmpty(query) ||
                                 item.Name.ToLowerInvariant().Contains(query) ||
                                 item.Category.ToLowerInvariant().Contains(query) ||
                                 item.HexRgb.ToLowerInvariant().Contains(query) ||
                                 item.Description.ToLowerInvariant().Contains(query);
            return matchesCategory && matchesSearch;
        }).ToList();

        FilteredColors.Clear();
        foreach (var item in filtered)
        {
            FilteredColors.Add(item);
        }
    }
}
