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
        { UdpIntake.CueByte.Warm_Manual, new ManualWarm() },
        { UdpIntake.CueByte.Cool_Manual, new ManualCool() },
        { UdpIntake.CueByte.Dischord, new Dischord() },
        { UdpIntake.CueByte.Stomp, new Stomp() },
        { UdpIntake.CueByte.Default, new Default() },
        { UdpIntake.CueByte.Warm_Automatic, new LoopWarm() },
        { UdpIntake.CueByte.Cool_Automatic, new LoopCool() },
        { UdpIntake.CueByte.BigRockEnding, new BigRockEnding() },
        { UdpIntake.CueByte.Searchlights, new SearchLight() },
        { UdpIntake.CueByte.Frenzy, new Frenzy() },
        { UdpIntake.CueByte.Sweep, new Sweep() },
        { UdpIntake.CueByte.Harmony, new Harmony() },
        { UdpIntake.CueByte.Flare_Slow, new FlareSlow() },
        { UdpIntake.CueByte.Flare_Fast, new FlareFast() },
        { UdpIntake.CueByte.Silhouettes_Spotlight, new SilhouetteSpot() },
        { UdpIntake.CueByte.Silhouettes, new Silhouettes() },
        { UdpIntake.CueByte.Blackout_Spotlight, new Blackout() },
        { UdpIntake.CueByte.Blackout_Slow, new Blackout() },
        { UdpIntake.CueByte.Blackout_Fast, new Blackout() },
        { UdpIntake.CueByte.Intro, new Intro() }
    };

    private static void CueChange(byte cueByte)
    {
        // Try to get the new cue from the dictionary
        if (!_cueDictionary.TryGetValue((UdpIntake.CueByte)cueByte, out var cue))
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
}
