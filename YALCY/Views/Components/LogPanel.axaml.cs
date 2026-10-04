using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using YALCY.Diagnostics;

namespace YALCY.Views.Components;

public partial class LogPanel : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly ObservableCollection<string> _sources = new() { "All sources" };
    private long _revision = -1;
    private string[] _visible = Array.Empty<string>();
    private LogEntry[]? _pausedEntries;

    public LogPanel()
    {
        InitializeComponent();
        LogLines.ItemTemplate = new FuncDataTemplate<LogEntry>((entry, _) =>
        {
            if (entry == null) return new TextBlock();
            var (foreground, background) = entry.Level switch
            {
                LogLevel.Error => ("#991B1B", "#FEE2E2"),
                LogLevel.Warning => ("#713F12", "#FEF08A"),
                LogLevel.Information => ("#1E40AF", "#DBEAFE"),
                _ => ("#374151", "#E5E7EB")
            };
            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"),
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new Border
                    {
                        Background = Brush.Parse(background),
                        CornerRadius = new Avalonia.CornerRadius(3),
                        Padding = new Avalonia.Thickness(5, 1),
                        Child = new TextBlock
                        {
                            Text = $"[{entry.Level}]",
                            Foreground = Brush.Parse(foreground),
                            FontWeight = FontWeight.SemiBold
                        }
                    },
                    new TextBlock
                    {
                        Text = $"[{entry.Source}] {entry.Message}",
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            };
        });
        SourceFilter.ItemsSource = _sources;
        SourceFilter.SelectedIndex = 0;
        LevelFilter.ItemsSource = new[] { "Debug", "Information", "Warning", "Error" };
        LevelFilter.SelectedIndex = 1;
        SourceFilter.SelectionChanged += (_, _) => _revision = -1;
        LevelFilter.SelectionChanged += (_, _) => _revision = -1;
        AutoScroll.IsCheckedChanged += (_, _) =>
        {
            _pausedEntries = AutoScroll.IsChecked == true ? null : AppLog.Snapshot().Entries;
            _revision = -1;
        };
        DebugLogging.IsCheckedChanged += (_, _) =>
        {
            AppLog.DebugEnabled = DebugLogging.IsChecked == true;
            if (AppLog.DebugEnabled) LevelFilter.SelectedIndex = 0;
            AppLog.Write(LogLevel.Information, "Application", $"Debug logging {(AppLog.DebugEnabled ? "enabled" : "disabled")}.");
        };
        _timer.Tick += (_, _) => Refresh();
        AttachedToVisualTree += (_, _) => { Refresh(); _timer.Start(); };
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    private void Refresh()
    {
        var snapshot = AppLog.Snapshot();
        if (_revision == snapshot.Revision) return;
        _revision = snapshot.Revision;
        foreach (var source in snapshot.Entries.Select(e => e.Source).Distinct().OrderBy(s => s))
            if (!_sources.Contains(source)) _sources.Add(source);
        var warnings = snapshot.Entries.Count(e => e.Level == LogLevel.Warning);
        var errors = snapshot.Entries.Count(e => e.Level == LogLevel.Error);
        Summary.Text = $"Log · {warnings} warnings · {errors} errors" + (snapshot.FileError != null ? " · File logging unavailable" : "");
        ToolTip.SetTip(Summary, snapshot.FileError ?? $"Log files: {AppLog.DirectoryPath}");
        var sourceFilter = SourceFilter.SelectedItem as string;
        var entries = (_pausedEntries ?? snapshot.Entries).Where(e => (int)e.Level >= LevelFilter.SelectedIndex &&
                (sourceFilter == "All sources" || e.Source == sourceFilter))
            .ToArray();
        var visible = entries.Select(e => e.ToString()).ToArray();
        if (_visible.SequenceEqual(visible)) return;
        _visible = visible;
        // Preserve the reader's position while auto-scroll is paused.
        var viewer = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(LogLines).OfType<ScrollViewer>().FirstOrDefault();
        var offset = viewer?.Offset;
        LogLines.ItemsSource = entries;
        if (AutoScroll.IsChecked == true && _visible.Length > 0)
            LogLines.ScrollIntoView(entries[^1]);
        else if (viewer != null && offset.HasValue)
            Dispatcher.UIThread.Post(() => viewer.Offset = offset.Value, DispatcherPriority.Loaded);
    }

    private void ResizeLog(object? sender, VectorEventArgs e)
    {
        LogBody.Height = Math.Clamp(LogBody.Height - e.Vector.Y, LogBody.MinHeight, LogBody.MaxHeight);
        e.Handled = true;
    }

    private async void CopyLog(object? sender, RoutedEventArgs e)
    {
        try
        {
            Refresh();
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null) throw new InvalidOperationException("Clipboard unavailable.");
            await clipboard.SetTextAsync(string.Join(Environment.NewLine, _visible));
        }
        catch (Exception ex) { AppLog.Write(LogLevel.Error, "Application", $"Could not copy log: {ex.Message}"); }
    }

    private async void SaveLog(object? sender, RoutedEventArgs e)
    {
        try
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage == null || !storage.CanSave) throw new InvalidOperationException("File saving unavailable.");
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save visible log entries", SuggestedFileName = $"yalcy-{DateTime.Now:yyyyMMdd-HHmmss}.log",
                DefaultExtension = "log"
            });
            if (file == null) return;
            using (file)
            {
                Refresh();
                await using var stream = await file.OpenWriteAsync();
                stream.SetLength(0);
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(string.Join(Environment.NewLine, _visible));
            }
        }
        catch (Exception ex) { AppLog.Write(LogLevel.Error, "Application", $"Could not save log: {ex.Message}"); }
    }

    private void ClearLog(object? sender, RoutedEventArgs e)
    {
        AppLog.Clear();
        if (_pausedEntries != null) _pausedEntries = Array.Empty<LogEntry>();
        Refresh();
    }
}
