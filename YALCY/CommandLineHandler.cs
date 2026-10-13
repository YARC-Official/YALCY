using System;
using System.Collections.Generic;
using Avalonia.Threading;
using YALCY.Diagnostics;
using YALCY.Views.Tabs;

namespace YALCY;

/// <summary>
/// Handles command line arguments for YALCY both on cold start and when forwarded
/// from secondary instances.
/// </summary>
public static class CommandLineHandler
{
    /// <summary>
    /// Parses raw command line arguments into normalized lowercase flag strings.
    /// Handles '--arg', '-arg', '/arg', and standalone '--' followed by 'arg'.
    /// </summary>
    public static HashSet<string> ParseFlags(IEnumerable<string> rawArgs)
    {
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (rawArgs == null) return flags;

        using var enumerator = rawArgs.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var current = enumerator.Current?.Trim();
            if (string.IsNullOrWhiteSpace(current)) continue;

            if (current == "--" || current == "-")
            {
                if (enumerator.MoveNext())
                {
                    var next = enumerator.Current?.Trim().TrimStart('-', '/');
                    if (!string.IsNullOrWhiteSpace(next))
                    {
                        flags.Add(Normalize(next));
                    }
                }
                continue;
            }

            var cleaned = current.TrimStart('-', '/');
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                flags.Add(Normalize(cleaned));
            }
        }

        return flags;
    }

    private static string Normalize(string val)
    {
        return val.Trim().Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Returns true if the arguments specify that the main window should start hidden (e.g. in tray or only popups).
    /// </summary>
    public static bool ShouldStartHidden(IEnumerable<string> rawArgs)
    {
        var flags = ParseFlags(rawArgs);
        return flags.Contains("ignore") ||
               flags.Contains("nomain") ||
               flags.Contains("nomainwindow") ||
               flags.Contains("tray") ||
               flags.Contains("hide") ||
               flags.Contains("minimized") ||
               flags.Contains("minimize") ||
               flags.Contains("quiet");
    }

    /// <summary>
    /// Executes the commands specified in the argument list on the Avalonia UI thread.
    /// </summary>
    public static void ExecuteArguments(IEnumerable<string> rawArgs)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var flags = ParseFlags(rawArgs);
                AppLog.Write(LogLevel.Information, "Application", $"Executing CLI arguments: [{string.Join(", ", flags)}]");

                // 1. Exit / Quit
                if (flags.Contains("exit") || flags.Contains("quit") || flags.Contains("kill") || flags.Contains("stop"))
                {
                    App.RequestExit();
                    return;
                }

                // 2. Close Popups
                if (flags.Contains("closepopups") || flags.Contains("closewindows"))
                {
                    YargTabView.CloseAllDetachedWindows();
                }

                // 3. Check for popup opening requests
                bool openedAnyPopup = false;

                // StageKit LED Visualizer popup (--stagekit, --visualizer, --leds)
                if (flags.Contains("stagekit") || flags.Contains("visualizer") || flags.Contains("leds") || flags.Contains("ledvisualizer") || flags.Contains("led"))
                {
                    YargTabView.OpenLedVisualizer();
                    openedAnyPopup = true;
                }

                // StageKit Status / More info popup (--moreinfo, --status, --stagekitstatus)
                if (flags.Contains("moreinfo") || flags.Contains("status") || flags.Contains("stagekitstatus") || flags.Contains("details") || flags.Contains("info"))
                {
                    YargTabView.OpenStageKitStatus();
                    openedAnyPopup = true;
                }

                // Strobe Visualizer popup (--strobe, --strobevisualizer)
                if (flags.Contains("strobe") || flags.Contains("strobevisualizer") || flags.Contains("flasher"))
                {
                    YargTabView.OpenStrobeVisualizer();
                    openedAnyPopup = true;
                }

                // 4. Main window handling
                bool isIgnore = flags.Contains("ignore") || flags.Contains("nomain") || flags.Contains("nomainwindow") || flags.Contains("quiet");
                bool isTray = flags.Contains("tray") || flags.Contains("hide") || flags.Contains("minimized") || flags.Contains("minimize");
                bool isShowMain = flags.Contains("main") || flags.Contains("show") || flags.Contains("restore");

                if (isTray)
                {
                    App.HideMainWindowToTray();
                }
                else if (isShowMain)
                {
                    App.ShowMainWindow();
                }
                else if (isIgnore)
                {
                    // Specifically requested to NOT open or focus the main window.
                    // Keep main window untouched.
                }
                else
                {
                    // If neither ignore nor any popup was opened, default to showing the main window.
                    if (!openedAnyPopup)
                    {
                        App.ShowMainWindow();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Write(LogLevel.Error, "Application", $"Error executing CLI arguments: {ex.Message}");
            }
        });
    }
}
