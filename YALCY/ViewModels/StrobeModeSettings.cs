using ReactiveUI;

namespace YALCY.ViewModels;

public static class StrobeOutputModes
{
    public const int StrobeCommand = 0;
    public const int ManualFlash = 1;

    public static int Normalize(int value)
    {
        return value == ManualFlash ? ManualFlash : StrobeCommand;
    }
}

public partial class MainWindowViewModel
{
    private int _dmxStrobeMode;
    private int _serialStrobeMode;
    private int _rb3eStrobeMode;
    private int _hueStrobeMode;
    private int _lifxStrobeMode;
    private int _openRgbStrobeMode;
    private int _homeAssistantStrobeMode;

    public int DmxStrobeMode
    {
        get => _dmxStrobeMode;
        set => this.RaiseAndSetIfChanged(ref _dmxStrobeMode, StrobeOutputModes.Normalize(value));
    }

    public int SerialStrobeMode
    {
        get => _serialStrobeMode;
        set => this.RaiseAndSetIfChanged(ref _serialStrobeMode, StrobeOutputModes.Normalize(value));
    }

    public int Rb3eStrobeMode
    {
        get => _rb3eStrobeMode;
        set => this.RaiseAndSetIfChanged(ref _rb3eStrobeMode, StrobeOutputModes.Normalize(value));
    }

    public int HueStrobeMode
    {
        get => _hueStrobeMode;
        set => this.RaiseAndSetIfChanged(ref _hueStrobeMode, StrobeOutputModes.Normalize(value));
    }

    public int LifxStrobeMode
    {
        get => _lifxStrobeMode;
        set => this.RaiseAndSetIfChanged(ref _lifxStrobeMode, StrobeOutputModes.Normalize(value));
    }

    public int OpenRgbStrobeMode
    {
        get => _openRgbStrobeMode;
        set => this.RaiseAndSetIfChanged(ref _openRgbStrobeMode, StrobeOutputModes.Normalize(value));
    }

    public int HomeAssistantStrobeMode
    {
        get => _homeAssistantStrobeMode;
        set => this.RaiseAndSetIfChanged(ref _homeAssistantStrobeMode, StrobeOutputModes.Normalize(value));
    }

    private void FeedInStrobeModeSettings()
    {
        DmxStrobeMode = SettingsManager.DmxStrobeMode;
        SerialStrobeMode = SettingsManager.SerialStrobeMode;
        Rb3eStrobeMode = SettingsManager.Rb3eStrobeMode;
        HueStrobeMode = SettingsManager.HueStrobeMode;
        LifxStrobeMode = SettingsManager.LifxStrobeMode;
        OpenRgbStrobeMode = SettingsManager.OpenRgbStrobeMode;
        HomeAssistantStrobeMode = SettingsManager.HomeAssistantStrobeMode;
    }
}
