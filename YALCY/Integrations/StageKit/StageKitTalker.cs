using System;
using System.Collections.Generic;
using YALCY.Udp;
using YALCY.Usb;
using YALCY.Views.Components;

namespace YALCY.Integrations.StageKit;

public class StageKitTalker
{
    public enum CommandId
    {
        FogOn = 0x01,
        FogOff = 0x02,

        StrobeSlow = 0x03,
        StrobeMedium = 0x04,
        StrobeFast = 0x05,
        StrobeFastest = 0x06,
        StrobeOff = 0x07,

        BlueLeds = 0x20,
        GreenLeds = 0x40,
        YellowLeds = 0x60,
        RedLeds = 0x80,

        DisableAll = 0xFF
    };

    private static StageKitLightingCue? _currentLightingCue;
    public static StageKitLightingCue? PreviousLightingCue;
    private bool _isEnabled;

    //cues that are outide of songs
    public StageKitTalker()
    {
        var startCue = CreateCue(UdpIntake.CueByte.NoCue);
        _currentLightingCue = startCue;
        PreviousLightingCue = startCue;
    }

    //private static byte _currentStrobeSpeed = (byte)UdpIntake.StrobeSpeedByte.Off;
    //private static byte _currentFogState = (byte)UdpIntake.FogStateByte.Off;

    //this should be on song start as, venue calculations are done in the song start
    private static readonly Dictionary<UdpIntake.CueByte, Func<StageKitLightingCue>> _cueFactories = new()
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

    internal static StageKitLightingCue? CreateCue(UdpIntake.CueByte cue)
    {
        return _cueFactories.TryGetValue(cue, out var factory) ? factory() : null;
    }

    private static void CueChange(byte cueByte)
    {
        var cue = CreateCue((UdpIntake.CueByte)cueByte);
        if (cue == null)
        {
            Console.WriteLine($"Cue {cueByte} not found in dictionary.");
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
            (byte)UdpIntake.CueByte.Strobe_Off => CommandId.StrobeOff,
            (byte)UdpIntake.CueByte.Strobe_Slow => CommandId.StrobeSlow,
            (byte)UdpIntake.CueByte.Strobe_Medium => CommandId.StrobeMedium,
            (byte)UdpIntake.CueByte.Strobe_Fast => CommandId.StrobeFast,
            (byte)UdpIntake.CueByte.Strobe_Fastest => CommandId.StrobeFastest,
            _ => CommandId.StrobeOff
        };
        UsbDeviceMonitor.SendReport(strobeSetting, 0x00);
    }

    private static void FogChange(bool activeFogState)
    {
        var fogSetting = activeFogState switch
        {
            false => CommandId.FogOff,
            true => CommandId.FogOn,
        };
        UsbDeviceMonitor.SendReport(fogSetting, 0x00);
    }

    public void EnableStageKitTalker(bool isEnabled)
    {
        if (_isEnabled == isEnabled)
        {
            return;
        }

        _isEnabled = isEnabled;
        if (isEnabled)
        {
            UdpIntake.OnLightingCue += CueChange;
            UdpIntake.OnStrobeState += StrobeChange;
            UdpIntake.OnFogState += FogChange;
            StatusFooter.UpdateStatus("StageKit", IntegrationStatus.Connected);
        }
        else
        {
            UdpIntake.OnLightingCue -= CueChange;
            UdpIntake.OnStrobeState -= StrobeChange;
            UdpIntake.OnFogState -= FogChange;
            StatusFooter.UpdateStatus("StageKit", IntegrationStatus.Off);

          // CueChange((byte)UdpIntake.CueByte.NoCue); Don't do this, other protocols might still be on
        }
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
