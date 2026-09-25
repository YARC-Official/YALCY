using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;
using YALCY.Integrations.Hue;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
    private List<HueLampAssignmentSetting> _savedHueLampAssignments = new();
    private string _hueLampStatus = "Lamps: press \"Discover lamps\" to read the YARG entertainment area.";
    private string _hueStrobeIdleColor = HueColorHelper.DefaultStrobeIdleColor;
    private double _hueStrobeIdleBrightness = HueColorHelper.DefaultStrobeIdleBrightness;
    private IBrush _hueStrobeIdlePreview = Brushes.White;

    public ICommand DiscoverHueLampsCommand { get; set; } = null!;
    public ICommand TestHueStrobeCommand { get; set; } = null!;
    public ObservableCollection<HueLampViewModel> HueLamps { get; private set; } = new();

    public string HueLampStatus
    {
        get => _hueLampStatus;
        set => this.RaiseAndSetIfChanged(ref _hueLampStatus, value);
    }

    /// <summary>Colour of dedicated strobe lamps while no strobe is running (hex, e.g. FFFFFF).</summary>
    public string HueStrobeIdleColor
    {
        get => _hueStrobeIdleColor;
        set
        {
            this.RaiseAndSetIfChanged(ref _hueStrobeIdleColor, value);
            var normalized = HueColorHelper.NormalizeHex(value);
            if (normalized == null)
            {
                return;
            }

            HueStrobeIdlePreview = new SolidColorBrush(Color.Parse($"#{normalized}"));
            PushHueLampSettings();
        }
    }

    /// <summary>Brightness (0–100 %) of dedicated strobe lamps while no strobe is running.</summary>
    public double HueStrobeIdleBrightness
    {
        get => _hueStrobeIdleBrightness;
        set
        {
            var clamped = Math.Clamp(Math.Round(value), 0, 100);
            if (Math.Abs(_hueStrobeIdleBrightness - clamped) < 0.5)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _hueStrobeIdleBrightness, clamped);
            PushHueLampSettings();
        }
    }

    public IBrush HueStrobeIdlePreview
    {
        get => _hueStrobeIdlePreview;
        private set => this.RaiseAndSetIfChanged(ref _hueStrobeIdlePreview, value);
    }

    private void InitializeHueCollections()
    {
        DiscoverHueLampsCommand = ReactiveCommand.CreateFromTask(() => HueTalker.DiscoverLampsAsync(this));
        TestHueStrobeCommand = ReactiveCommand.CreateFromTask(() => HueTalker.TestStrobeAsync(this));

        _savedHueLampAssignments = SettingsManager.HueLampAssignments
            .Select(CloneHueAssignment)
            .ToList();

        _hueStrobeIdleColor = HueColorHelper.NormalizeHex(SettingsManager.HueStrobeIdleColor)
                              ?? HueColorHelper.DefaultStrobeIdleColor;
        _hueStrobeIdleBrightness = Math.Clamp(SettingsManager.HueStrobeIdleBrightness, 0, 100);
        _hueStrobeIdlePreview = new SolidColorBrush(Color.Parse($"#{_hueStrobeIdleColor}"));

        HueLamps = new ObservableCollection<HueLampViewModel>();
        var ordered = _savedHueLampAssignments.OrderBy(a => a.ChannelId).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var assignment = ordered[i];
            HueLamps.Add(new HueLampViewModel(
                new HueLampModel(assignment.ChannelId, assignment.Name, double.NaN, double.NaN),
                assignment.Role,
                OnHueLampAssignmentChanged,
                IsAlternateRow(i)));
        }

        if (HueLamps.Count > 0)
        {
            HueLampStatus = $"Lamps: {HueLamps.Count} saved. Press \"Discover lamps\" after changing the area in the Hue app.";
        }

        PushHueLampSettings();
    }

    public IReadOnlyList<HueLampAssignmentSetting> GetHueLampAssignments()
    {
        if (HueLamps.Count > 0)
        {
            return BuildHueAssignments(HueLamps);
        }

        return _savedHueLampAssignments.Select(CloneHueAssignment).ToList();
    }

    /// <summary>
    /// Called by HueTalker after discovery. Keeps existing roles for channels that still exist.
    /// </summary>
    public void SetHueLamps(IReadOnlyList<HueLampModel> lamps)
    {
        var currentRoles = GetHueLampAssignments()
            .GroupBy(a => a.ChannelId)
            .ToDictionary(g => g.Key, g => g.First().Role);

        var viewModels = lamps
            .OrderBy(l => l.ChannelId)
            .Select((lamp, index) =>
            {
                currentRoles.TryGetValue(lamp.ChannelId, out var role);
                return new HueLampViewModel(lamp, role, OnHueLampAssignmentChanged, IsAlternateRow(index));
            })
            .ToList();

        void Update()
        {
            HueLamps.Clear();
            foreach (var lamp in viewModels)
            {
                HueLamps.Add(lamp);
            }

            _savedHueLampAssignments = BuildHueAssignments(HueLamps);
            HueLampStatus = $"Lamps: found {HueLamps.Count} channel(s) in the YARG entertainment area.";
            PushHueLampSettings();
            SaveSettingsNow();
        }

        RunOnUiThread(Update);
    }

    public void SetHueLampStatus(string status)
    {
        RunOnUiThread(() => HueLampStatus = status);
    }

    private void OnHueLampAssignmentChanged()
    {
        _savedHueLampAssignments = BuildHueAssignments(HueLamps);
        PushHueLampSettings();
        SaveSettingsNow();
    }

    /// <summary>Writes Settings.json right away, so lamp roles survive a crash or a closed console.</summary>
    private void SaveSettingsNow()
    {
        try
        {
            SettingsManager.SaveSettings(this);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    /// <summary>Hands a thread-safe snapshot of the lamp roles and strobe idle state to the Hue output.</summary>
    private void PushHueLampSettings()
    {
        // HueTalker is created before the settings are fed in, but guard anyway.
        HueTalker?.ApplyLampSettings(
            GetHueLampAssignments(),
            HueColorHelper.NormalizeHex(_hueStrobeIdleColor) ?? HueColorHelper.DefaultStrobeIdleColor,
            _hueStrobeIdleBrightness / 100.0);
    }

    private static List<HueLampAssignmentSetting> BuildHueAssignments(IEnumerable<HueLampViewModel> lamps)
    {
        return lamps
            .Select(l => new HueLampAssignmentSetting
            {
                ChannelId = l.ChannelId,
                Name = l.Name,
                Role = HueLampRoles.Normalize(l.SelectedRole)
            })
            .ToList();
    }

    private static HueLampAssignmentSetting CloneHueAssignment(HueLampAssignmentSetting a)
    {
        return new HueLampAssignmentSetting
        {
            ChannelId = a.ChannelId,
            Name = a.Name ?? string.Empty,
            Role = HueLampRoles.Normalize(a.Role)
        };
    }

    private static void RunOnUiThread(Action action)
    {
        try
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                action();
            }
            else
            {
                Dispatcher.UIThread.Post(action);
            }
        }
        catch (InvalidOperationException)
        {
            action();
        }
    }
}

