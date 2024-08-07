using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YALCY;

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
        _cueDictionary.TryGetValue(UdpIntake.CueByte.NoCue, out var startCue);
        _currentLightingCue = startCue;
        PreviousLightingCue = startCue;
    }

    //private static byte _currentStrobeSpeed = (byte)UdpIntake.StrobeSpeedByte.Off;
    //private static byte _currentFogState = (byte)UdpIntake.FogStateByte.Off;

    //this should be on song start as, venue calculations are done in the song start
    private static readonly Dictionary<UdpIntake.CueByte, StageKitLightingCue> _cueDictionary = new()
    {
        { UdpIntake.CueByte.NoCue, new NoCue() },
        { UdpIntake.CueByte.Menu, new MenuLighting() },
        { UdpIntake.CueByte.Score, new ScoreLighting() },
        { UdpIntake.CueByte.WarmManual, new ManualWarm() },
        { UdpIntake.CueByte.CoolManual, new ManualCool() },
        { UdpIntake.CueByte.Dischord, new Dischord() },
        { UdpIntake.CueByte.Stomp, new Stomp() },
        { UdpIntake.CueByte.Default, new Default() },
        { UdpIntake.CueByte.WarmLoop, new LoopWarm() },
        { UdpIntake.CueByte.CoolLoop, new LoopCool() },
        { UdpIntake.CueByte.BigRockEnding, new BigRockEnding() },
        { UdpIntake.CueByte.Searchlights, new SearchLight() },
        { UdpIntake.CueByte.Frenzy, new Frenzy() },
        { UdpIntake.CueByte.Sweep, new Sweep() },
        { UdpIntake.CueByte.Harmony, new Harmony() },
        { UdpIntake.CueByte.FlareSlow, new FlareSlow() },
        { UdpIntake.CueByte.FlareFast, new FlareFast() },
        { UdpIntake.CueByte.SilhouettesSpotlight, new SilhouetteSpot() },
        { UdpIntake.CueByte.Silhouettes, new Silhouettes() },
        { UdpIntake.CueByte.BlackoutSpotlight, new Blackout() },
        { UdpIntake.CueByte.BlackoutSlow, new Blackout() },
        { UdpIntake.CueByte.BlackoutFast, new Blackout() },
        { UdpIntake.CueByte.Intro, new Intro() }
    };

    private static void CueChange(UdpIntake.CueByte cueByte)
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


    private static void StrobeChange(UdpIntake.StrobeSpeedByte activeStrobeSpeed)
    {
        var strobeSetting = activeStrobeSpeed switch
        {
            UdpIntake.StrobeSpeedByte.Off => CommandId.StrobeOff,
            UdpIntake.StrobeSpeedByte.Slow => CommandId.StrobeSlow,
            UdpIntake.StrobeSpeedByte.Medium => CommandId.StrobeMedium,
            UdpIntake.StrobeSpeedByte.Fast => CommandId.StrobeFast,
            UdpIntake.StrobeSpeedByte.Fastest => CommandId.StrobeFastest,
            _ => CommandId.StrobeOff
        };
        USBDeviceMonitor.SendReport(strobeSetting, 0x00);
    }

    private static void FogChange(UdpIntake.FogStateByte activeFogState)
    {
        var fogSetting = activeFogState switch
        {
            UdpIntake.FogStateByte.Off => CommandId.FogOff,
            UdpIntake.FogStateByte.On => CommandId.FogOn,
            _ => CommandId.FogOff
        };
        USBDeviceMonitor.SendReport(fogSetting, 0x00);
    }

    public void EnableStageKitTalker(bool isEnabled)
    {
        if (isEnabled)
        {
            UdpIntake.OnLightingCue += CueChange;
            UdpIntake.OnStrobeState += StrobeChange;
            UdpIntake.OnFogState += FogChange;
        }
        else
        {
            UdpIntake.OnLightingCue -= CueChange;
            UdpIntake.OnStrobeState -= StrobeChange;
            UdpIntake.OnFogState -= FogChange;

            CueChange(UdpIntake.CueByte.NoCue);
        }
    }
}
