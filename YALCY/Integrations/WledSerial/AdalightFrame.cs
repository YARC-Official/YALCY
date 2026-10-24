using YALCY.Lighting;
using System;

namespace YALCY.Integrations.WledSerial;

internal sealed class AdalightFrame
{
    private readonly byte[] _banks = new byte[4];
    private int _strobe;
    public void Apply(LightingCommand command, byte mask)
    {
        var bank = command switch { LightingCommand.BlueLeds => 0, LightingCommand.RedLeds => 1,
            LightingCommand.GreenLeds => 2, LightingCommand.YellowLeds => 3, _ => -1 };
        if (bank >= 0) _banks[bank] = mask;
        else if (command == LightingCommand.DisableAll) { Array.Clear(_banks); _strobe = 0; }
        else if (command == LightingCommand.StrobeOff) _strobe = 0;
        else if (ManualStrobeFlasher.TryGetSpeed(command, out var speed)) _strobe = speed;
    }

    // Stock WLED Adalight: 'Ada', big-endian (LED count - 1), hi XOR lo XOR 0x55, then RGB triples.
    public byte[] Render(WledSerialOptions options, bool blackout, long milliseconds, float bpm, string? testColor = null)
    {
        var packet = new byte[6 + options.LedCount * 3];
        packet[0] = (byte)'A'; packet[1] = (byte)'d'; packet[2] = (byte)'a';
        var count = options.LedCount - 1;
        packet[3] = (byte)(count >> 8); packet[4] = (byte)count;
        packet[5] = (byte)(packet[3] ^ packet[4] ^ 0x55);
        RenderRgb(options, blackout, milliseconds, bpm, testColor).CopyTo(packet, 6);
        return packet;
    }

    internal byte[] RenderRgb(WledSerialOptions options, bool blackout, long milliseconds, float bpm, string? testColor = null)
    {
        var packet = new byte[options.LedCount * 3];
        if (blackout) return packet;
        var intensity = (byte)(255 * options.Brightness / 100);
        if (testColor != null)
        {
            for (var i = 0; i < options.LedCount; i++) Paint(packet, i, testColor, intensity);
            return packet;
        }
        foreach (var map in options.Mappings)
        {
            var parts = map.Assignment.Split(' ');
            var bank = parts[0] switch { "Blue" => 0, "Red" => 1, "Green" => 2, "Yellow" => 3, _ => -1 };
            for (var offset = 0; offset < map.Count; offset++)
            {
                var lit = map.Assignment == "Strobe"
                    ? _strobe > 0 && milliseconds / ManualStrobeFlasher.CalculateIntervalMs(_strobe, bpm) % 2 == 0
                    : bank >= 0 && (_banks[bank] & (1 << (parts[1] == "bank" ? offset % 8 : int.Parse(parts[1]) - 1))) != 0;
                if (lit) Paint(packet, map.Start - 1 + offset, bank < 0 ? "White" : parts[0], intensity);
            }
        }
        return packet;
    }

    private static void Paint(byte[] packet, int led, string color, byte value)
    {
        var p = led * 3;
        packet[p] = color is "Red" or "Yellow" or "White" ? value : (byte)0;
        packet[p + 1] = color is "Green" or "Yellow" or "White" ? value : (byte)0;
        packet[p + 2] = color is "Blue" or "White" ? value : (byte)0;
    }

    public static int FrameIntervalMs(WledSerialOptions options) => Math.Max(34,
        (int)Math.Ceiling((6 + options.LedCount * 3) * 10.0 * 1000 / options.Baud / 0.8));
}
