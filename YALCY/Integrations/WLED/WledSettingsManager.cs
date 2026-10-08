using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YALCY.ViewModels;

namespace YALCY.Integrations.WLED;

/// <summary>
/// Encapsulates all persistent user configuration options for the WLED integration.
/// Stored in a dedicated "WledSettings.json" file in the YALCY settings directory.
/// </summary>
public sealed class WledSetting
{
    public string ServerIp { get; set; } = "wled";
    public ushort ServerPort { get; set; } = (ushort)WledSender.DefaultDdpPort;
    public int Protocol { get; set; } = (int)WledProtocol.DdpRgb;
    public int LedCount { get; set; } = 32;
    public int Brightness { get; set; } = 255;
    public int WhiteChannelBrightness { get; set; } = 255;
    public int LayoutMode { get; set; } = (int)WledLayoutMode.TiledRepeat32;
    public int WhiteStrobeMode { get; set; } = (int)WledWhiteStrobeMode.Disabled;
    public int StrobeMode { get; set; } = StrobeOutputModes.ManualFlash;
    public bool RhythmicWhiteAccent { get; set; } = false;
    public bool SnareFlashEnabled { get; set; } = false;
    public bool StarPowerPulseEnabled { get; set; } = false;
    public int TimeoutSeconds { get; set; } = 2;
    public bool ReverseDirection { get; set; } = false;
    public int StartOffset { get; set; } = 0;
    public int SnareFlashIntensity { get; set; } = 255;
    public int BeatPulseIntensity { get; set; } = 180;
    public int FogHazeBoost { get; set; } = 30;
    public int MaxBrightnessLimit { get; set; } = 255;
    public int RepeatStyle { get; set; } = (int)WledRepeatStyle.ContinuousLoop;
    public int RepeatTileSize { get; set; } = 32;
    public int LayoutGapLeds { get; set; } = 0;
    public int StretchInterpolation { get; set; } = (int)WledStretchInterpolation.PixelSharp;
    public int StretchPadding { get; set; } = 0;
    public int PodColorOrder { get; set; } = (int)WledPodColorOrder.BlueRedGreenYellow;
    public int PodGapLeds { get; set; } = 0;
    public int CenterSplitDirection { get; set; } = (int)WledCenterSplitDirection.InsideOut;
    public int CenterSplitOffset { get; set; } = 0;
    public int DirectExcessMode { get; set; } = (int)WledDirectExcessMode.Blackout;
    public int WashBlendMode { get; set; } = (int)WledWashBlendMode.HarmonicAverage;
    public int GradientCycles { get; set; } = 1;
}

/// <summary>
/// Dedicated manager responsible for loading and saving WLED configuration
/// to its own dedicated file (WledSettings.json).
/// </summary>
public static class WledSettingsManager
{
    private static readonly string SettingsDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YALCY", "Settings");

    private static readonly string SettingsFilePath =
        Path.Combine(SettingsDirectory, "WledSettings.json");

    public static readonly WledSetting Default = new();
    public static WledSetting Current { get; private set; } = new();

    public static void Save(MainWindowViewModel mainViewModel)
    {
        if (!Directory.Exists(SettingsDirectory))
        {
            Directory.CreateDirectory(SettingsDirectory);
        }

        Current = new WledSetting
        {
            ServerIp = string.IsNullOrWhiteSpace(mainViewModel.WledServerIp) ? Default.ServerIp : mainViewModel.WledServerIp,
            ServerPort = mainViewModel.WledServerPort,
            Protocol = mainViewModel.WledProtocol,
            LedCount = mainViewModel.WledLedCount,
            Brightness = mainViewModel.WledBrightness,
            WhiteChannelBrightness = mainViewModel.WledWhiteChannelBrightness,
            LayoutMode = mainViewModel.WledLayoutMode,
            WhiteStrobeMode = mainViewModel.WledWhiteStrobeMode,
            StrobeMode = mainViewModel.WledStrobeMode,
            RhythmicWhiteAccent = mainViewModel.WledRhythmicWhiteAccent,
            SnareFlashEnabled = mainViewModel.WledSnareFlashEnabled,
            StarPowerPulseEnabled = mainViewModel.WledStarPowerPulseEnabled,
            TimeoutSeconds = mainViewModel.WledTimeoutSeconds,
            ReverseDirection = mainViewModel.WledReverseDirection,
            StartOffset = mainViewModel.WledStartOffset,
            SnareFlashIntensity = mainViewModel.WledSnareFlashIntensity,
            BeatPulseIntensity = mainViewModel.WledBeatPulseIntensity,
            FogHazeBoost = mainViewModel.WledFogHazeBoost,
            MaxBrightnessLimit = mainViewModel.WledMaxBrightnessLimit,
            RepeatStyle = mainViewModel.WledRepeatStyle,
            RepeatTileSize = mainViewModel.WledRepeatTileSize,
            LayoutGapLeds = mainViewModel.WledLayoutGapLeds,
            StretchInterpolation = mainViewModel.WledStretchInterpolation,
            StretchPadding = mainViewModel.WledStretchPadding,
            PodColorOrder = mainViewModel.WledPodColorOrder,
            PodGapLeds = mainViewModel.WledPodGapLeds,
            CenterSplitDirection = mainViewModel.WledCenterSplitDirection,
            CenterSplitOffset = mainViewModel.WledCenterSplitOffset,
            DirectExcessMode = mainViewModel.WledDirectExcessMode,
            WashBlendMode = mainViewModel.WledWashBlendMode,
            GradientCycles = mainViewModel.WledGradientCycles
        };

        File.WriteAllText(SettingsFilePath, JsonConvert.SerializeObject(Current, Formatting.Indented));
    }

