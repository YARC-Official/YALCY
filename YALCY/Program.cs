using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;

using System.Linq;
using YALCY.Diagnostics;

namespace YALCY;

sealed class Program
{
    public static string[] StartupArgs { get; private set; } = Array.Empty<string>();

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        StartupArgs = args ?? Array.Empty<string>();
        AppLog.Initialize();

        bool allowMultiple = StartupArgs.Contains("--multi-instance", StringComparer.OrdinalIgnoreCase);
        if (!allowMultiple && !SingleInstanceManager.TryAcquire())
        {
            AppLog.Write(LogLevel.Information, "Application", $"Another instance of YALCY is already running. Forwarding arguments ({string.Join(" ", StartupArgs)}) and exiting.");
            SingleInstanceManager.NotifyExistingInstance(StartupArgs);
            AppLog.Shutdown();
            return;
        }

        try
        {
            if (!allowMultiple)
            {
                SingleInstanceManager.StartIpcServer(receivedArgs =>
                {
                    CommandLineHandler.ExecuteArguments(receivedArgs);
                });
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            AppLog.Write(LogLevel.Error, "Application", ex.ToString());
            throw;
        }
        finally
        {
            if (!allowMultiple)
            {
                SingleInstanceManager.Release();
            }

            AppLog.Write(LogLevel.Information, "Application", "YALCY stopped.");
            AppLog.Shutdown();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
