using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;
using ReactiveUI;

namespace YALCY.Integrations.WledSerial;

[JsonObject(MemberSerialization.OptIn)]
public sealed class WledSerialOptions : ReactiveObject
{
    private string _port = "";
    private int _baud = 115200, _count = 32, _brightness = 100;
    [JsonProperty] public string Port { get => _port; set => this.RaiseAndSetIfChanged(ref _port, value ?? ""); }
    [JsonProperty] public int Baud { get => _baud; set => this.RaiseAndSetIfChanged(ref _baud, value); }
    [JsonProperty] public int LedCount { get => _count; set => this.RaiseAndSetIfChanged(ref _count, value); }
    [JsonProperty] public int Brightness { get => _brightness; set => this.RaiseAndSetIfChanged(ref _brightness, value); }
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)] public ObservableCollection<WledSerialMapping> Mappings { get; set; } = new()
    {
        new() { Start = 1, Count = 8, Assignment = "Blue bank" },
        new() { Start = 9, Count = 8, Assignment = "Red bank" },
        new() { Start = 17, Count = 8, Assignment = "Green bank" },
        new() { Start = 25, Count = 8, Assignment = "Yellow bank" }
    };
    public static readonly int[] BaudRates = { 115200, 230400, 460800, 500000, 576000, 921600, 1000000, 1500000 };

    public WledSerialOptions Snapshot()
    {
        if (string.IsNullOrWhiteSpace(Port)) throw new ArgumentException("Select a serial port, such as COM3 or /dev/ttyUSB0.");
        if (!BaudRates.Contains(Baud)) throw new ArgumentException("Choose a supported baud rate and set the same rate in WLED Sync Interfaces.");
        if (LedCount is < 1 or > 2048) throw new ArgumentException("LED count must be between 1 and 2048.");
        if (Brightness is < 0 or > 100) throw new ArgumentException("Brightness must be between 0 and 100 percent.");
        var occupied = new bool[LedCount];
        foreach (var map in Mappings ?? throw new ArgumentException("Add at least one mapping."))
        {
            if (map.Start < 1 || map.Count < 1 || (long)map.Start + map.Count - 1 > LedCount)
                throw new ArgumentException($"Mapping starting at LED {map.Start} extends outside the configured {LedCount} LEDs.");
            if (!WledSerialMapping.Assignments.Contains(map.Assignment)) throw new ArgumentException("Choose a valid Stage Kit assignment.");
            for (var i = map.Start - 1; i < map.Start - 1 + map.Count; i++)
            {
                if (occupied[i]) throw new ArgumentException($"Mappings overlap at LED {i + 1}. Use separate LED ranges.");
                occupied[i] = true;
            }
        }
        return new WledSerialOptions { Port = Port.Trim(), Baud = Baud, LedCount = LedCount, Brightness = Brightness,
            Mappings = new(Mappings.Select(m => new WledSerialMapping { Start = m.Start, Count = m.Count, Assignment = m.Assignment })) };
    }
}

[JsonObject(MemberSerialization.OptIn)]
public sealed class WledSerialMapping : ReactiveObject
{
    private int _start = 1, _count = 1;
    private string _assignment = "Unassigned";
    [JsonProperty] public int Start { get => _start; set => this.RaiseAndSetIfChanged(ref _start, value); }
    [JsonProperty] public int Count { get => _count; set => this.RaiseAndSetIfChanged(ref _count, value); }
    [JsonProperty] public string Assignment { get => _assignment; set => this.RaiseAndSetIfChanged(ref _assignment, value); }
    public static IReadOnlyList<string> Assignments { get; } = new[] { "Unassigned", "Strobe" }
        .Concat(new[] { "Blue", "Red", "Green", "Yellow" }.SelectMany(c => new[] { c + " bank" }.Concat(Enumerable.Range(1, 8).Select(i => $"{c} {i}")))).ToArray();
    public IReadOnlyList<string> AvailableAssignments => Assignments;
}
