using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using YALCY;

namespace YALCY.CLI;

class Program
{
    private static HeadlessHost? _host;
    private static readonly ManualResetEventSlim _shutdownEvent = new(false);

    static async Task<int> Main(string[] args)
    {
        Console.WriteLine($"YALCY CLI v{GetVersion()}");
        Console.WriteLine("Headless lighting integration for YARG");
        Console.WriteLine();

        // Set up Ctrl+C and SIGTERM handling
        Console.CancelKeyPress += OnCancelKeyPress;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        try
        {
            _host = new HeadlessHost();
            await _host.InitializeAsync();

            Console.WriteLine();
            Console.WriteLine("YALCY is running. Press Ctrl+C to exit.");
            Console.WriteLine();

            // Wait for shutdown signal
            _shutdownEvent.Wait();

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true; // Prevent immediate termination
        Console.WriteLine();
        Console.WriteLine("Received shutdown signal...");
        TriggerShutdown();
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        TriggerShutdown();
    }

    private static void TriggerShutdown()
    {
        if (_host != null)
        {
            _host.ShutdownAsync().GetAwaiter().GetResult();
            _host = null;
        }
        _shutdownEvent.Set();
    }

    private static string GetVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version != null)
        {
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
        return "1.0.0";
    }
}
