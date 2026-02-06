using System;

namespace YALCY.Udp;

public partial class UdpIntake
{
    public static Action<byte>? OnBeat { get; set; }
    public static Action<byte>? OnKeyFrame { get; set; }
    public static Action<byte>? OnDrum { get; set; }
    public static Action<float>? OnVocalHarmony { get; set; }
    public static Action<byte>? OnLightingCue { get; set; }
    public static Action<bool>? OnFogState { get; set; }
    public static Action<byte>? OnStrobeState { get; set; }
    public static Action<byte>? OnPause { get; set; }
    public static string GetVocalHarmonyByteDescription(float byteValue)
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
        OnVocalHarmony?.Invoke(byteValue);
        return vocalHarmonyDescription;
    }
    public static string GetPauseByteDescription(byte byteValue)
    {
        var pauseDescription = byteValue switch
        {
            0 => "At menu",
            1 => "Unpaused",
            2 => "Paused",
        };
        OnPause?.Invoke(byteValue);
        return pauseDescription;
    }

    public static string GetSongSectionByteDescription(byte byteValue)
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

    public static string GetVenueSizeByteDescription(byte byteValue)
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

    public static string GetFogStateByteDescription(bool byteValue)
    {
        var fogStateDescription = byteValue switch
        {
            false => "Off",
            true => "On",
        };
        OnFogState?.Invoke(byteValue);
        return fogStateDescription;
    }

    public static string GetDrumsByteDescription(byte byteValue)
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
        OnDrum?.Invoke(byteValue);
        return result;
    }

    public static string GetInstrumentByteDescription(byte byteValue)
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

    public static string GetDatagramVersionByteDescription(byte byteValue)
    {
        return byteValue == (int)DatagramVersionByte.Version ? "Current Version" : "Unknown";
    }

    public static string GetHeaderByteDescription(uint byteValue)
    {
        if (byteValue != PACKET_HEADER)
        {
            return "Wrong header byte!";
        }

        return "YARG";
    }

    public static string GetBeatlineByteDescription(byte byteValue)
    {
        var beatlineDescription = byteValue switch
        {
            0 => "Measure",
            1 => "Strong",
            2 => "Weak",
            3 => "Off",
            _ => "Unknown"
        };

        OnBeat?.Invoke(byteValue);

        return beatlineDescription;
    }

    public static string GetKeyFrameDescription(byte byteValue)
    {
        var keyFrameDescription = byteValue switch
        {
            (int)KeyFrameByte.Off => "Off",
            (int)KeyFrameByte.KeyframeNext => "Next",
            (int)KeyFrameByte.KeyframePrevious => "Previous",
            (int)KeyFrameByte.KeyframeFirst => "First",
            _ => ""
        };

        OnKeyFrame?.Invoke(byteValue);
        return keyFrameDescription;
    }

    public static string GetStrobeByteDescription(byte byteValue)
    {
        var strobeDescription = byteValue switch
        {
            (byte)CueByte.Strobe_Off => "Off",
            (byte)CueByte.Strobe_Slow => "Slow",
            (byte)CueByte.Strobe_Medium => "Medium",
            (byte)CueByte.Strobe_Fast => "Fast",
            (byte)CueByte.Strobe_Fastest => "Fastest",

            _ => "Unknown"
        };
        OnStrobeState?.Invoke(byteValue);
        return strobeDescription;
    }

    public static string GetPlatformByteDescription(byte byteValue)
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

    public static string GetCueByteDescription(byte byteValue)
    {
            int intValue = byteValue; // Cast byte to int
            OnLightingCue?.Invoke(byteValue);
            return Enum.IsDefined(typeof(CueByte), intValue) ? ((CueByte)intValue).ToString() : "Unknown";
    }

    public static string GetPerformerDescription(byte byteValue)
    {
        var result = "";

        foreach (PerformerByte note in Enum.GetValues<PerformerByte>())
        {
            if (note == PerformerByte.None || (byteValue & (byte)note) == 0) continue;
            if (result != "")
            {
                result += ", ";
            }
            result += note.ToString();
        }

        // If no bits are set, it means "None"
        if (string.IsNullOrEmpty(result))
        {
            result = PerformerByte.None.ToString();
        }
        OnDrum?.Invoke(byteValue);
        return result;
    }

    public static string GetAutoGenByteDescription(bool boolValue)
    {
        var AutoGenBDescription = boolValue switch
        {
            false => "Authored",
            true => "Generated",
        };
        return AutoGenBDescription;
    }
    public static string GetPostProcessingByteDescription(byte byteValue)
    {
        int intValue = byteValue; // Cast byte to int
        return Enum.IsDefined(typeof(PostProcessingByte), intValue) ? ((PostProcessingByte)intValue).ToString() : "Unknown";
    }

    public static string GetBonusEffectByteDescription(bool byteValue)
    {
        var bonusEffectDescription = byteValue switch
        {
            false => "Off",
            true => "Triggered!",
        };
        return bonusEffectDescription;
    }

    public static string GetSceneIndexByteDescription(byte byteValue)
    {
        var sceneIndexDescription = byteValue switch
        {
            (int)SceneIndexByte.Unknown => "Unknown",
            (int)SceneIndexByte.Menu => "Menu",
            (int)SceneIndexByte.Gameplay => "Gameplay",
            (int)SceneIndexByte.Score => "Score",
            (int)SceneIndexByte.Calibration => "Calibration",
            (int)SceneIndexByte.Practice => "Practice",
            _ => "Unknown"
        };
        return sceneIndexDescription;
    }

}
