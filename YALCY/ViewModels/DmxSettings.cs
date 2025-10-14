using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Avalonia;
using ReactiveUI;

namespace YALCY.ViewModels;

public interface IDmxChannelSetting
{
    string Label { get; set; }
    int[]? Channel { get; set; }
}

public class DmxSingleSetting : ReactiveObject
{
    private string _label;
    private int _value;

    [DataMember]
    public string Label
    {
        get => _label;
        set => this.RaiseAndSetIfChanged(ref _label, value);
    }

    [DataMember]
    public int Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }

    public DmxSingleSetting(string label, int value)
    {
        Label = label;
        _label = label;
        Value = value;
    }
}

public class DmxChannelSetting : ReactiveObject, IDmxChannelSetting
{
    private string _label;
    private int[]? _channel;

    [DataMember]
    public string Label
    {
        get => _label;
        set => this.RaiseAndSetIfChanged(ref _label, value);
    }

    [DataMember]
    public int[]? Channel
    {
        get => _channel;
        set => this.RaiseAndSetIfChanged(ref _channel, value);
    }

    public DmxChannelSetting(string label, params int[]? channels)
    {
        Label = label;
        _label = label;
        Channel = channels;
    }
}

public class DmxDimmerChannelSetting : ReactiveObject, IDmxChannelSetting
{
    private string _label;
    private int[]? _channel;

    [DataMember]
    public string Label
    {
        get => _label;
        set => this.RaiseAndSetIfChanged(ref _label, value);
    }

    [DataMember]
    public int[]? Channel
    {
        get => _channel;
        set
        {
            this.RaiseAndSetIfChanged(ref _channel, value);
            // Safely access the MainViewModel instance
            var app = (App)Application.Current!;
            //set previous channel value to 0
            if (app.MainViewModel != null)
            {

                foreach (var t in _channel)
                {
                    app.MainViewModel.DmxTalker?.SetChannelToValue(t, 0);

                }

                //set the new channel value to the previous value
                app.MainViewModel.DmxTalker?.UpdateMasterDimmers();
            }
        }
    }

    public DmxDimmerChannelSetting(string label, params int[]? channels)
    {
        Label = label;
        _label = label;
        Channel = channels;
    }
}

public class DmxDimmerValueSetting : ReactiveObject, IDmxChannelSetting
{
    private string _label;
    private int[]? _channel;

    [DataMember]
    public string Label
    {
        get => _label;
        set => this.RaiseAndSetIfChanged(ref _label, value);
    }

    [DataMember]
    public int[]? Channel
    {
        get => _channel;
        set
        {
            this.RaiseAndSetIfChanged(ref _channel, value);

            // Safely access the MainViewModel instance
            var app = (App)Application.Current!;
            if (app.MainViewModel != null)
            {
                app.MainViewModel.DmxTalker.UpdateMasterDimmers();
            }
        }
    }

    public DmxDimmerValueSetting(string label, params int[]? channels)
    {
        Label = label;
        _label = label;
        Channel = channels;
    }
}

public partial class MainWindowViewModel
{
    public ObservableCollection<DmxSingleSetting> AdvancedSettingsContainer { get; set; }
    public ObservableCollection<IDmxChannelSetting> EffectsChannelSettingsContainer { get; set; }
    public ObservableCollection<IDmxChannelSetting> MasterDimmerSettingsContainer { get; set; }
    public ObservableCollection<IDmxChannelSetting> ColorChannelSettingsContainer { get; set; }
    public ObservableCollection<DmxSingleSetting> InstrumentNoteSettingsContainer { get; set; }
    public ObservableCollection<DmxSingleSetting> BroadcastSettingsContainer { get; set; }

    public DmxDimmerChannelSetting MasterDimmerSettings = new("Master Dimmer Channels", 1, 8, 15, 22, 29, 36, 43, 50, 57, 64, 71, 78, 85, 92, 99, 106);

    public DmxDimmerValueSetting MasterDimmerValues =
        new("Master Dimmer Values", 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255);

    public DmxChannelSetting StrobeChannels = new("Strobe Channels", 7, 14, 21, 28, 35, 42, 49, 56);
    public DmxChannelSetting FogChannels = new("Fog Channels", 6, 13, 20, 27, 34, 41, 48, 55);
    public DmxChannelSetting RedChannels = new("Red Channels", 2, 9, 16, 23, 30, 37, 44, 51);
    public DmxChannelSetting BlueChannels = new("Blue Channels", 3, 10, 17, 24, 31, 38, 45, 52);
    public DmxChannelSetting YellowChannels = new("Yellow Channels", 4, 11, 18, 25, 32, 39, 46, 53);
    public DmxChannelSetting GreenChannels = new("Green Channels", 5, 12, 19, 26, 33, 40, 47, 54);
    public DmxSingleSetting BpmChannelSetting = new("BPM Channel", 56);
    public DmxSingleSetting CueChangeChannelSetting = new("Cue Change Channel", 57);
    public DmxSingleSetting PostProcessingChannelSetting = new("Post-Processing Channel", 62);
    public DmxSingleSetting KeyFrameChannelSetting = new("KeyFrame Channel", 60);
    public DmxSingleSetting BeatLineChannelSetting = new("BeatLine Channel", 58);
    public DmxSingleSetting BonusEffectChannelSetting = new("Bonus Effect Channel", 59);
    public DmxSingleSetting DrumNoteChannelSetting = new("Drum Note Channel", 61);
    public DmxSingleSetting GuitarNoteChannelSetting = new("Guitar Note Channel", 63);
    public DmxSingleSetting BassNoteChannelSetting = new("Bass Note Channel", 64);
    public DmxSingleSetting CurrentSpotlightSetting = new("Current Spotlight", 65);
    public DmxSingleSetting CurrentSingalongSetting = new("Current Singalong", 66);
    public DmxSingleSetting KeysNoteChannelSetting = new("Keys Note Channel", 67);
    public DmxSingleSetting VocalsNoteChannelSetting = new("Vocals Note Channel", 68);
    public DmxSingleSetting Harmony0NoteChannelSetting = new("Harmony 0 Note Channel", 69);
    public DmxSingleSetting Harmony1NoteChannelSetting = new("Harmony 1 Note Channel", 70);
    public DmxSingleSetting Harmony2NoteChannelSetting = new("Harmony 2 Note Channel", 71);
    public DmxSingleSetting BroadcastUniverseSetting = new("Broadcast Universe", 1);
    public DmxSingleSetting CurrentSceneSetting = new("Current Scene", 72);
    public DmxSingleSetting VenueSizeSetting = new("Venue Size", 73);
    public DmxSingleSetting PauseStateSetting = new("Pause State", 74);
    public DmxSingleSetting SongSectionSetting = new("Song Section", 75);