    public static void Load()
    {
        if (File.Exists(SettingsFilePath))
        {
            try
            {
                var content = File.ReadAllText(SettingsFilePath);
                var loaded = JsonConvert.DeserializeObject<WledSetting>(content);
                if (loaded != null)
                {
                    Sanitize(loaded);
                    Current = loaded;
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading WledSettings.json: {ex.Message}");
            }
        }
        else
        {
            // Backwards-compatibility fallback: migrate from Settings.json if present
            var legacySettingsPath = Path.Combine(SettingsDirectory, "Settings.json");
            if (File.Exists(legacySettingsPath))
            {
                try
                {
                    var text = File.ReadAllText(legacySettingsPath);
                    var jobj = JObject.Parse(text);
                    if (jobj.TryGetValue("WledSettings", out var wTok) && wTok is JObject wObj)
                    {
                        var fromLegacy = wObj.ToObject<WledSetting>();
                        if (fromLegacy != null)
                        {
                            Sanitize(fromLegacy);
                            Current = fromLegacy;
                            SaveCurrent();
                            return;
                        }
                    }
                }
                catch { }
            }
        }

        Current = new WledSetting();
    }

    private static void Sanitize(WledSetting w)
    {
        w.ServerIp = string.IsNullOrWhiteSpace(w.ServerIp) ? Default.ServerIp : w.ServerIp;
        w.ServerPort = w.ServerPort > 0 ? w.ServerPort : Default.ServerPort;
        w.LedCount = w.LedCount is > 0 and <= 1200 ? w.LedCount : Default.LedCount;
        w.Brightness = Math.Clamp(w.Brightness, 0, 255);
        w.WhiteChannelBrightness = Math.Clamp(w.WhiteChannelBrightness, 0, 255);
        w.TimeoutSeconds = Math.Clamp(w.TimeoutSeconds, 1, 30);
        w.StartOffset = Math.Clamp(w.StartOffset, 0, 1199);
        w.SnareFlashIntensity = Math.Clamp(w.SnareFlashIntensity, 0, 255);
        w.BeatPulseIntensity = Math.Clamp(w.BeatPulseIntensity, 0, 255);
        w.FogHazeBoost = Math.Clamp(w.FogHazeBoost, 0, 255);
        w.MaxBrightnessLimit = Math.Clamp(w.MaxBrightnessLimit, 10, 255);
        w.RepeatTileSize = w.RepeatTileSize is >= 4 and <= 128 ? w.RepeatTileSize : Default.RepeatTileSize;
        w.LayoutGapLeds = Math.Clamp(w.LayoutGapLeds, 0, 50);
        w.StretchPadding = Math.Clamp(w.StretchPadding, 0, 50);
        w.PodGapLeds = Math.Clamp(w.PodGapLeds, 0, 50);
        w.CenterSplitOffset = Math.Clamp(w.CenterSplitOffset, -100, 100);
        w.GradientCycles = Math.Clamp(w.GradientCycles, 1, 4);
    }

    private static void SaveCurrent()
    {
        try
        {
            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
            }
            File.WriteAllText(SettingsFilePath, JsonConvert.SerializeObject(Current, Formatting.Indented));
        }
        catch { }
    }
}
