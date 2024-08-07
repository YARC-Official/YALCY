using System;

namespace YALCY;

public partial class UdpIntake
{
    public static Action<BeatByte>? OnBeat { get; set; }
    public static Action<KeyFrameByte>? OnKeyFrame { get; set; }
    public static Action<DrumNotesByte>? OnDrum { get; set; }
    public static Action<VocalHarmonyBytes>? OnVocalHarmony { get; set; }
    public static Action<CueByte>? OnLightingCue { get; set; }
    public static Action<FogStateByte>? OnFogState { get; set; }
    public static Action<StrobeSpeedByte>? OnStrobeState { get; set; }

    private static void HandleFogByte(FogStateByte fogStateByte)
    {
        if ((byte)fogStateByte == _previousBuffer[(int)ByteIndexName.FogState]) return;
        OnFogState?.Invoke(fogStateByte);
    }

    private static void HandleStrobeByte(StrobeSpeedByte strobeSpeedByte)
    {
        if ((byte)strobeSpeedByte == _previousBuffer[(int)ByteIndexName.StrobeState]) return;
        OnStrobeState?.Invoke(strobeSpeedByte);
    }

    private static void HandleLightingCueByte(CueByte cueByte)
    {
        if ((byte)cueByte == _previousBuffer[(byte)ByteIndexName.LightingCue]) return;
        OnLightingCue?.Invoke(cueByte);
    }
    private static void HandleVocalHarmonyByte(VocalHarmonyBytes vocalHarmonyByte)
    {
        if ((byte)vocalHarmonyByte == _previousBuffer[(int)ByteIndexName.VocalsNote]) return;
        OnVocalHarmony?.Invoke(vocalHarmonyByte);
    }

    private static void HandleBeatByte(BeatByte beatByte)
    {
        if ((byte)beatByte == _previousBuffer[(int)ByteIndexName.Beat]) return;
        OnBeat?.Invoke(beatByte);
    }

    private static void HandleKeyFrameCueEByte(KeyFrameByte keyFrameCueEByte)
    {
        if ((byte)keyFrameCueEByte == _previousBuffer[(int)ByteIndexName.Keyframe]) return;
        OnKeyFrame?.Invoke(keyFrameCueEByte);
    }

    private static void HandleDrumByte(DrumNotesByte drumByte)
    {
        if ((byte)drumByte == _previousBuffer[(int)ByteIndexName.DrumsNotes]) return;
        OnDrum?.Invoke(drumByte);
    }
}
