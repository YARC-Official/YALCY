using YALCY.Diagnostics;
using System;
using System.Collections.Generic;
using YALCY.Udp;

namespace YALCY.Lighting;

public class LightingEngine : IDisposable
{
    private static LightingCue? _currentLightingCue;
    public static LightingCue? PreviousLightingCue;
    private bool _isEnabled;

    public LightingEngine()
    {
        var startCue = CreateCue(UdpIntake.CueByte.NoCue);
        _currentLightingCue = startCue;
        PreviousLightingCue = startCue;
    }

    private static readonly Dictionary<UdpIntake.CueByte, Func<LightingCue>> _cueFactories = new()
    {
        { UdpIntake.CueByte.NoCue, static () => new NoCue() },
        { UdpIntake.CueByte.Menu, static () => new MenuLighting() },
        { UdpIntake.CueByte.Score, static () => new ScoreLighting() },
        { UdpIntake.CueByte.Warm_Manual, static () => new ManualWarm() },
        { UdpIntake.CueByte.Cool_Manual, static () => new ManualCool() },
        { UdpIntake.CueByte.Dischord, static () => new Dischord() },
        { UdpIntake.CueByte.Stomp, static () => new Stomp() },
        { UdpIntake.CueByte.Default, static () => new Default() },
        { UdpIntake.CueByte.Warm_Automatic, static () => new LoopWarm() },
        { UdpIntake.CueByte.Cool_Automatic, static () => new LoopCool() },
        { UdpIntake.CueByte.BigRockEnding, static () => new BigRockEnding() },
        { UdpIntake.CueByte.Searchlights, static () => new SearchLight() },
        { UdpIntake.CueByte.Frenzy, static () => new Frenzy() },
        { UdpIntake.CueByte.Sweep, static () => new Sweep() },
        { UdpIntake.CueByte.Harmony, static () => new Harmony() },
        { UdpIntake.CueByte.Flare_Slow, static () => new FlareSlow() },
        { UdpIntake.CueByte.Flare_Fast, static () => new FlareFast() },
        { UdpIntake.CueByte.Silhouettes_Spotlight, static () => new SilhouetteSpot() },
        { UdpIntake.CueByte.Silhouettes, static () => new Silhouettes() },
        { UdpIntake.CueByte.Blackout_Spotlight, static () => new Blackout() },
        { UdpIntake.CueByte.Blackout_Slow, static () => new Blackout() },
        { UdpIntake.CueByte.Blackout_Fast, static () => new Blackout() },
        { UdpIntake.CueByte.Intro, static () => new Intro() }
    };

    internal static LightingCue? CreateCue(UdpIntake.CueByte cue)
    {
        return _cueFactories.TryGetValue(cue, out var factory) ? factory() : null;
    }

    private static void CueChange(byte cueByte)
    {
        var cue = CreateCue((UdpIntake.CueByte)cueByte);
        if (cue == null)
        {
            AppLog.Write(LogLevel.Warning, "Lighting", $"Cue {cueByte} not found in dictionary.");
            return;
        }

        StopCurrentCue();
        // Set and enable the new lighting cue
        PreviousLightingCue = _currentLightingCue;
        _currentLightingCue = cue;
        _currentLightingCue.Enable();
    }

    private static void StrobeChange(byte activeStrobeSpeed)
    {
        var strobeSetting = activeStrobeSpeed switch
        {
            (byte)UdpIntake.CueByte.Strobe_Off => LightingCommand.StrobeOff,
            (byte)UdpIntake.CueByte.Strobe_Slow => LightingCommand.StrobeSlow,
            (byte)UdpIntake.CueByte.Strobe_Medium => LightingCommand.StrobeMedium,
            (byte)UdpIntake.CueByte.Strobe_Fast => LightingCommand.StrobeFast,
            (byte)UdpIntake.CueByte.Strobe_Fastest => LightingCommand.StrobeFastest,
            _ => LightingCommand.StrobeOff
        };
        LightingOutput.Publish(strobeSetting, 0x00);
    }

    private static void FogChange(bool activeFogState)
    {
        var fogSetting = activeFogState switch
        {
            false => LightingCommand.FogOff,
            true => LightingCommand.FogOn,
        };
        LightingOutput.Publish(fogSetting, 0x00);
    }

    public void Start()
    {
        if (_isEnabled) return;
        _isEnabled = true;
        UdpIntake.OnLightingCue += CueChange;
        UdpIntake.OnStrobeState += StrobeChange;
        UdpIntake.OnFogState += FogChange;
    }

    public void Dispose()
    {
        if (!_isEnabled) return;
        _isEnabled = false;
        UdpIntake.OnLightingCue -= CueChange;
        UdpIntake.OnStrobeState -= StrobeChange;
        UdpIntake.OnFogState -= FogChange;
        SuspendCurrentCue();
    }

    internal void SuspendCurrentCue()
    {
        StopCurrentCue();
        _currentLightingCue = null;
    }

    private static void StopCurrentCue()
    {
        if (_currentLightingCue == null)
        {
            return;
        }

        foreach (var primitive in _currentLightingCue.CuePrimitives)
        {
            primitive.KillSelf();
        }

        _currentLightingCue.KillSelf();
    }
}
