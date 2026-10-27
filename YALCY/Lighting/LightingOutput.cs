using System;
using System.Threading;
using YALCY.Diagnostics;

namespace YALCY.Lighting;

// Shared command stream. Output enable flags control subscriptions, never cue generation.
public static class LightingOutput
{
    private static int _outputSuppressed;
    public static event Action<LightingCommand, byte>? CommandReceived;
    internal static bool IsOutputSuppressed => Volatile.Read(ref _outputSuppressed) != 0;

    public static void Publish(LightingCommand commandId, byte parameter)
    {
        if (IsOutputSuppressed)
        {
            return;
        }

        DispatchReport(commandId, parameter);
    }

    internal static void EnterSafetyBlackout()
    {
        if (Interlocked.Exchange(ref _outputSuppressed, 1) != 0)
        {
            return;
        }

        DispatchReport(LightingCommand.DisableAll, 0);
    }

    internal static void ExitSafetyBlackout()
    {
        Interlocked.Exchange(ref _outputSuppressed, 0);
    }

    public static void Preview(LightingCommand commandId, byte parameter)
    {
        // Hardware previews must obey the same blackout gate as game cues.
        Publish(commandId, parameter);
    }

    private static void DispatchReport(LightingCommand commandId, byte parameter)
    {
        var subscribers = CommandReceived?.GetInvocationList();
        if (subscribers != null)
        {
            foreach (var subscriber in subscribers)
            {
                try
                {
                    ((Action<LightingCommand, byte>)subscriber)(commandId, parameter);
                }
                catch (Exception ex)
                {
                    AppLog.Write(LogLevel.Error, "Lighting", $"Lighting command subscriber failed: {ex.Message}");
                }
            }
        }

    }
}
