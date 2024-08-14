using System;

namespace YALCY.Udp;

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
        if ((byte)fogStateByte == UdpIntake._previousBuffer[(int)ByteIndexName.FogState]) return;
        OnFogState?.Invoke(fogStateByte);
    }

    private static void HandleStrobeByte(StrobeSpeedByte strobeSpeedByte)
    {
        if ((byte)strobeSpeedByte == UdpIntake._previousBuffer[(int)ByteIndexName.StrobeState]) return;
        OnStrobeState?.Invoke(strobeSpeedByte);
    }

    private static void HandleLightingCueByte(CueByte cueByte)
    {
        if ((byte)cueByte == UdpIntake._previousBuffer[(byte)ByteIndexName.LightingCue]) return;
        OnLightingCue?.Invoke(cueByte);
    }
    private static void HandleVocalHarmonyByte(VocalHarmonyBytes vocalHarmonyByte)
    {
        if ((byte)vocalHarmonyByte == UdpIntake._previousBuffer[(int)ByteIndexName.VocalsNote]) return;
        OnVocalHarmony?.Invoke(vocalHarmonyByte);
    }

    private static void HandleBeatByte(BeatByte beatByte)
    {
        if ((byte)beatByte == UdpIntake._previousBuffer[(int)ByteIndexName.Beat]) return;
        OnBeat?.Invoke(beatByte);
    }

    private static void HandleKeyFrameCueEByte(KeyFrameByte keyFrameCueEByte)
    {
        if ((byte)keyFrameCueEByte == UdpIntake._previousBuffer[(int)ByteIndexName.Keyframe]) return;
        OnKeyFrame?.Invoke(keyFrameCueEByte);
    }

    private static void HandleDrumByte(DrumNotesByte drumByte)
    {
        if ((byte)drumByte == UdpIntake._previousBuffer[(int)ByteIndexName.DrumsNotes]) return;
        OnDrum?.Invoke(drumByte);
    }
}
