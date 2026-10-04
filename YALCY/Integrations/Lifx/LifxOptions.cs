using System;
using ReactiveUI;
using Newtonsoft.Json;

namespace YALCY.Integrations.Lifx;

[JsonObject(MemberSerialization.OptIn)]
public sealed class LifxOptions : ReactiveObject
{
    private bool _wholeLightMode;
    private int _brightness = 100, _transitionMs = 150, _discoverySeconds = 2, _rediscoverySeconds = 30;
    private bool _autoDiscover = true;
    private string _includeLights = "";
    [JsonProperty] public bool WholeLightMode { get => _wholeLightMode; set => this.RaiseAndSetIfChanged(ref _wholeLightMode, value); }
    [JsonProperty] public int Brightness { get => _brightness; set => this.RaiseAndSetIfChanged(ref _brightness, Math.Clamp(value, 0, 100)); }
    [JsonProperty] public int TransitionMs { get => _transitionMs; set => this.RaiseAndSetIfChanged(ref _transitionMs, Math.Clamp(value, 0, 5000)); }
    [JsonProperty] public int DiscoverySeconds { get => _discoverySeconds; set => this.RaiseAndSetIfChanged(ref _discoverySeconds, Math.Clamp(value, 1, 10)); }
    [JsonProperty] public int RediscoverySeconds { get => _rediscoverySeconds; set => this.RaiseAndSetIfChanged(ref _rediscoverySeconds, Math.Clamp(value, 10, 3600)); }
    [JsonProperty] public bool AutoDiscover { get => _autoDiscover; set => this.RaiseAndSetIfChanged(ref _autoDiscover, value); }
    [JsonProperty] public string IncludeLights { get => _includeLights; set => this.RaiseAndSetIfChanged(ref _includeLights, value ?? ""); }
}

internal static class LifxPalette
{
    public static readonly string[] Names = { "Red", "Orange", "Yellow", "Green", "Cyan", "Blue", "Purple", "White" };
    public static LifxHsbk Color(string name, int brightness)
    {
        var degrees = name switch { "Orange" => 30, "Yellow" => 60, "Green" => 120, "Cyan" => 180, "Blue" => 240, "Purple" => 280, _ => 0 };
        return new LifxHsbk((ushort)Math.Round(degrees * 65535.0 / 360),
            name == "White" ? (ushort)0 : ushort.MaxValue,
            (ushort)Math.Round(65535.0 * Math.Clamp(brightness, 0, 100) / 100), 3500);
    }
    public static string SongColor(byte cue, byte section) => cue switch
    {
        1 or 14 or 17 or 18 => "Purple", 2 or 12 or 13 => "Yellow",
        3 or 5 or 11 => "Blue", 6 or 26 => "Orange", 16 => "Green", 25 => "Cyan",
        4 or 7 or 15 or 19 or 30 or 31 => "White",
        _ => section == 2 ? "Yellow" : section == 5 ? "Blue" : "White"
    };
}
