using System;
using System.Collections.Generic;
using System.Diagnostics;
using YALCY.Integrations.StageKit;
using YALCY.Udp;
using YALCY.Usb;

namespace YALCY.Integrations.AutoGen;

/// <summary>
/// Generates strobe commands from gameplay data (double bass, star power, cues, BonusFX, crash on downbeat)
/// for songs whose venue track has no (or no usable) strobe events. Runs on every UDP packet, on the receive thread.
/// Output goes through the normal Stage Kit command path, so every output (Hue, DMX, Stage Kit, ...) reacts to it.
/// A strobe that comes from the chart always wins.
/// </summary>
public sealed class AutoStrobeGenerator
{
    private const byte SceneGameplay = 2;
    private const byte ScenePractice = 5;
    private const byte PausedValue = 2;
    private const byte KickBit = 1 << 0;
    private const byte GreenCymbalBit = 1 << 7;
    private const byte BeatMeasure = 1;
    private const long PacketGapResendMs = 1000;
    private const long CrashDownbeatWindowMs = 80;

    private readonly UdpIntake _udpIntake;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private volatile AutoStrobeSettingsData _settings = new();

    // Song state
    private byte _previousScene;
    private bool _chartHasStrobes;

    // Previous packet values (for edge detection)
    private byte _previousDrums;
    private byte _previousBeat;
    private byte _previousChartStrobe = (byte)UdpIntake.CueByte.Strobe_Off;
    private bool _previousBonus;
    private readonly HashSet<int> _previousStarPowerActive = new();
    private long _lastPacketMs = -1;

    // Trigger state
    private long _lastKickMs = long.MinValue / 2;
    private int _kickRun;
    private long _starBurstUntilMs;
    private bool _anyStarPowerActive;
    private long _bonusUntilMs;
    private long _lastCrashMs = long.MinValue / 2;
    private long _lastMeasureMs = long.MinValue / 2;
    private long _crashUntilMs;

    // Output state
    private bool _autoActive;
    private StageKitTalker.CommandId _lastSent = StageKitTalker.CommandId.StrobeOff;
    private string _lastStatus = string.Empty;

    public AutoStrobeGenerator(UdpIntake udpIntake)
    {
        _udpIntake = udpIntake;
        _udpIntake.PacketProcessed += OnPacketProcessed;
    }

    /// <summary>Raised (on the receive thread) when the human-readable status changes.</summary>
    public event Action<string>? StatusChanged;

    public AutoStrobeSettingsData Settings
    {
        get => _settings;
        set => _settings = value.Normalized();
    }

    private void OnPacketProcessed(byte[] _)
    {
        try
        {
            Process();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Auto strobe error: {ex.Message}");
        }
    }

