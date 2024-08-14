using System;
using System.Collections.Generic;
using YALCY.Usb;

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

    //cues that are outide of songs
    public StageKitTalker()
    {
        _cueDictionary.TryGetValue(Udp.UdpIntake.CueByte.NoCue, out var startCue);
        _currentLightingCue = startCue;
        PreviousLightingCue = startCue;
    }

    //private static byte _currentStrobeSpeed = (byte)UdpIntake.StrobeSpeedByte.Off;
    //private static byte _currentFogState = (byte)UdpIntake.FogStateByte.Off;

    //this should be on song start as, venue calculations are done in the song start
    private static readonly Dictionary<Udp.UdpIntake.CueByte, StageKitLightingCue> _cueDictionary = new()
    {
        { Udp.UdpIntake.CueByte.NoCue, new NoCue() },
        { Udp.UdpIntake.CueByte.Menu, new MenuLighting() },
        { Udp.UdpIntake.CueByte.Score, new ScoreLighting() },
        { Udp.UdpIntake.CueByte.WarmManual, new ManualWarm() },
        { Udp.UdpIntake.CueByte.CoolManual, new ManualCool() },
        { Udp.UdpIntake.CueByte.Dischord, new Dischord() },
        { Udp.UdpIntake.CueByte.Stomp, new Stomp() },
        { Udp.UdpIntake.CueByte.Default, new Default() },
        { Udp.UdpIntake.CueByte.WarmLoop, new LoopWarm() },
        { Udp.UdpIntake.CueByte.CoolLoop, new LoopCool() },
        { Udp.UdpIntake.CueByte.BigRockEnding, new BigRockEnding() },
        { Udp.UdpIntake.CueByte.Searchlights, new SearchLight() },
        { Udp.UdpIntake.CueByte.Frenzy, new Frenzy() },
        { Udp.UdpIntake.CueByte.Sweep, new Sweep() },
        { Udp.UdpIntake.CueByte.Harmony, new Harmony() },
        { Udp.UdpIntake.CueByte.FlareSlow, new FlareSlow() },
        { Udp.UdpIntake.CueByte.FlareFast, new FlareFast() },
        { Udp.UdpIntake.CueByte.SilhouettesSpotlight, new SilhouetteSpot() },
        { Udp.UdpIntake.CueByte.Silhouettes, new Silhouettes() },
        { Udp.UdpIntake.CueByte.BlackoutSpotlight, new Blackout() },
        { Udp.UdpIntake.CueByte.BlackoutSlow, new Blackout() },
        { Udp.UdpIntake.CueByte.BlackoutFast, new Blackout() },
        { Udp.UdpIntake.CueByte.Intro, new Intro() }
    };

    private static void CueChange(Udp.UdpIntake.CueByte cueByte)
    {
        // Try to get the new cue from the dictionary
        if (!_cueDictionary.TryGetValue(cueByte, out var cue))
        {
            Console.WriteLine($"Cue {cueByte} not found in dictionary.");
            return;
        }

        foreach (var primitive in _currentLightingCue?.CuePrimitives)
        {
            primitive.KillSelf();
        }

        _currentLightingCue.KillSelf();
        // Set and enable the new lighting cue
        PreviousLightingCue = _currentLightingCue;
        _currentLightingCue = cue;
        _currentLightingCue.Enable();
    }


    private static void StrobeChange(Udp.UdpIntake.StrobeSpeedByte activeStrobeSpeed)
    {
        var strobeSetting = activeStrobeSpeed switch
        {
            Udp.UdpIntake.StrobeSpeedByte.Off => CommandId.StrobeOff,
            Udp.UdpIntake.StrobeSpeedByte.Slow => CommandId.StrobeSlow,
            Udp.UdpIntake.StrobeSpeedByte.Medium => CommandId.StrobeMedium,
            Udp.UdpIntake.StrobeSpeedByte.Fast => CommandId.StrobeFast,
            Udp.UdpIntake.StrobeSpeedByte.Fastest => CommandId.StrobeFastest,
            _ => CommandId.StrobeOff
        };
        UsbDeviceMonitor.SendReport(strobeSetting, 0x00);
    }

    private static void FogChange(Udp.UdpIntake.FogStateByte activeFogState)
    {
        var fogSetting = activeFogState switch
        {
            Udp.UdpIntake.FogStateByte.Off => CommandId.FogOff,
            Udp.UdpIntake.FogStateByte.On => CommandId.FogOn,
            _ => CommandId.FogOff
        };
        UsbDeviceMonitor.SendReport(fogSetting, 0x00);
    }

    public void EnableStageKitTalker(bool isEnabled)
    {
        if (isEnabled)
        {
            Udp.UdpIntake.OnLightingCue += CueChange;
            Udp.UdpIntake.OnStrobeState += StrobeChange;
            Udp.UdpIntake.OnFogState += FogChange;
        }
        else
        {
            Udp.UdpIntake.OnLightingCue -= CueChange;
            Udp.UdpIntake.OnStrobeState -= StrobeChange;
            Udp.UdpIntake.OnFogState -= FogChange;

            CueChange(Udp.UdpIntake.CueByte.NoCue);
        }
    }
}
