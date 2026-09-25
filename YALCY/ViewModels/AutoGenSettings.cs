using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ReactiveUI;
using YALCY.Integrations.AutoGen;

namespace YALCY.ViewModels;

public partial class MainWindowViewModel
{
    private AutoStrobeSettingsData _autoStrobe = new();
    private string _autoStrobeStatus = "Auto strobe: waiting for data from YARG";

    public AutoStrobeGenerator AutoStrobeGenerator { get; private set; } = null!;

    public IReadOnlyList<string> AutoStrobeModeOptions => AutoStrobeModes.All;
    public IReadOnlyList<string> AutoStrobeSpeedOptions => AutoStrobeSpeeds.All;

    public string AutoStrobeStatus
    {
        get => _autoStrobeStatus;
        set => this.RaiseAndSetIfChanged(ref _autoStrobeStatus, value);
    }

    private void InitializeAutoGen()
    {
        _autoStrobe = SettingsManager.AutoStrobe.Normalized();
        AutoStrobeGenerator = new AutoStrobeGenerator(UdpIntake) { Settings = _autoStrobe.Clone() };
        AutoStrobeGenerator.StatusChanged += status => RunOnUiThread(() => AutoStrobeStatus = $"Auto strobe: {status}");
    }

    public AutoStrobeSettingsData GetAutoStrobeSettings() => _autoStrobe.Clone();

    private void UpdateAutoStrobe(Action<AutoStrobeSettingsData> change, [CallerMemberName] string? propertyName = null)
    {
        change(_autoStrobe);
        _autoStrobe = _autoStrobe.Normalized();
        this.RaisePropertyChanged(propertyName);
        if (AutoStrobeGenerator != null)
        {
            AutoStrobeGenerator.Settings = _autoStrobe.Clone();
        }
    }

    // General
    public string AutoStrobeMode
    {
        get => _autoStrobe.Mode;
        set
        {
            // ComboBoxes can push null while (re)loading their items; ignore that instead of resetting.
            if (value != null)
            {
                UpdateAutoStrobe(s => s.Mode = value);
            }
        }
    }

    public string AutoStrobeSpeed
    {
        get => _autoStrobe.Speed;
        set
        {
            if (value != null)
            {
                UpdateAutoStrobe(s => s.Speed = value);
            }
        }
    }

    // Double bass
    public bool AutoStrobeDoubleBass
    {
        get => _autoStrobe.DoubleBassEnabled;
        set => UpdateAutoStrobe(s => s.DoubleBassEnabled = value);
    }

    public decimal? AutoStrobeDoubleBassMinKicks
    {
        get => _autoStrobe.DoubleBassMinKicks;
        set => UpdateAutoStrobe(s => s.DoubleBassMinKicks = (int)(value ?? 6));
    }

    public decimal? AutoStrobeDoubleBassMaxGapMs
    {
        get => _autoStrobe.DoubleBassMaxGapMs;
        set => UpdateAutoStrobe(s => s.DoubleBassMaxGapMs = (int)(value ?? 0));
    }

    public decimal? AutoStrobeDoubleBassMinBpm
    {
        get => _autoStrobe.DoubleBassMinBpm;
        set => UpdateAutoStrobe(s => s.DoubleBassMinBpm = (int)(value ?? 0));
    }

    public decimal? AutoStrobeDoubleBassReleaseMs
    {
        get => _autoStrobe.DoubleBassReleaseMs;
        set => UpdateAutoStrobe(s => s.DoubleBassReleaseMs = (int)(value ?? 250));
    }

    // Star power
    public bool AutoStrobeStarPower
    {
        get => _autoStrobe.StarPowerEnabled;
        set => UpdateAutoStrobe(s => s.StarPowerEnabled = value);
    }

    public bool AutoStrobeStarPowerWhileActive
    {
        get => _autoStrobe.StarPowerWhileActive;
        set => UpdateAutoStrobe(s => s.StarPowerWhileActive = value);
    }

    public decimal? AutoStrobeStarPowerBurstMs
    {
        get => _autoStrobe.StarPowerBurstMs;
        set => UpdateAutoStrobe(s => s.StarPowerBurstMs = (int)(value ?? 2000));
    }

    // Cues
    public bool AutoStrobeCueFrenzy
    {
        get => _autoStrobe.CueFrenzy;
        set => UpdateAutoStrobe(s => s.CueFrenzy = value);
    }

    public bool AutoStrobeCueBigRockEnding
    {
        get => _autoStrobe.CueBigRockEnding;
        set => UpdateAutoStrobe(s => s.CueBigRockEnding = value);
    }

    public bool AutoStrobeCueFlareFast
    {
        get => _autoStrobe.CueFlareFast;
        set => UpdateAutoStrobe(s => s.CueFlareFast = value);
    }

    public bool AutoStrobeCueFlareSlow
    {
        get => _autoStrobe.CueFlareSlow;
        set => UpdateAutoStrobe(s => s.CueFlareSlow = value);
    }

    public bool AutoStrobeCueStomp
    {
        get => _autoStrobe.CueStomp;
        set => UpdateAutoStrobe(s => s.CueStomp = value);
    }

    public bool AutoStrobeCueDischord
    {
        get => _autoStrobe.CueDischord;
        set => UpdateAutoStrobe(s => s.CueDischord = value);
    }

    // BonusFX
    public bool AutoStrobeBonusFx
    {
        get => _autoStrobe.BonusFxEnabled;
        set => UpdateAutoStrobe(s => s.BonusFxEnabled = value);
    }

    public decimal? AutoStrobeBonusFxBurstMs
    {
        get => _autoStrobe.BonusFxBurstMs;
        set => UpdateAutoStrobe(s => s.BonusFxBurstMs = (int)(value ?? 600));
    }

    // Crash on downbeat
    public bool AutoStrobeCrashDownbeat
    {
        get => _autoStrobe.CrashDownbeatEnabled;
        set => UpdateAutoStrobe(s => s.CrashDownbeatEnabled = value);
    }

    public decimal? AutoStrobeCrashFlashMs
    {
        get => _autoStrobe.CrashFlashMs;
        set => UpdateAutoStrobe(s => s.CrashFlashMs = (int)(value ?? 250));
    }
}
