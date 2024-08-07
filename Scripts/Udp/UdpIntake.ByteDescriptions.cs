using System;
using YALCY.ViewModels;

namespace YALCY;

public partial class UdpIntake
{
    private static string GetVocalHarmonyByteDescription(byte byteValue)
    {
        var vocalHarmonyDescription = byteValue switch
        {
            (int)VocalHarmonyBytes.None => "None",
            (int)VocalHarmonyBytes.Unpitched => "No pitch",

            (int)VocalHarmonyBytes.A2 => "A2",
            (int)VocalHarmonyBytes.A3 => "A3",
            (int)VocalHarmonyBytes.A4 => "A4",
            (int)VocalHarmonyBytes.A5 => "A5",

            (int)VocalHarmonyBytes.B2 => "B2",
            (int)VocalHarmonyBytes.B3 => "B3",
            (int)VocalHarmonyBytes.B4 => "B4",
            (int)VocalHarmonyBytes.B5 => "B5",

            (int)VocalHarmonyBytes.Bb2 => "Bb2",
            (int)VocalHarmonyBytes.Bb3 => "Bb3",
            (int)VocalHarmonyBytes.Bb4 => "Bb4",
            (int)VocalHarmonyBytes.Bb5 => "Bb5",

            (int)VocalHarmonyBytes.C2 => "C2",
            (int)VocalHarmonyBytes.C3 => "C3",
            (int)VocalHarmonyBytes.C4 => "C4",
            (int)VocalHarmonyBytes.C5 => "C5",
            (int)VocalHarmonyBytes.C6 => "C6",

            (int)VocalHarmonyBytes.CSharp2 => "C#2",
            (int)VocalHarmonyBytes.CSharp3 => "C#3",
            (int)VocalHarmonyBytes.CSharp4 => "C#4",
            (int)VocalHarmonyBytes.CSharp5 => "C#5",

            (int)VocalHarmonyBytes.D2 => "D2",
            (int)VocalHarmonyBytes.D3 => "D3",
            (int)VocalHarmonyBytes.D4 => "D4",
            (int)VocalHarmonyBytes.D5 => "D5",

            (int)VocalHarmonyBytes.E2 => "E2",
            (int)VocalHarmonyBytes.E3 => "E3",
            (int)VocalHarmonyBytes.E4 => "E4",
            (int)VocalHarmonyBytes.E5 => "E5",

            (int)VocalHarmonyBytes.Eb2 => "Eb2",
            (int)VocalHarmonyBytes.Eb3 => "Eb3",
            (int)VocalHarmonyBytes.Eb4 => "Eb4",
            (int)VocalHarmonyBytes.Eb5 => "Eb5",

            (int)VocalHarmonyBytes.F2 => "F2",
            (int)VocalHarmonyBytes.F3 => "F3",
            (int)VocalHarmonyBytes.F4 => "F4",
            (int)VocalHarmonyBytes.F5 => "F5",

            (int)VocalHarmonyBytes.FSharp2 => "F#2",
            (int)VocalHarmonyBytes.FSharp3 => "F#3",
            (int)VocalHarmonyBytes.FSharp4 => "F#4",
            (int)VocalHarmonyBytes.FSharp5 => "F#5",

            (int)VocalHarmonyBytes.G2 => "G2",
            (int)VocalHarmonyBytes.G3 => "G3",
            (int)VocalHarmonyBytes.G4 => "G4",
            (int)VocalHarmonyBytes.G5 => "G5",

            (int)VocalHarmonyBytes.GSharp2 => "G#2",
            (int)VocalHarmonyBytes.GSharp3 => "G#3",
            (int)VocalHarmonyBytes.GSharp4 => "G#4",
            (int)VocalHarmonyBytes.GSharp5 => "G#5",

            _ => "Unknown"

        };
        return vocalHarmonyDescription;
    }
    private static string GetPauseByteDescription(byte byteValue)
    {
        var pauseDescription = byteValue switch
        {
            (int)PauseByte.Unpaused => "Unpaused",
            (int)PauseByte.Paused => "Paused",
            _ => "Unknown"
        };

        return pauseDescription;
    }

    private static string GetSongSectionByteDescription(byte byteValue)
    {
        var songSectionDescription = byteValue switch
        {
            (int)SongSectionByte.None => "None",
            (int)SongSectionByte.Verse => "Verse",
            (int)SongSectionByte.Chorus => "Chorus",
            _ => "Unknown"
        };
        return songSectionDescription;
    }

    private static string GetVenueSizeByteDescription(byte byteValue)
    {
        var venueSizeDescription = byteValue switch
        {
            (int)VenueSizeByte.NoVenue => "No Venue",
            (int)VenueSizeByte.Small => "Small",
            (int)VenueSizeByte.Large => "Large",
            _ => "Unknown"
        };
        return venueSizeDescription;
    }

