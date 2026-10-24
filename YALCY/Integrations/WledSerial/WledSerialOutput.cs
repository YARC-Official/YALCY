using YALCY.Lighting;
using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using YALCY.Diagnostics;
using YALCY.Udp;
using YALCY.Views.Components;

namespace YALCY.Integrations.WledSerial;

internal interface IWledSerialConnection : IDisposable
{
    void Open();
    int PendingBytes { get; }
    void Write(byte[] packet);
}

internal sealed class WledSerialConnection : IWledSerialConnection
{
    private readonly SerialPort _port;
    public WledSerialConnection(WledSerialOptions options) => _port = new SerialPort(options.Port, options.Baud, Parity.None, 8, StopBits.One)
    { Handshake = Handshake.None, DtrEnable = false, RtsEnable = false, WriteTimeout = 1000 };
    public void Open() => _port.Open();
    public int PendingBytes => _port.BytesToWrite;
    public void Write(byte[] packet) => _port.Write(packet, 0, packet.Length);
    public void Dispose() => _port.Dispose();
}

public sealed class WledSerialOutput
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly AdalightFrame _frame = new();
    private readonly Func<WledSerialOptions, IWledSerialConnection> _connect;
    private CancellationTokenSource? _cancellation;
    private Task? _worker;
    private string? _testColor;
    private long _testUntil;
    public event Action<string>? StatusChanged;
    public WledSerialOutput() : this(options => new WledSerialConnection(options)) { }
    internal WledSerialOutput(Func<WledSerialOptions, IWledSerialConnection> connect) => _connect = connect;

    public async Task EnableAsync(bool enabled, WledSerialOptions options)
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            _cancellation?.Cancel();
            if (_worker != null) await _worker.ConfigureAwait(false);
            _cancellation?.Dispose(); _cancellation = null; _worker = null;
            LightingOutput.CommandReceived -= OnCue;
            lock (_gate) { _frame.Apply(LightingCommand.DisableAll, 0); _testColor = null; }
            if (!enabled) { Report("Disabled; serial port released.", IntegrationStatus.Off); return; }
            WledSerialOptions snapshot;
            try { snapshot = options.Snapshot(); }
            catch (Exception ex) { Report(ex.Message, IntegrationStatus.Error); return; }
            LightingOutput.CommandReceived += OnCue;
            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;
            _worker = Task.Run(() => RunAsync(snapshot, token));
        }
        finally { _lifecycle.Release(); }
    }

    public void TestColor(string color)
    {
        if (_worker == null || _worker.IsCompleted) { Report("Enable WLED Serial before testing a color.", IntegrationStatus.Error); return; }
        if (color is not ("Red" or "Green" or "Blue" or "Yellow" or "White")) return;
        lock (_gate) { _testColor = color; _testUntil = Environment.TickCount64 + 2000; }
        AppLog.Write(LogLevel.Information, "WLED Serial", $"Testing {color} for two seconds, then returning to mapped lighting.");
    }

    private void OnCue(LightingCommand command, byte parameter)
    {
        lock (_gate)
        {
            _frame.Apply(command, parameter);
            if (command == LightingCommand.DisableAll) _testColor = null;
        }
    }

    private async Task RunAsync(WledSerialOptions options, CancellationToken token)
    {
        var interval = AdalightFrame.FrameIntervalMs(options);
        try
        {
            while (!token.IsCancellationRequested)
            {
                IWledSerialConnection? port = null;
                try
                {
                    Report($"Opening {options.Port} at {options.Baud} baud…", IntegrationStatus.Connecting);
                    port = _connect(options); port.Open();
                    // Some USB serial adapters reset the ESP32 when the port opens.
                    await Task.Delay(1500, token);
                    Report($"Serial port open: {options.Port}, {options.Baud} baud, {options.LedCount} LEDs; up to {1000.0 / interval:F1} frames/s. WLED reception is not acknowledged.", IntegrationStatus.Connected);
                    while (!token.IsCancellationRequested)
                    {
                        var timer = Stopwatch.StartNew();
                        if (port.PendingBytes == 0)
                        {
                            byte[] packet;
                            lock (_gate)
                            {
                                if (Environment.TickCount64 >= _testUntil) _testColor = null;
                                packet = _frame.Render(options, LightingOutput.IsOutputSuppressed,
                                    Environment.TickCount64, UdpIntake.BeatsPerMinute.Value, _testColor);
                            }
                            port.Write(packet);
                        }
                        await Task.Delay(Math.Max(1, interval - (int)timer.ElapsedMilliseconds), token);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    Report($"Serial output on {options.Port} failed: {ex.GetType().Name}: {ex.Message} Check the USB data cable, driver, port permissions and that no other app has the port open. Retrying in 3 seconds.", IntegrationStatus.Error);
                }
                finally
                {
                    if (port != null)
                    {
                        try
                        {
                            if (token.IsCancellationRequested)
                                port.Write(_frame.Render(options, true, 0, 0));
                        }
                        catch (Exception ex) { AppLog.Write(LogLevel.Warning, "WLED Serial", $"Could not send final blackout: {ex.Message}"); }
                        try { port.Dispose(); }
                        catch (Exception ex) { AppLog.Write(LogLevel.Warning, "WLED Serial", $"Could not release serial port: {ex.Message}"); }
                    }
                }
                await Task.Delay(3000, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private void Report(string text, IntegrationStatus status)
    {
        StatusFooter.UpdateStatus("WLED Serial", status, text);
        StatusChanged?.Invoke(text);
    }
}

