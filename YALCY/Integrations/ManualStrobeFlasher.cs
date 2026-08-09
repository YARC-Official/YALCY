using System;
using System.Threading;
using System.Threading.Tasks;
using YALCY.Integrations.StageKit;

namespace YALCY.Integrations;

public sealed class ManualStrobeFlasher : IDisposable
{
    private readonly object _lock = new();
    private readonly Action<Exception>? _onError;
    private CancellationTokenSource? _cancellationTokenSource;

    public ManualStrobeFlasher(Action<Exception>? onError = null)
    {
        _onError = onError;
    }

    public void Start(
        StageKitTalker.CommandId commandId,
        float bpm,
        Func<bool, CancellationToken, Task> setFlashState)
    {
        if (!TryGetSpeed(commandId, out var speed))
        {
            Stop(setFlashState);
            return;
        }

        var intervalMs = CalculateIntervalMs(speed, bpm);
        Stop();

        var cancellationTokenSource = new CancellationTokenSource();
        lock (_lock)
        {
            _cancellationTokenSource = cancellationTokenSource;
        }

        _ = RunFlashLoopAsync(cancellationTokenSource, intervalMs, setFlashState);
    }

    public void Stop(Func<bool, CancellationToken, Task>? setFlashState = null)
    {
        CancellationTokenSource? cancellationTokenSource;
        lock (_lock)
        {
            cancellationTokenSource = _cancellationTokenSource;
            _cancellationTokenSource = null;
        }

        cancellationTokenSource?.Cancel();

        if (setFlashState != null)
        {
            _ = SetStateSafelyAsync(false, CancellationToken.None, setFlashState);
        }
    }

    public static bool TryGetSpeed(StageKitTalker.CommandId commandId, out int speed)
    {
        speed = commandId switch
        {
            StageKitTalker.CommandId.StrobeSlow => 1,
            StageKitTalker.CommandId.StrobeMedium => 2,
            StageKitTalker.CommandId.StrobeFast => 3,
            StageKitTalker.CommandId.StrobeFastest => 4,
            _ => 0
        };

        return speed > 0;
    }

    public static int CalculateIntervalMs(int speed, float bpm)
    {
        var noteValue = speed switch
        {
            1 => 16,
            2 => 24,
            3 => 32,
            4 => 64,
            _ => 16
        };

        if (bpm <= 0)
        {
            return 100;
        }

        return Math.Max(15, (int)(60000.0 / bpm * 4 / noteValue));
    }

    private async Task RunFlashLoopAsync(
        CancellationTokenSource cancellationTokenSource,
        int intervalMs,
        Func<bool, CancellationToken, Task> setFlashState)
    {
        var token = cancellationTokenSource.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                await setFlashState(true, token);
                await Task.Delay(intervalMs, token);

                await setFlashState(false, token);
                await Task.Delay(intervalMs, token);
            }
        }
        catch (OperationCanceledException)
        {
            // The strobe was stopped or restarted.
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
        }
        finally
        {
            lock (_lock)
            {
                if (ReferenceEquals(_cancellationTokenSource, cancellationTokenSource))
                {
                    _cancellationTokenSource = null;
                }
            }

            cancellationTokenSource.Dispose();
        }
    }

    private async Task SetStateSafelyAsync(
        bool isOn,
        CancellationToken cancellationToken,
        Func<bool, CancellationToken, Task> setFlashState)
    {
        try
        {
            await setFlashState(isOn, cancellationToken);
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