    private void FeedInDmxSettings()
    {
        BpmChannelSetting.Value = SettingsManager.BpmChannelSettingValue;
        CueChangeChannelSetting.Value = SettingsManager.CueChangeChannelSettingValue;
        PostProcessingChannelSetting.Value = SettingsManager.PostProcessingChannelSettingValue;
        KeyFrameChannelSetting.Value = SettingsManager.KeyFrameChannelSettingValue;
        BeatLineChannelSetting.Value = SettingsManager.BeatLineChannelSettingValue;
        BonusEffectChannelSetting.Value = SettingsManager.BonusEffectChannelSettingValue;
        DrumNoteChannelSetting.Value = SettingsManager.DrumNoteChannelSettingValue;
        GuitarNoteChannelSetting.Value = SettingsManager.GuitarNoteChannelSettingValue;
        BassNoteChannelSetting.Value = SettingsManager.BassNoteChannelSettingValue;
        KeysNoteChannelSetting.Value = SettingsManager.KeysNoteChannelSettingValue;
        VocalsNoteChannelSetting.Value = SettingsManager.VocalsNoteChannelSettingValue;
        Harmony0NoteChannelSetting.Value = SettingsManager.Harmony0NoteChannelSettingValue;
        Harmony1NoteChannelSetting.Value = SettingsManager.Harmony1NoteChannelSettingValue;
        Harmony2NoteChannelSetting.Value = SettingsManager.Harmony2NoteChannelSettingValue;
        CurrentSpotlightSetting.Value = SettingsManager.CurrentSpotlightSettingValue;
        CurrentSingalongSetting.Value = SettingsManager.CurrentSingalongSettingValue;
        CurrentSceneSetting.Value = SettingsManager.CurrentSceneSettingValue;
        VenueSizeSetting.Value = SettingsManager.VenueSizeSettingValue;
        PauseStateSetting.Value = SettingsManager.PauseStateSettingValue;
        SongSectionSetting.Value = SettingsManager.SongSectionSettingValue;

        MasterDimmerSettings.Channel = SettingsManager.MasterDimmerSettingsChannel;
        MasterDimmerValues.Channel = SettingsManager.MasterDimmerValuesChannel;
        StrobeChannels.Channel = SettingsManager.StrobeChannelsChannel;
        FogChannels.Channel = SettingsManager.FogChannelsChannel;
        RedChannels.Channel = SettingsManager.RedChannelsChannel;
        BlueChannels.Channel = SettingsManager.BlueChannelsChannel;
        YellowChannels.Channel = SettingsManager.YellowChannelsChannel;
        GreenChannels.Channel = SettingsManager.GreenChannelsChannel;

        BroadcastUniverseSetting.Value = SettingsManager.BroadcastUniverseSettingValue;
    }

    private void InitializeDmxCollections()
    {
        EffectsChannelSettingsContainer = new ObservableCollection<IDmxChannelSetting>
        {
            FogChannels,
            StrobeChannels,
        };

        MasterDimmerSettingsContainer = new ObservableCollection<IDmxChannelSetting>
        {
            MasterDimmerSettings,
            MasterDimmerValues,
        };

        ColorChannelSettingsContainer = new ObservableCollection<IDmxChannelSetting>
        {
            RedChannels,
            BlueChannels,
            YellowChannels,
            GreenChannels,
        };

        InstrumentNoteSettingsContainer = new ObservableCollection<DmxSingleSetting>
        {
            GuitarNoteChannelSetting,
            BassNoteChannelSetting,
            DrumNoteChannelSetting,
            KeysNoteChannelSetting,
            VocalsNoteChannelSetting,
            Harmony0NoteChannelSetting,
            Harmony1NoteChannelSetting,
            Harmony2NoteChannelSetting,
        };

        AdvancedSettingsContainer = new ObservableCollection<DmxSingleSetting>
        {
            BpmChannelSetting,
            CueChangeChannelSetting,
            BeatLineChannelSetting,
            BonusEffectChannelSetting,
            KeyFrameChannelSetting,
            PostProcessingChannelSetting,
            CurrentSingalongSetting,
            CurrentSpotlightSetting,
            CurrentSceneSetting,
            VenueSizeSetting,
            PauseStateSetting,
            SongSectionSetting,
        };

        BroadcastSettingsContainer = new ObservableCollection<DmxSingleSetting>
        {
            BroadcastUniverseSetting,
        };
    }
}
