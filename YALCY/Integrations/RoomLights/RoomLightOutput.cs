using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YALCY.Lighting;

namespace YALCY.Integrations.RoomLights;

public sealed class RoomLightOutput
{
    private readonly RoomLightKind _kind;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly object _gate = new();
    private readonly byte[] _banks = new byte[4];
    private CancellationTokenSource? _stop;
    private Task? _worker;
    private GoveeSocket? _govee;
    private string? _testAddress;
    private RoomColor _testColor;
    private long _testUntil;
    internal Func<RoomLightDevice, IRoomLightConnection>? ConnectionFactory { get; set; }
    public event Action<string, string, bool>? DeviceStatusChanged;
    public RoomLightOutput(RoomLightKind kind) => _kind = kind;

    public async Task EnableAsync(bool enabled, RoomLightOptions options)
    {
        await _lifecycle.WaitAsync();
        try
        {
            LightingOutput.CommandReceived -= OnCue;
            _stop?.Cancel();
            if (_worker != null) await _worker.ConfigureAwait(false);
            _worker = null; _stop?.Dispose(); _stop = null; _govee?.Dispose(); _govee = null;
            lock (_gate) { Array.Clear(_banks); _testAddress = null; }
            if (!enabled) return;
            var snapshot = options.Snapshot(_kind);
            if (_kind == RoomLightKind.Govee && ConnectionFactory == null) _govee = new();
            _stop = new();
            var token = _stop.Token;
            LightingOutput.CommandReceived += OnCue;
            _worker = Task.WhenAll(snapshot.Devices.Select(device => Task.Run(() => RunDeviceAsync(device, snapshot, token))));
        }
        finally { _lifecycle.Release(); }
    }

    public void Test(string address, string color)
    {
        if (_worker == null || _worker.IsCompleted || LightingOutput.IsOutputSuppressed) return;
        lock (_gate)
        {
            _testAddress = address.Trim(); _testUntil = Environment.TickCount64 + 2000;
            _testColor = color switch
            {
                "Red" => new(255, 0, 0, 100), "Green" => new(0, 255, 0, 100),
                "Blue" => new(0, 0, 255, 100), "Yellow" => new(255, 255, 0, 100), _ => new(255, 255, 255, 100)
            };
        }
    }
    private void OnCue(LightingCommand command, byte mask)
    {
        lock (_gate)
        {
            var index = command switch { LightingCommand.BlueLeds => 0, LightingCommand.RedLeds => 1,
                LightingCommand.GreenLeds => 2, LightingCommand.YellowLeds => 3, _ => -1 };
            if (index >= 0) _banks[index] = mask;
            if (command == LightingCommand.DisableAll) { Array.Clear(_banks); _testAddress = null; }
        }
    }
    internal RoomColor Desired(RoomLightDevice device, int brightness)
    {
        lock (_gate)
        {
            if (LightingOutput.IsOutputSuppressed) return RoomColor.Off;
            if (_testAddress == device.Address && Environment.TickCount64 < _testUntil) return _testColor with { Brightness = brightness };
            int r = 0, g = 0, b = 0;
            for (var i = 0; i < 4; i++)
            {
                var active = _banks[i] != 0;
                if (device.Assignment.StartsWith("Light ", StringComparison.Ordinal))
                    active = (_banks[i] & (1 << (int.Parse(device.Assignment[6..]) - 1))) != 0;
                else if (device.Assignment != "All colors")
                    active &= device.Assignment == new[] { "Blue bank", "Red bank", "Green bank", "Yellow bank" }[i];
                if (!active) continue;
                if (i is 1 or 3) r = 255;
                if (i is 2 or 3) g = 255;
                if (i == 0) b = 255;
            }
            return new((byte)r, (byte)g, (byte)b, brightness);
        }
    }
    private async Task RunDeviceAsync(RoomLightDevice device, RoomLightOptions options, CancellationToken token)
    {
        IRoomLightConnection? connection = null;
        var captured = false;
        try
        {
            connection = ConnectionFactory?.Invoke(device) ?? new RoomLightConnection(_kind, device, _govee);
            while (!captured)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    await connection.CaptureAsync(token);
                    captured = true;
                    Report(device, _kind == RoomLightKind.Govee ? "Device responded; sending colors (UDP changes are not acknowledged)." : "Device responded; output ready.");
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    Report(device, $"No usable state received. {ErrorDetail(ex)} Retrying in 3 seconds.", true);
                    await Task.Delay(3000, token);
                }
            }
            RoomColor? last = null;
            long next = 0, refresh = 0, retry = 0;
            var failed = false;
            var lastAttemptOff = false;
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(25));
            do
            {
                var desired = Desired(device, options.Brightness);
                var now = Environment.TickCount64;
                if ((now < retry && (!desired.IsOff || lastAttemptOff)) || (now < next && !desired.IsOff) || (last == desired && now < refresh)) continue;
                try
                {
                    lastAttemptOff = desired.IsOff;
                    await connection.SendAsync(desired, token);
                    last = desired;
                    next = Environment.TickCount64 + 1000 / options.UpdatesPerSecond;
                    refresh = Environment.TickCount64 + 2000;
                    if (failed) { Report(device, "Output resumed."); failed = false; }
                }
                catch (Exception ex) when (!token.IsCancellationRequested)
                {
                    failed = true;
                    Report(device, $"Output failed. {ErrorDetail(ex)} Retrying in 3 seconds.", true);
                    retry = Environment.TickCount64 + 3000;
                    last = null;
                }
            } while (await timer.WaitForNextTickAsync(token));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception) { Report(device, "Could not start this device. Disable output, check its configuration, and try again.", true); }
        finally
        {
            if (connection != null)
            {
                if (captured)
                {
                    try
                    {
                        using var timeout = new CancellationTokenSource(3000);
                        if (options.RestoreOnDisable && !LightingOutput.IsOutputSuppressed) await connection.RestoreAsync(timeout.Token);
                        else await connection.SendAsync(RoomColor.Off, timeout.Token);
                        Report(device, options.RestoreOnDisable && !LightingOutput.IsOutputSuppressed ? "Stopped; restoration sent." : "Stopped; lights off requested.");
                    }
                    catch { Report(device, "Stopped; final restoration/off request failed. Check the light manually.", true); }
                    finally
                    {
                        // Use a fresh deadline: restoration may have exhausted its own timeout.
                        if (LightingOutput.IsOutputSuppressed)
                        {
                            try
                            {
                                using var blackoutTimeout = new CancellationTokenSource(2000);
                                await connection.SendAsync(RoomColor.Off, blackoutTimeout.Token);
                            }
                            catch { Report(device, "Final blackout request failed. Check the light manually.", true); }
                        }
                    }
                }
                connection.Dispose();
            }
        }
    }
    private static string ErrorDetail(Exception error) => error is System.IO.IOException ? error.Message :
        "Check the device address, network, local control, and pairing.";
    private void Report(RoomLightDevice device, string status, bool error = false) => DeviceStatusChanged?.Invoke(device.Address, status, error);
}