    private void Process()
    {
        var settings = _settings;
        var now = _clock.ElapsedMilliseconds;
        var resend = _lastPacketMs >= 0 && now - _lastPacketMs > PacketGapResendMs;
        _lastPacketMs = now;

        var scene = _udpIntake.CurrentScene.Value;
        var inSong = scene is SceneGameplay or ScenePractice;
        var paused = _udpIntake.Paused.Value == PausedValue;

        if (inSong && _previousScene is not (SceneGameplay or ScenePractice))
        {
            ResetSongState();
        }

        _previousScene = scene;

        var chartStrobe = _udpIntake.StrobeState.Value;
        var chartStrobeActive = IsStrobeOn(chartStrobe);
        if (chartStrobeActive)
        {
            _chartHasStrobes = true;
        }

        if (chartStrobe != _previousChartStrobe)
        {
            // The chart strobe changed: its StrobeOn/StrobeOff command may have overwritten ours.
            resend = true;
            _previousChartStrobe = chartStrobe;
        }

        var bpm = UdpIntake.BeatsPerMinute.Value;
        var drums = _udpIntake.CurrentDrumNotes.Value;
        var beat = _udpIntake.Beat.Value;
        var bonus = _udpIntake.BonusEffect.Value;

        // --- Double bass ------------------------------------------------------------------------------
        var kickOnset = (drums & KickBit) != 0 && (_previousDrums & KickBit) == 0;
        var maxGap = settings.DoubleBassMaxGapMs > 0 ? settings.DoubleBassMaxGapMs : AutoKickGapMs(bpm);
        if (kickOnset)
        {
            _kickRun = now - _lastKickMs <= maxGap ? _kickRun + 1 : 1;
            _lastKickMs = now;
        }

        var doubleBass = settings.DoubleBassEnabled
                         && _kickRun >= settings.DoubleBassMinKicks
                         && now - _lastKickMs <= Math.Max(maxGap, settings.DoubleBassReleaseMs)
                         && (settings.DoubleBassMinBpm <= 0 || bpm >= settings.DoubleBassMinBpm);

        // --- Star power --------------------------------------------------------------------------------
        var starPower = _udpIntake.GetPlayerStarPowerSnapshot();
        _anyStarPowerActive = false;
        for (var i = 0; i < starPower.Count; i++)
        {
            if (!starPower[i].IsActive)
            {
                _previousStarPowerActive.Remove(i);
                continue;
            }

            _anyStarPowerActive = true;
            if (_previousStarPowerActive.Add(i))
            {
                _starBurstUntilMs = now + settings.StarPowerBurstMs;
            }
        }

        var starPowerTrigger = settings.StarPowerEnabled &&
                               (settings.StarPowerWhileActive ? _anyStarPowerActive : now < _starBurstUntilMs);

        // --- Cues ---------------------------------------------------------------------------------------
        var cue = (UdpIntake.CueByte)_udpIntake.LightingCue.Value;
        var cueTrigger = cue switch
        {
            UdpIntake.CueByte.Frenzy => settings.CueFrenzy,
            UdpIntake.CueByte.BigRockEnding => settings.CueBigRockEnding,
            UdpIntake.CueByte.Flare_Fast => settings.CueFlareFast,
            UdpIntake.CueByte.Flare_Slow => settings.CueFlareSlow,
            UdpIntake.CueByte.Stomp => settings.CueStomp,
            UdpIntake.CueByte.Dischord => settings.CueDischord,
            _ => false
        };

        // --- BonusFX ------------------------------------------------------------------------------------
        if (bonus && !_previousBonus)
        {
            _bonusUntilMs = now + settings.BonusFxBurstMs;
        }

        var bonusTrigger = settings.BonusFxEnabled && now < _bonusUntilMs;

        // --- Crash on downbeat --------------------------------------------------------------------------
        var crashOnset = (drums & GreenCymbalBit) != 0 && (_previousDrums & GreenCymbalBit) == 0;
        var measureOnset = beat == BeatMeasure && _previousBeat != BeatMeasure;
        if (crashOnset)
        {
            _lastCrashMs = now;
        }

        if (measureOnset)
        {
            _lastMeasureMs = now;
        }

        if ((crashOnset || measureOnset) && Math.Abs(_lastCrashMs - _lastMeasureMs) <= CrashDownbeatWindowMs)
        {
            _crashUntilMs = now + settings.CrashFlashMs;
        }

        var crashTrigger = settings.CrashDownbeatEnabled && now < _crashUntilMs;

        _previousDrums = drums;
        _previousBeat = beat;
        _previousBonus = bonus;

        // --- Gate ---------------------------------------------------------------------------------------
        var autoGenVenue = _udpIntake.AutoGen.Value;
        var gateOpen = settings.Mode switch
        {
            AutoStrobeModes.GeneratedOnly => autoGenVenue,
            AutoStrobeModes.GeneratedOrNoChartStrobes => autoGenVenue || !_chartHasStrobes,
            AutoStrobeModes.Always => true,
            _ => false
        };

        string? reason = null;
        if (inSong && !paused && gateOpen)
        {
            reason = doubleBass ? "double bass"
                : starPowerTrigger ? "star power"
                : cueTrigger ? $"cue {cue}"
                : bonusTrigger ? "BonusFX"
                : crashTrigger ? "crash on downbeat"
                : null;
        }

        // --- Output -------------------------------------------------------------------------------------
        string status;
        if (chartStrobeActive)
        {
            // The chart strobe is running; it owns the strobe. Forget ours so we resend once it ends.
            _autoActive = false;
            status = "Chart strobe active (auto strobe waits)";
        }
        else if (reason != null)
        {
            var command = SpeedToCommand(settings.Speed, bpm);
            if (!_autoActive || command != _lastSent || resend)
            {
                UsbDeviceMonitor.SendReport(command, 0x00);
                _autoActive = true;
                _lastSent = command;
            }

            status = $"Strobing: {reason}";
        }
        else
        {
            if (_autoActive)
            {
                UsbDeviceMonitor.SendReport(StageKitTalker.CommandId.StrobeOff, 0x00);
                _autoActive = false;
                _lastSent = StageKitTalker.CommandId.StrobeOff;
            }

            status = settings.Mode == AutoStrobeModes.Off ? "Off"
                : !inSong ? "Waiting for a song"
                : paused ? "Paused"
                : !gateOpen ? (autoGenVenue ? "Inactive for this song" : "Inactive: authored venue" + (_chartHasStrobes ? " with chart strobes" : ""))
                : "Armed";
        }

        if (status != _lastStatus)
        {
            _lastStatus = status;
            StatusChanged?.Invoke(status);
        }
    }

