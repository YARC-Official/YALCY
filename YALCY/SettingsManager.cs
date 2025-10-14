using System;
using System.Collections.Generic;
using System.IO;
using HueApi.Models.Clip;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YALCY.ViewModels;

namespace YALCY;

public class SettingsContainer
{
    public List<EnableSetting> CurrentEnableSettings { get; set; }
    public List<DmxSingleSetting> CurrentSingleSettings { get; set; }
    public List<DmxChannelSetting> CurrentChannelSettings { get; set; }
    public DmxDimmerChannelSetting CurrentMasterDimmerChannelSettings { get; set; }
    public DmxDimmerValueSetting CurrentMasterDimmerValueChannelSettings { get; set; }
    public RegisterEntertainmentResult HueAuthResult { get; set; }
    public string? HueBridgeIP { get; set; }
    public ushort UdpListenPort { get; set; }
    public ushort OpenRgbServerPort { get; set; }
    public string? OpenRgbServerIp { get; set; }
}

internal static class SettingsManager
{
    private static readonly string SettingsDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YALCY", "Settings");

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "Settings.json");

    private static SettingsContainer settings = new()
    {
        CurrentEnableSettings = new List<EnableSetting>(),
        CurrentChannelSettings = new List<DmxChannelSetting>(),
        CurrentSingleSettings = new List<DmxSingleSetting>(),
        CurrentMasterDimmerChannelSettings =
            new DmxDimmerChannelSetting("Master Dimmer Channels", 1, 8, 15, 22, 29, 36, 43, 50),
        CurrentMasterDimmerValueChannelSettings =
            new DmxDimmerValueSetting("Master Dimmer Values", 255, 255, 255, 255, 255, 255, 255, 255),
        HueAuthResult = new RegisterEntertainmentResult(),
        HueBridgeIP = "",
        UdpListenPort = 0,
        OpenRgbServerPort = 0,
        OpenRgbServerIp = "",
    };

    public static bool UdpEnableSettingIsEnabled { get; set; }
    public static bool DmxEnabledSettingIsEnabled { get; set; }
    public static bool HueEnabledSettingIsEnabled { get; set; }
    public static bool StageKitEnabledSettingIsEnabled { get; set; }
    public static bool Rb3eEnabledSettingIsEnabled { get; set; }
    public static bool SerialEnabledSettingIsEnabled { get; set; }
    public static bool OpenRgbEnabledSettingIsEnabled { get; set; }
    public static int BpmChannelSettingValue { get; private set; }
    public static int CueChangeChannelSettingValue { get; private set; }
    public static int PostProcessingChannelSettingValue { get; private set; }
    public static int KeyFrameChannelSettingValue { get; private set; }
    public static int BeatLineChannelSettingValue { get; private set; }
    public static int BonusEffectChannelSettingValue { get; private set; }
    public static int DrumNoteChannelSettingValue { get; private set; }
    public static int GuitarNoteChannelSettingValue { get; private set; }
    public static int BassNoteChannelSettingValue { get; private set; }
    public static int KeysNoteChannelSettingValue { get; private set; }
    public static int VocalsNoteChannelSettingValue { get; private set; }
    public static int Harmony0NoteChannelSettingValue { get; private set; }
    public static int Harmony1NoteChannelSettingValue { get; private set; }
    public static int Harmony2NoteChannelSettingValue { get; private set; }
    public static int CurrentSceneSettingValue { get; private set; }
    public static int VenueSizeSettingValue { get; private set; }
    public static int PauseStateSettingValue { get; private set; }
    public static int SongSectionSettingValue { get; private set; }
    public static int CurrentSpotlightSettingValue { get; private set; }
    public static int CurrentSingalongSettingValue { get; private set; }
    public static int BroadcastUniverseSettingValue { get; private set; }
    public static int[]? MasterDimmerSettingsChannel { get; private set; }
    public static int[]? MasterDimmerValuesChannel { get; private set; }
    public static int[]? StrobeChannelsChannel { get; private set; }
    public static int[]? FogChannelsChannel { get; private set; }
    public static int[]? RedChannelsChannel { get; private set; }
    public static int[]? BlueChannelsChannel { get; private set; }
    public static int[]? YellowChannelsChannel { get; private set; }
    public static int[]? GreenChannelsChannel { get; private set; }
    public static string? HueAuthResultUsername { get; private set; }
    public static string? HueAuthResultStreamingClientKey { get; private set; }
    public static string? HueAuthResultIp { get; private set; }
    public static string? HueBridgeIp { get; private set; }
    public static ushort UdpListenPort { get; set; }
    public static ushort OpenRgbServerPort { get; set; }
    public static string? OpenRgbServerIp { get; set; }

    public static void SaveSettings(MainWindowViewModel mainViewModel)
    {
        if (!Directory.Exists(SettingsDirectory))
        {
            Directory.CreateDirectory(SettingsDirectory);
        }

        settings.CurrentEnableSettings.Add(mainViewModel.UdpEnableSetting);
        settings.CurrentEnableSettings.Add(mainViewModel.DmxEnabledSetting);
        settings.CurrentEnableSettings.Add(mainViewModel.HueEnabledSetting);
        settings.CurrentEnableSettings.Add(mainViewModel.StageKitEnabledSetting);
        settings.CurrentEnableSettings.Add(mainViewModel.Rb3eEnabledSetting);
        settings.CurrentEnableSettings.Add(mainViewModel.OpenRgbEnabledSetting);
        settings.CurrentEnableSettings.Add(mainViewModel.SerialEnabledSetting);

        settings.CurrentSingleSettings.Add(mainViewModel.BpmChannelSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.CueChangeChannelSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.PostProcessingChannelSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.KeyFrameChannelSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.BeatLineChannelSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.BonusEffectChannelSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.DrumNoteChannelSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.GuitarNoteChannelSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.BassNoteChannelSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.CurrentSpotlightSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.CurrentSingalongSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.KeysNoteChannelSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.VocalsNoteChannelSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.Harmony0NoteChannelSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.Harmony1NoteChannelSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.Harmony2NoteChannelSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.BroadcastUniverseSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.CurrentSceneSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.VenueSizeSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.PauseStateSetting);
        settings.CurrentSingleSettings.Add(mainViewModel.SongSectionSetting);

        settings.CurrentChannelSettings.Add(mainViewModel.StrobeChannels);
        settings.CurrentChannelSettings.Add(mainViewModel.FogChannels);
        settings.CurrentChannelSettings.Add(mainViewModel.RedChannels);
        settings.CurrentChannelSettings.Add(mainViewModel.BlueChannels);
        settings.CurrentChannelSettings.Add(mainViewModel.YellowChannels);
        settings.CurrentChannelSettings.Add(mainViewModel.GreenChannels);

        settings.CurrentMasterDimmerChannelSettings = mainViewModel.MasterDimmerSettings;
        settings.CurrentMasterDimmerValueChannelSettings = mainViewModel.MasterDimmerValues;

        settings.HueAuthResult = mainViewModel.HueAuthResult;
        settings.HueBridgeIP = mainViewModel.HueBridgeIp;
        settings.UdpListenPort = mainViewModel.UdpListenPort;

        settings.OpenRgbServerIp = mainViewModel.OpenRgbServerIp;
        settings.OpenRgbServerPort = mainViewModel.OpenRgbServerPort;

        File.WriteAllText(SettingsFilePath, JsonConvert.SerializeObject(settings, Formatting.Indented));
    }

    public static void LoadSettings()
    {
        if (!Directory.Exists(SettingsDirectory))
        {
            Directory.CreateDirectory(SettingsDirectory);
        }

        if (IsJsonFileValid(SettingsFilePath))
        {
            var container = JsonConvert.DeserializeObject<SettingsContainer>(File.ReadAllText(SettingsFilePath));

            if (container == null) return;

            foreach (var enable in container.CurrentEnableSettings)
            {
                switch (enable.Label)
                {
                    case "UDP Enabled":
                        UdpEnableSettingIsEnabled = enable.IsEnabled;
                        break;

                    case "DMX Enabled":
                        DmxEnabledSettingIsEnabled = enable.IsEnabled;
                        break;

                    case "Hue Enabled":
                        HueEnabledSettingIsEnabled = enable.IsEnabled;
                        break;

                    case "StageKit Enabled":
                        StageKitEnabledSettingIsEnabled = enable.IsEnabled;
                        break;

                    case "RB3E Enabled":
                        Rb3eEnabledSettingIsEnabled = enable.IsEnabled;
                        break;

                    case "Serial Enabled":
                        SerialEnabledSettingIsEnabled = enable.IsEnabled;
                        break;

                    case "OpenRGB Enabled":
                        OpenRgbEnabledSettingIsEnabled = enable.IsEnabled;
                        break;
                }
            }

            //These might need to be constructed like the above, but this seems to work for now.
            foreach (var single in container.CurrentSingleSettings)
            {
                switch (single.Label)
                {
                    case "BPM Channel":
                        BpmChannelSettingValue = single.Value;
                        break;

                    case "Cue Change Channel":
                        CueChangeChannelSettingValue = single.Value;
                        break;

                    case "Post-Processing Channel":
                        PostProcessingChannelSettingValue = single.Value;
                        break;

                    case "KeyFrame Channel":
                        KeyFrameChannelSettingValue = single.Value;
                        break;

                    case "BeatLine Channel":
                        BeatLineChannelSettingValue = single.Value;
                        break;

                    case "Bonus Effect Channel":
                        BonusEffectChannelSettingValue = single.Value;
                        break;

                    case "Drum Note Channel":
                        DrumNoteChannelSettingValue = single.Value;
                        break;

                    case "Guitar Note Channel":
                        GuitarNoteChannelSettingValue = single.Value;
                        break;

                    case "Bass Note Channel":
                        BassNoteChannelSettingValue = single.Value;
                        break;

                    case "Keys Note Channel":
                        KeysNoteChannelSettingValue = single.Value;
                        break;

                    case "Vocals Note Channel":
                        VocalsNoteChannelSettingValue = single.Value;
                        break;

                    case "Harmony 0 Note Channel":
                        Harmony0NoteChannelSettingValue = single.Value;
                        break;

                    case "Harmony 1 Note Channel":
                        Harmony1NoteChannelSettingValue = single.Value;
                        break;

                    case "Harmony 2 Note Channel":
                        Harmony2NoteChannelSettingValue = single.Value;
                        break;

                    case "Current Scene":
                        CurrentSceneSettingValue = single.Value;
                        break;

                    case "Venue Size":
                        VenueSizeSettingValue = single.Value;
                        break;

                    case "Pause State":
                        PauseStateSettingValue = single.Value;
                        break;

                    case "Song Section":
                        SongSectionSettingValue = single.Value;
                        break;

                    case "Current Spotlight":
                        CurrentSpotlightSettingValue = single.Value;
                        break;

                    case "Current Singalong":
                        CurrentSingalongSettingValue = single.Value;
                        break;

                    case "Broadcast Universe":
                        BroadcastUniverseSettingValue = single.Value;
                        break;
                }

                foreach (var channel in container.CurrentChannelSettings)
                {
                    switch (channel.Label)
                    {
                        case "Fog Channels":
                            FogChannelsChannel = channel.Channel;
                            break;

                        case "Strobe Channels":
                            StrobeChannelsChannel = channel.Channel;
                            break;

                        case "Red Channels":
                            RedChannelsChannel = channel.Channel;
                            break;

                        case "Blue Channels":
                            BlueChannelsChannel = channel.Channel;
                            break;

                        case "Yellow Channels":
                            YellowChannelsChannel = channel.Channel;
                            break;

                        case "Green Channels":
                            GreenChannelsChannel = channel.Channel;
                            break;

                    }
                }
            }

            MasterDimmerValuesChannel = container.CurrentMasterDimmerValueChannelSettings.Channel;
            MasterDimmerSettingsChannel = container.CurrentMasterDimmerChannelSettings.Channel;

            HueAuthResultUsername = container.HueAuthResult.Username;
            HueAuthResultStreamingClientKey = container.HueAuthResult.StreamingClientKey;
            HueAuthResultIp = container.HueAuthResult.Ip;
            HueBridgeIp = container.HueBridgeIP;

            UdpListenPort = container.UdpListenPort;

            OpenRgbServerIp = container.OpenRgbServerIp;
            OpenRgbServerPort = container.OpenRgbServerPort;
        }
        else // File is either garbage or doesn't exist. Load defaults.
        {
            Console.WriteLine("Settings file is either garbage or doesn't exist. Loading defaults.");

            UdpEnableSettingIsEnabled = true;
            DmxEnabledSettingIsEnabled = true;
            HueEnabledSettingIsEnabled = true;
            StageKitEnabledSettingIsEnabled = true;
            Rb3eEnabledSettingIsEnabled = true;
            OpenRgbEnabledSettingIsEnabled = true;
            SerialEnabledSettingIsEnabled = true;

            BpmChannelSettingValue = 57;
            CueChangeChannelSettingValue = 58;
            BeatLineChannelSettingValue = 59;
            BonusEffectChannelSettingValue = 60;
            KeyFrameChannelSettingValue = 61;
            DrumNoteChannelSettingValue = 62;
            PostProcessingChannelSettingValue = 63;
            GuitarNoteChannelSettingValue = 64;
            BassNoteChannelSettingValue = 65;
            CurrentSpotlightSettingValue = 66;
            CurrentSingalongSettingValue = 67;
            KeysNoteChannelSettingValue = 68;
            VocalsNoteChannelSettingValue = 69;
            Harmony0NoteChannelSettingValue = 70;
            Harmony1NoteChannelSettingValue = 71;
            Harmony2NoteChannelSettingValue = 72;
            CurrentSceneSettingValue = 73;
            VenueSizeSettingValue = 74;
            PauseStateSettingValue = 75;
            SongSectionSettingValue = 76;

            BroadcastUniverseSettingValue = 1;

            MasterDimmerSettingsChannel = new[] { 1, 8, 15, 22, 29, 36, 43, 50, 57, 64, 71, 78, 85, 92, 99, 106 };
            MasterDimmerValuesChannel = new[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 };
            FogChannelsChannel = new[] { 6, 13, 20, 27, 34, 41, 48, 55 };
            StrobeChannelsChannel = new[] { 7, 14, 21, 28, 35, 42, 49, 56 };
            RedChannelsChannel = new[] { 2, 9, 16, 23, 30, 37, 44, 51 };
            BlueChannelsChannel = new[] { 3, 10, 17, 24, 31, 38, 45, 52 };
            YellowChannelsChannel = new[] { 4, 11, 18, 25, 32, 39, 46, 53 };
            GreenChannelsChannel = new[] { 5, 12, 19, 26, 33, 40, 47, 54 };

            HueAuthResultUsername = "";
            HueAuthResultStreamingClientKey = "";
            HueAuthResultIp = "";
            HueBridgeIp = "";

            UdpListenPort = 36107;

            OpenRgbServerIp = "127.0.0.1";
            OpenRgbServerPort = 6742;
        }
    }

    private static bool IsJsonFileValid(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var jsonContent = File.ReadAllText(filePath);
            JToken.Parse(jsonContent);
            return true;
        }
        catch (JsonReaderException)
        {
            return false;
        }
    }
}
