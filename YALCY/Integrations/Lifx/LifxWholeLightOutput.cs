using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YALCY.Integrations.Lifx;

// One desired state per light, rather than a backlog of song frames. Transport
// runs only on the worker; Submit never waits for a socket or a rate-limit delay.
internal sealed class LifxWholeLightOutput : IAsyncDisposable
{
    private sealed class Entry
    {
        public required LifxLanDeviceModel Device;
        public LifxHsbk Color;
        public uint Transition;
        public int Rate;
        public LifxHsbk? Sent;
        public double LastAttempt = double.NegativeInfinity;
        public double LastRefresh = double.NegativeInfinity;
        public double RetryAfter;
        public bool Urgent;
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<LifxLanDeviceModel, LifxHsbk, uint, bool> _send;
    private readonly Action<LifxLanDeviceModel, Exception> _onError;
    private readonly Func<bool> _suppressed;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    private bool _stopped;

    internal LifxWholeLightOutput(Action<LifxLanDeviceModel, LifxHsbk, uint, bool> send,
        Func<bool> suppressed, Action<LifxLanDeviceModel, Exception> onError, bool startWorker = true)
    {
        _send = send;
        _suppressed = suppressed;
        _onError = onError;
        _worker = startWorker ? Task.Run(RunAsync) : Task.CompletedTask;
    }

    internal void Submit(LifxLanDeviceModel device, LifxHsbk color, uint transition, int rate, bool urgent = false)
    {
        lock (_gate)
        {
            if (_stopped) return;
            if (!_entries.TryGetValue(device.Serial, out var entry))
                _entries[device.Serial] = entry = new Entry { Device = device };
            if (!ReferenceEquals(entry.Device, device)) entry.Sent = null;
            entry.Device = device;
            entry.Color = color;
            entry.Transition = transition;
            entry.Rate = Math.Clamp(rate, 1, 60);
            entry.Urgent |= urgent;
        }
    }

    internal void RetainDevices(IEnumerable<string> serials)
    {
        var keep = new HashSet<string>(serials, StringComparer.OrdinalIgnoreCase);
        lock (_gate)
            foreach (var serial in _entries.Keys.Where(key => !keep.Contains(key)).ToArray())
                _entries.Remove(serial);
    }

    private async Task RunAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(10));
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token).ConfigureAwait(false))
                FlushDue(elapsed.Elapsed.TotalSeconds);
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    // The worker is the sole caller in production. Explicit time makes the
    // scheduler's rate limiting, retry, and refresh behavior deterministic to test.
    internal void FlushDue(double now)
    {
        string[] serials;
        lock (_gate) serials = _entries.Keys.ToArray();
        foreach (var serial in serials)
        {
            Entry entry;
            LifxLanDeviceModel device;
            LifxHsbk color;
            uint transition;
            bool refresh;
            lock (_gate)
            {
                if (_stopped || !_entries.TryGetValue(serial, out entry!)) continue;
                color = _suppressed() ? entry.Color.WithBrightness(0) : entry.Color;
                var urgent = entry.Urgent || (_suppressed() && entry.Sent is { IsOn: true });
                // A new blackout gets one immediate attempt; a persistent socket
                // failure must still back off instead of retrying every tick.
                if (now < entry.RetryAfter && !entry.Urgent) continue;
                refresh = now - entry.LastRefresh >= 1;
                if (!urgent && (now < entry.RetryAfter || now - entry.LastAttempt < 1.0 / entry.Rate
                    || (entry.Sent == color && !refresh))) continue;
                device = entry.Device;
                transition = color.IsOn ? entry.Transition : 0;
                entry.Urgent = false;
                entry.LastAttempt = now;
            }

            try
            {
                // Recheck after taking the snapshot: blackout can arrive while
                // another device is being sent. The transport checks again too.
                if (_suppressed()) { color = color.WithBrightness(0); transition = 0; }
                _send(device, color, transition, refresh);
                lock (_gate)
                {
                    entry.Sent = color;
                    entry.RetryAfter = 0;
                    if (refresh) entry.LastRefresh = now;
                }
            }
            catch (Exception ex)
            {
                lock (_gate) entry.RetryAfter = now + 1;
                _onError(device, ex);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate) { _stopped = true; _entries.Clear(); }
        _stop.Cancel();
        await _worker.ConfigureAwait(false);
        _stop.Dispose();
    }
}
