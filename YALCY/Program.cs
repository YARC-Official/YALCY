using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;

namespace YALCY;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        Diagnostics.AppLog.Initialize();
        try { BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); }
        catch (Exception ex)
        {
            Diagnostics.AppLog.Write(Diagnostics.LogLevel.Error, "Application", ex.ToString());
            throw;
        }
        finally
        {
            Diagnostics.AppLog.Write(Diagnostics.LogLevel.Information, "Application", "YALCY stopped.");
            Diagnostics.AppLog.Shutdown();
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
