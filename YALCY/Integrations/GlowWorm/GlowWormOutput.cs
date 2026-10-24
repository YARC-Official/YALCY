using YALCY.Lighting;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using YALCY.Diagnostics;
using YALCY.Integrations.WledSerial;
using YALCY.Udp;
using YALCY.Views.Components;

namespace YALCY.Integrations.GlowWorm;

public sealed class GlowWormOutput
{
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly object _gate = new();
    private readonly AdalightFrame _frame = new();
    private CancellationTokenSource? _cancellation;
    private Task? _worker;
    private string? _testColor;
    private long _testUntil;
    public event Action<string>? StatusChanged;
    public async Task EnableAsync(bool enabled, GlowWormOptions options)
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            _cancellation?.Cancel();
            if (_worker != null) await _worker.ConfigureAwait(false);
            _cancellation?.Dispose(); _cancellation = null; _worker = null;
            LightingOutput.CommandReceived -= OnCue;
            lock (_gate) { _frame.Apply(LightingCommand.DisableAll, 0); _testColor = null; }
            if (!enabled) { Report("Disabled; UDP output stopped.", IntegrationStatus.Off); return; }
            GlowWormOptions snapshot;
            try { snapshot = options.Snapshot(); }
            catch (Exception ex) { Report(ex.Message, IntegrationStatus.Error); return; }
            _cancellation = new();
            var token = _cancellation.Token;
            LightingOutput.CommandReceived += OnCue;
            _worker = Task.Run(() => RunAsync(snapshot, token));
        }
        finally { _lifecycle.Release(); }
    }

    public void TestColor(string color)
    {
        if (_worker == null || _worker.IsCompleted) { Report("Enable Glow Worm before testing.", IntegrationStatus.Error); return; }
        if (color is not ("Red" or "Green" or "Blue" or "Yellow" or "White")) return;
        lock (_gate) { _testColor = color; _testUntil = Environment.TickCount64 + 2000; }
        AppLog.Write(LogLevel.Information, "Glow Worm", $"Testing {color} for two seconds.");
    }

    private void OnCue(LightingCommand command, byte parameter)
    {
        lock (_gate) { _frame.Apply(command, parameter); if (command == LightingCommand.DisableAll) _testColor = null; }
    }

    private async Task RunAsync(GlowWormOptions options, CancellationToken token)
    {
        var brightness = (byte)(255 * options.Lighting.Brightness / 100);
        // Brightness is applied by the firmware from the packet header, exactly once.
        options.Lighting.Brightness = 100;
        using var controlCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var control = options.AutoControl ? ControlAsync(options, brightness, controlCancellation.Token) : Task.CompletedTask;
        try
        {
            while (!token.IsCancellationRequested)
            {
                UdpClient? client = null;
                try
                {
                    var address = IPAddress.Parse(options.Address);
                    client = new UdpClient(address.AddressFamily);
                    client.Connect(address, options.Port);
                    Report($"Sending RGB to {options.Address}:{options.Port}, {options.Lighting.LedCount} LEDs at up to {options.Fps} FPS. UDP reception is not acknowledged.", IntegrationStatus.Connected);
                    while (!token.IsCancellationRequested)
                    {
                        var timer = Stopwatch.StartNew();
                        byte[] rgb;
                        lock (_gate)
                        {
                            if (Environment.TickCount64 >= _testUntil) _testColor = null;
                            rgb = _frame.RenderRgb(options.Lighting, LightingOutput.IsOutputSuppressed, Environment.TickCount64, UdpIntake.BeatsPerMinute.Value, _testColor);
                        }
                        await SendAsync(client, rgb, brightness, token);
                        await Task.Delay(Math.Max(1, (int)Math.Ceiling(1000.0 / options.Fps) - (int)timer.ElapsedMilliseconds), token);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception ex) { Report($"UDP output to {options.Address}:{options.Port} failed: {ex.GetType().Name}: {ex.Message} Check the controller address and network. Retrying in 3 seconds.", IntegrationStatus.Error); }
                finally
                {
                    if (client != null)
                    {
                        if (token.IsCancellationRequested)
                        {
                            try
                            {
                                using var timeout = new CancellationTokenSource(1000);
                                await SendAsync(client, new byte[options.Lighting.LedCount * 3], brightness, timeout.Token);
                            }
                            catch (Exception ex) { AppLog.Write(LogLevel.Warning, "Glow Worm", $"Final blackout failed: {ex.Message}"); }
                        }
                        client.Dispose();
                    }
                }
                await Task.Delay(3000, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally { controlCancellation.Cancel(); await control; }
    }

    private static async Task SendAsync(UdpClient client, byte[] rgb, byte brightness, CancellationToken token)
    {
        var first = true;
        foreach (var packet in GlowWormProtocol.Encode(rgb, brightness))
        {
            if (!first) await Task.Delay(1, token);
            await client.SendAsync(packet.AsMemory(), token);
            first = false;
        }
    }

    private static async Task ControlAsync(GlowWormOptions options, byte brightness, CancellationToken token)
    {
        // Keep control retries separate so a slow HTTP endpoint never stalls RGB frames.
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var response = await http.GetAsync(GlowWormProtocol.ControlUri(options, brightness), token);
                    response.EnsureSuccessStatusCode();
                    AppLog.Write(LogLevel.Information, "Glow Worm", "Controller accepted ON / Bias light mode.");
                    return;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception ex) { AppLog.Write(LogLevel.Warning, "Glow Worm", $"Bias light request to {options.Address}:{options.HttpPort} failed: {ex.Message} UDP streaming continues; retrying control in 5 seconds. Select Bias light manually if HTTP control is unavailable."); }
                await Task.Delay(5000, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private void Report(string message, IntegrationStatus status)
    {
        StatusFooter.UpdateStatus("Glow Worm", status, message);
        StatusChanged?.Invoke(message);
    }
}
