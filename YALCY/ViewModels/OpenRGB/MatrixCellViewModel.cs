using System;
using ReactiveUI;

namespace YALCY.ViewModels.OpenRGB;

public class MatrixCellViewModel : ReactiveObject
{
    public uint Row { get; }
    public uint Column { get; }
    public int LedIndex { get; }
    public string RawName { get; }
    public string DisplayLabel { get; }
    public bool HasKey { get; }
    public int ColumnSpan { get; }
    public int RowSpan { get; }

    public double KeyWidth => (ColumnSpan * 32.0) + ((ColumnSpan - 1) * 2.0);
    public double KeyHeight => (RowSpan * 32.0) + ((RowSpan - 1) * 2.0);

    private int _areaIndex = -1;
    public int AreaIndex
    {
        get => _areaIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _areaIndex, value);
            this.RaisePropertyChanged(nameof(AreaColorHex));
            this.RaisePropertyChanged(nameof(AreaBorderHex));
            this.RaisePropertyChanged(nameof(AreaTextHex));
            this.RaisePropertyChanged(nameof(AreaName));
            this.RaisePropertyChanged(nameof(ChannelBadge));
            this.RaisePropertyChanged(nameof(FullToolTip));
            OnCellChanged?.Invoke();
        }
    }

    public string AreaColorHex => StageKitAreaOption.GetColorHex(_areaIndex);
    public string AreaBorderHex => StageKitAreaOption.GetBorderHex(_areaIndex);
    public string AreaTextHex => StageKitAreaOption.GetTextHex(_areaIndex);
    public string AreaName => StageKitAreaOption.GetDisplayName(_areaIndex);

    public string FullToolTip
    {
        get
        {
            if (!HasKey) return string.Empty;
            string keyInfo = !string.IsNullOrWhiteSpace(RawName) ? RawName : $"Key #{LedIndex + 1}";
            return $"{keyInfo} (LED #{LedIndex}) - {AreaName} ({ChannelBadge})";
        }
    }

    public string ChannelBadge => _areaIndex switch
    {
        >= 0 and < 8 => $"B{_areaIndex + 1}",
        >= 8 and < 16 => $"R{_areaIndex - 7}",
        >= 16 and < 24 => $"G{_areaIndex - 15}",
        >= 24 and < 32 => $"Y{_areaIndex - 23}",
        _ => "OFF"
    };

    public Action? OnCellChanged { get; set; }

    public MatrixCellViewModel(uint row, uint col, int ledIndex, string rawName, int initialArea, int colSpan = 1, int rowSpan = 1, Action? onCellChanged = null)
    {
        Row = row;
        Column = col;
        LedIndex = ledIndex;
        RawName = rawName;
        HasKey = ledIndex >= 0;
        ColumnSpan = colSpan;
        RowSpan = rowSpan;
        DisplayLabel = CleanKeyLabel(rawName, ledIndex);
        _areaIndex = initialArea;
        OnCellChanged = onCellChanged;
    }

    private static string CleanKeyLabel(string rawName, int ledIndex)
    {
        if (ledIndex < 0) return string.Empty;
        if (string.IsNullOrWhiteSpace(rawName)) return $"#{ledIndex + 1}";

        string name = rawName.Trim();
        if (name.StartsWith("Key: ", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(5).Trim();
        }

        if (name.Contains("Keypad", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Numpad", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Number Pad", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Num Pad", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Num ", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("Enter", StringComparison.OrdinalIgnoreCase) || name.Contains("Return", StringComparison.OrdinalIgnoreCase)) return "↵";
            if (name.Contains("Plus", StringComparison.OrdinalIgnoreCase) || name.EndsWith("+")) return "+";
            if (name.Contains("Minus", StringComparison.OrdinalIgnoreCase) || name.EndsWith("-")) return "-";
            if (name.Contains("Asterisk", StringComparison.OrdinalIgnoreCase) || name.Contains("Star", StringComparison.OrdinalIgnoreCase) || name.EndsWith("*")) return "*";
            if (name.Contains("Slash", StringComparison.OrdinalIgnoreCase) || name.EndsWith("/")) return "/";
            if (name.Contains("Period", StringComparison.OrdinalIgnoreCase) || name.Contains("Decimal", StringComparison.OrdinalIgnoreCase) || name.Contains("Dot", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".")) return ".";
            if (name.Contains("Lock", StringComparison.OrdinalIgnoreCase)) return "Num";

            for (int d = 0; d <= 9; d++)
            {
                if (name.EndsWith(d.ToString())) return d.ToString();
            }
        }

        return name switch
        {
            "Escape" => "Esc",
            "Left Control" or "Right Control" or "Control" => "Ctl",
            "Left Shift" or "Right Shift" or "Shift" => "Shift",
            "Left Alt" or "Right Alt" or "Alt" => "Alt",
            "Left Windows" or "Right Windows" or "Windows" => "❖",
            "Caps Lock" => "Cap",
            "Backspace" => "⌫",
            "Return" or "Enter" => "↵",
            "Print Screen" => "Prt",
            "Scroll Lock" => "Scr",
            "Pause / Break" or "Pause" or "Break" => "Brk",
            "Insert" => "Ins",
            "Delete" => "Del",
            "Page Up" => "PUp",
            "Page Down" => "PDn",
            "Up Arrow" => "↑",
            "Down Arrow" => "↓",
            "Left Arrow" => "←",
            "Right Arrow" => "→",
            "Num Lock" => "Num",
            "Space" or "Spacebar" => "Spc",
            "Menu" or "App" or "Application" => "≡",
            string s when s.Contains("Play", StringComparison.OrdinalIgnoreCase) || s.Contains("Pause", StringComparison.OrdinalIgnoreCase) => "⏯",
            string s when s.Contains("Next", StringComparison.OrdinalIgnoreCase) => "⏭",
            string s when s.Contains("Prev", StringComparison.OrdinalIgnoreCase) => "⏮",
            string s when s.Contains("Stop", StringComparison.OrdinalIgnoreCase) => "⏹",
            string s when s.Contains("Mute", StringComparison.OrdinalIgnoreCase) => "🔇",
            string s when s.Contains("Vol", StringComparison.OrdinalIgnoreCase) && s.Contains("Up", StringComparison.OrdinalIgnoreCase) => "🔊",
            string s when s.Contains("Vol", StringComparison.OrdinalIgnoreCase) && s.Contains("Down", StringComparison.OrdinalIgnoreCase) => "🔉",
            string s when s.Contains("Media", StringComparison.OrdinalIgnoreCase) => "🎵",
            string s when s.Contains("Light", StringComparison.OrdinalIgnoreCase) => "RGB",
            string s when s.Contains("Game", StringComparison.OrdinalIgnoreCase) => "Game",
            string s when s.Contains("Logo", StringComparison.OrdinalIgnoreCase) => "Logo",
            string s when s.Contains("Macro", StringComparison.OrdinalIgnoreCase) => "Mac",
            _ => name.Length > 5 ? name.Substring(0, 4) : name
        };
    }
}
