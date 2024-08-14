using System;

namespace YALCY.Udp;

public partial class UdpIntake
{
     public enum ByteIndexName
    {
        //header
        HeaderByte1,
        HeaderByte2,
        HeaderByte3,
        HeaderByte4,
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
		 None,
		 Verse,
		 Chorus,
	 }

     public enum VocalHarmonyBytes
     {
         None = 0,
         Unpitched = 255,
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

    private enum HeaderBytes
    {
        HeaderByte1 = 0x59, // Y
        HeaderByte2 = 0x41, // A
        HeaderByte3 = 0x52, // R
        HeaderByte4 = 0x47, // G
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
    private enum BonusEffectByte
    {
        Off,
        On,
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
        Off,
        KeyframeNext,
        KeyframePrevious,
        KeyframeFirst,
    }

    public enum StrobeSpeedByte
    {
        Off,
        Slow,
        Medium,
        Fast,
        Fastest,
    }

    private enum PauseByte
    {
        Unpaused,
        Paused,
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
        NoCue = 0,
        Menu = 10,
        Score = 20,
        Intro = 30,
        CoolLoop = 60,
        WarmLoop = 70,
        CoolManual = 80,
        WarmManual = 90,
        Dischord = 100,
        Stomp = 110,
        Default = 120,
        Harmony = 130,
        Frenzy = 140,
        Silhouettes = 150,
        SilhouettesSpotlight = 160,
        Searchlights = 170,
        Sweep = 180,
        BlackoutFast = 190,
        BlackoutSlow = 200,
        BlackoutSpotlight = 210,
        FlareSlow = 220,
        FlareFast = 230,
        BigRockEnding = 240,
    }
    private enum PostProcessingByte
    {
        Default = 0,

        // Basic effects
        Bloom = 4,
        Bright = 14,
        Contrast = 24,
        Mirror = 34,
        PhotoNegative = 44,
        Posterize = 54,

        // Color filters/effects
        BlackAndWhite = 64,
        SepiaTone = 74,
        SilverTone = 84,
        ChoppyBlackAndWhite = 94,
        PhotoNegativeRedAndBlack = 104,
        PolarizedBlackAndWhite = 114,
        PolarizedRedAndBlue = 124,
        DesaturatedRed = 134,
        DesaturatedBlue = 144,
        ContrastRed = 154,
        ContrastGreen = 164,
        ContrastBlue = 174,

        // Grainy
        GrainyFilm = 184,
        GrainyChromaticAbberation = 194,
        // Scanlines
        Scanlines = 204,
        ScanlinesBlackAndWhite = 214,
        ScanlinesBlue = 224,
        ScanlinesSecurity = 234,

        // Trails
        Trails = 244,
        TrailsLong = 252,
        TrailsDesaturated = 253,
        TrailsFlickery = 254,
        TrailsSpacey = 255,
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