    private static string GetFogStateByteDescription(byte byteValue)
    {
        var fogStateDescription = byteValue switch
        {
            (int)FogStateByte.Off => "Off",
            (int)FogStateByte.On => "On",
            _ => "Unknown"
        };
        return fogStateDescription;
    }

    private static string GetDrumsByteDescription(byte byteValue)
    {
        var result = "";

        foreach (DrumNotesByte note in Enum.GetValues<DrumNotesByte>())
        {
            if (note == DrumNotesByte.None || (byteValue & (byte)note) == 0) continue;
            if (result != "")
            {
                result += ", ";
            }
            result += note.ToString();
        }

        // If no bits are set, it means "None"
        if (string.IsNullOrEmpty(result))
        {
            result = DrumNotesByte.None.ToString();
        }

        return result;
    }
    private static string GetInstrumentByteDescription(byte byteValue)
    {
        var result = "";

        foreach (GuitarBassKeyboardNotesByte color in Enum.GetValues(typeof(GuitarBassKeyboardNotesByte)))
        {
            if (color == GuitarBassKeyboardNotesByte.None || (byteValue & (byte)color) == 0) continue;
            if (result != "")
            {
                result += ", ";
            }
            result += color.ToString();
        }

        // If no bits are set, it means "None"
        if (string.IsNullOrEmpty(result))
        {
            result = GuitarBassKeyboardNotesByte.None.ToString();
        }

        return result;
    }
    private static string GetDatagramVersionByteDescription(byte byteValue)
    {
        return byteValue == (int)DatagramVersionByte.Version ? "Current Version" : "Unknown";
    }
    private static string GetHeaderByteDescription(byte byteValue)
    {
        var headerDescription = byteValue switch
        {
            (int)HeaderBytes.HeaderByte1 => "Y",
            (int)HeaderBytes.HeaderByte2 => "A",
            (int)HeaderBytes.HeaderByte3 => "R",
            (int)HeaderBytes.HeaderByte4 => "G",
            _ => "Wrong header byte!"
        };
        return headerDescription;
    }
    private static string GetBeatlineByteDescription(BeatByte byteValue)
    {
        var beatlineDescription = byteValue switch
        {
            BeatByte.Off => "Off",
            BeatByte.Measure => "Measure",
            BeatByte.Strong => "Strong",
            BeatByte.Weak => "Weak",
            _ => "Unknown"
        };

        return beatlineDescription;
    }
    private static string GetKeyFrameDescription(byte byteValue)
    {
        var keyFrameDescription = byteValue switch
        {
            (int)KeyFrameByte.Off => "Off",
            (int)KeyFrameByte.KeyframeNext => "Next",
            (int)KeyFrameByte.KeyframePrevious => "Previous",
            (int)KeyFrameByte.KeyframeFirst => "First",
            _ => ""
        };
        return keyFrameDescription;
    }
    private static string GetStrobeByteDescription(byte byteValue)
    {
        var strobeDescription = byteValue switch
        {
            (int)StrobeSpeedByte.Off => "Off",
            (int)StrobeSpeedByte.Slow => "Slow",
            (int)StrobeSpeedByte.Medium => "Medium",
            (int)StrobeSpeedByte.Fast => "Fast",
            (int)StrobeSpeedByte.Fastest => "Fastest",
            _ => "Unknown"
        };

        return strobeDescription;
    }
    private static string GetPlatformByteDescription(byte byteValue)
    {
        var os = byteValue switch
        {
            (int)PlatformByte.Unknown => "Unknown",
            (int)PlatformByte.Windows => "Windows",
            (int)PlatformByte.Linux => "Linux",
            (int)PlatformByte.Mac => "Mac",
            _ => "Unknown"
        };

        return os;
    }

    private static string GetCueByteDescription(byte byteValue)
    {
            int intValue = byteValue; // Cast byte to int
            return Enum.IsDefined(typeof(CueByte), intValue) ? ((CueByte)intValue).ToString() : "Unknown";
    }
    private static string GetPostProcessingByteDescription(byte byteValue)
    {
        int intValue = byteValue; // Cast byte to int
        return Enum.IsDefined(typeof(PostProcessingByte), intValue) ? ((PostProcessingByte)intValue).ToString() : "Unknown";
    }
    private static string GetBonusEffectByteDescription(byte byteValue)
    {
        var bonusEffectDescription = byteValue switch
        {
            (int)BonusEffectByte.Off => "Off",
            (int)BonusEffectByte.On => "Triggered!",
            _ => "Unknown"
        };
        return bonusEffectDescription;
    }

    private static string GetSceneIndexByteDescription(byte byteValue)
    {
        var sceneIndexDescription = byteValue switch
        {
            (int)SceneIndexByte.Unknown => "Unknown",
            (int)SceneIndexByte.Menu => "Menu",
            (int)SceneIndexByte.Gameplay => "Gameplay",
            (int)SceneIndexByte.Score => "Score",
            (int)SceneIndexByte.Calibration => "Calibration",
            _ => "Unknown"
        };
        return sceneIndexDescription;
    }
}
