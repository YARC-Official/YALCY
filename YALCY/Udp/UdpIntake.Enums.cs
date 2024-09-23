using System;

namespace YALCY.Udp;

public partial class UdpIntake
{
    const uint PACKET_HEADER = 0x59415247; // YARG

     public enum ByteIndexName
    {
        //header
        Header,
        //Tech info
        DatagramVersion,
        Platform,
        //game info
        CurrentScene,
        PauseState,
        VenueSize,
        //song info
        BeatsPerMinute,
        SongSection,
        //instruments
        GuitarNotes,
        BassNotes,
        DrumsNotes,
        KeysNotes,
        VocalsNote,
        Harmony0Note,
        Harmony1Note,
        Harmony2Note,
        // Lighting information
        LightingCue,
        PostProcessing,
        FogState,
        StrobeState,
        Performer,
        Beat,
        Keyframe,
        BonusEffect,
    }

     public enum SongSectionByte
	 {
		 None = 0,
         Chorus = 2,
		 Verse = 5,
	 }

     public enum VocalHarmonyBytes
     {   Unpitched = -1,
         None = 0,
         C6 = 84,
         B5 = 83,
         Bb5 = 82,
         A5 = 81,
         GSharp5 = 80, // G#5
         G5 = 79,
         FSharp5 = 78, // F#5
         F5 = 77,
         E5 = 76,
         Eb5 = 75,
         D5 = 74,
         CSharp5 = 73, // C#5
         C5 = 72,
         B4 = 71,
         Bb4 = 70,
         A4 = 69,
         GSharp4 = 68, // G#4
         G4 = 67,
         FSharp4 = 66, // F#4
         F4 = 65,
         E4 = 64,
         Eb4 = 63,
         D4 = 62,
         CSharp4 = 61, // C#4
         C4 = 60,
         B3 = 59,
         Bb3 = 58,
         A3 = 57,
         GSharp3 = 56, // G#3
         G3 = 55,
         FSharp3 = 54, // F#3
         F3 = 53,
         E3 = 52,
         Eb3 = 51,
         D3 = 50,
         CSharp3 = 49, // C#3
         C3 = 48,
         B2 = 47,
         Bb2 = 46,
         A2 = 45,
         GSharp2 = 44, // G#2
         G2 = 43,
         FSharp2 = 42, // F#2
         F2 = 41,
         E2 = 40,
         Eb2 = 39,
         D2 = 38,
         CSharp2 = 37, // C#2
         C2 = 36
     }

    private enum DatagramVersionByte
    {
        Version,
    }

    private enum PlatformByte
    {
        Unknown,
        Windows,
        Linux,
        Mac,
    }
    public enum BeatByte
    {
        Off,
        Measure,
        Strong,
        Weak,
    }
    public enum KeyFrameByte
    {
        Off = 0,
        KeyframeFirst = 27,
        KeyframeNext = 28,
        KeyframePrevious = 29,
    }

    public enum VenueSizeByte
    {
        NoVenue,
        Small,
        Large,
    }

    public enum FogStateByte
    {
        Off,
        On,
    }

    private enum GuitarBassKeyboardNotesByte
    {
        None = 0,
        Open = 1 << 0,
        Green = 1 << 1,
        Red = 1 <<2,
        Yellow = 1 << 3,
        Blue = 1 << 4,
        Orange = 1 << 5,
    }

    [Flags]
    public enum DrumNotesByte
    {
        None = 0,
        Kick = 1 << 0,
        RedDrum = 1 << 1,
        YellowDrum = 1 << 2,
        BlueDrum = 1 << 3,
        GreenDrum = 1 << 4,
        YellowCymbal = 1 << 5,
        BlueCymbal = 1 << 6,
        GreenCymbal = 1 << 7,
    }

    public enum CueByte
    {
        Default,
        Dischord,
        Chorus,
        Cool_Manual,
        Stomp,
        Verse,
        Warm_Manual,

        // Automatic
        BigRockEnding,
        Blackout_Fast,
        Blackout_Slow,
        Blackout_Spotlight,
        Cool_Automatic,
        Flare_Fast,
        Flare_Slow,
        Frenzy,
        Intro,
        Harmony,
        Silhouettes,
        Silhouettes_Spotlight,
        Searchlights,
        Strobe_Fastest,
        Strobe_Fast,
        Strobe_Medium,
        Strobe_Slow,
        Strobe_Off,
        Sweep,
        Warm_Automatic,

        // Keyframe events
        Keyframe_First,
        Keyframe_Next,
        Keyframe_Previous,

        //YARG internal
        Menu,
        Score,
        NoCue,
    }

    private enum PostProcessingByte
    {
        // Basic effects
        Default,
        Bloom,
        Bright,
        Contrast,
        Posterize,
        PhotoNegative,
        Mirror,

        // Color filters/effects
        BlackAndWhite,
        SepiaTone,
        SilverTone,

        Choppy_BlackAndWhite,
        PhotoNegative_RedAndBlack,
        Polarized_BlackAndWhite,
        Polarized_RedAndBlue,

        Desaturated_Blue,
        Desaturated_Red,

        Contrast_Red,
        Contrast_Green,
        Contrast_Blue,

        // Grainy
        Grainy_Film,
        Grainy_ChromaticAbberation,

        // Scanlines
        Scanlines,
        Scanlines_BlackAndWhite,
        Scanlines_Blue,
        Scanlines_Security,

        // Trails
        Trails,
        Trails_Long,
        Trails_Desaturated,
        Trails_Flickery,
        Trails_Spacey,
    }

    private enum SceneIndexByte
    {
        Unknown,
        Menu,
        Gameplay,
        Score,
        Calibration,
    }
}