    private void ResetSongState()
    {
        _chartHasStrobes = false;
        _kickRun = 0;
        _starBurstUntilMs = 0;
        _bonusUntilMs = 0;
        _crashUntilMs = 0;
        _previousStarPowerActive.Clear();
    }

    private static bool IsStrobeOn(byte strobeState) =>
        strobeState is (byte)UdpIntake.CueByte.Strobe_Fastest
            or (byte)UdpIntake.CueByte.Strobe_Fast
            or (byte)UdpIntake.CueByte.Strobe_Medium
            or (byte)UdpIntake.CueByte.Strobe_Slow;

    /// <summary>Kicks count as "double bass" when they come at least as dense as 16th notes (with some slack).</summary>
    private static long AutoKickGapMs(float bpm)
    {
        if (bpm <= 0)
        {
            return 150;
        }

        var sixteenthMs = 60000.0 / bpm / 4.0;
        return (long)Math.Clamp(sixteenthMs * 1.35, 70, 220);
    }

    /// <summary>
    /// "By BPM" picks the subdivision whose flash phase is closest to ~90 ms at the current tempo,
    /// so the strobe stays locked to the beat grid but doesn't get unwatchably fast or slow.
    /// </summary>
    private static StageKitTalker.CommandId SpeedToCommand(string speed, float bpm)
    {
        switch (speed)
        {
            case AutoStrobeSpeeds.Slow: return StageKitTalker.CommandId.StrobeSlow;
            case AutoStrobeSpeeds.Medium: return StageKitTalker.CommandId.StrobeMedium;
            case AutoStrobeSpeeds.Fast: return StageKitTalker.CommandId.StrobeFast;
            case AutoStrobeSpeeds.Fastest: return StageKitTalker.CommandId.StrobeFastest;
        }

        var effectiveBpm = bpm > 0 ? bpm : 120;
        var candidates = new (int NoteValue, StageKitTalker.CommandId Command)[]
        {
            (16, StageKitTalker.CommandId.StrobeSlow),
            (24, StageKitTalker.CommandId.StrobeMedium),
            (32, StageKitTalker.CommandId.StrobeFast),
            (64, StageKitTalker.CommandId.StrobeFastest),
        };

        var best = candidates[0];
        var bestDistance = double.MaxValue;
        foreach (var candidate in candidates)
        {
            var phaseMs = 60000.0 / effectiveBpm * 4 / candidate.NoteValue;
            var distance = Math.Abs(phaseMs - 90);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best.Command;
    }
}