public sealed class HueLampViewModel : ReactiveObject
{
    private static readonly IBrush RowBackgroundBrush = new SolidColorBrush(Color.Parse("#10121A"));
    private static readonly IBrush AlternateRowBackgroundBrush = new SolidColorBrush(Color.Parse("#171B28"));

    private readonly Action? _onAssignmentChanged;
    private string _selectedRole;

    internal HueLampViewModel(HueLampModel lamp, string? role, Action? onAssignmentChanged, bool isAlternateRow)
    {
        ChannelId = lamp.ChannelId;
        Name = string.IsNullOrWhiteSpace(lamp.Name) ? $"Channel {lamp.ChannelId}" : lamp.Name;
        Position = double.IsNaN(lamp.X)
            ? "position unknown"
            : string.Create(CultureInfo.InvariantCulture, $"x {lamp.X:0.00}, y {lamp.Y:0.00}");
        DisplayName = $"#{lamp.ChannelId}  {Name}";
        RowBackground = isAlternateRow ? AlternateRowBackgroundBrush : RowBackgroundBrush;
        _selectedRole = HueLampRoles.Normalize(role);
        _onAssignmentChanged = onAssignmentChanged;
    }

    public int ChannelId { get; }
    public string Name { get; }
    public string Position { get; }
    public string DisplayName { get; }
    public IBrush RowBackground { get; }
    public IReadOnlyList<string> AvailableRoles => HueLampRoles.AllOptions;

    public string SelectedRole
    {
        get => _selectedRole;
        set
        {
            var normalized = HueLampRoles.Normalize(value);
            if (_selectedRole == normalized)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _selectedRole, normalized);
            _onAssignmentChanged?.Invoke();
        }
    }
}
