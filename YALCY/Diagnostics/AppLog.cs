using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace YALCY.Diagnostics;

public enum LogLevel { Debug, Information, Warning, Error }

public sealed record LogEntry(DateTimeOffset Timestamp, LogLevel Level, string Source, string Message)
{
    public override string ToString() => $"{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] [{Source}] {Message}";
}

/// <summary>Thread-safe, bounded history shared by desktop and headless integrations.</summary>
public static class AppLog
{
    private static readonly object Gate = new();
    private static readonly Queue<LogEntry> Entries = new();
    private const int Capacity = 2000;
    private const long MaxFileBytes = 2 * 1024 * 1024;
    private static long _revision;
    private static string? _fileError;
    private static volatile bool _debugEnabled;
    private static readonly Channel<LogEntry> Pending = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(Capacity)
    { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private static Task? _writer;
    public static bool DebugEnabled { get => _debugEnabled; set => _debugEnabled = value; }
    public static bool EchoToConsole { get; set; }
    public static string DirectoryPath { get; private set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YALCY", "Logs");
    // Separate files prevent two running instances from competing during rotation.
    public static string FilePath => Path.Combine(DirectoryPath, $"yalcy-{Environment.ProcessId}.log");

    public static (long Revision, LogEntry[] Entries, string? FileError) Snapshot()
    {
        lock (Gate) return (_revision, Entries.ToArray(), _fileError);
    }

    public static void Clear()
    {
        lock (Gate) { Entries.Clear(); _revision++; }
    }

    public static void Write(LogLevel level, string source, string message)
    {
        if (level == LogLevel.Debug && !DebugEnabled) return;
        // Keep an individual malformed packet/exception from consuming the entire history.
        if (message.Length > 8192) message = message[..8192] + " [truncated]";
        var entry = new LogEntry(DateTimeOffset.Now, level, source, message);
        lock (Gate)
        {
            Entries.Enqueue(entry);
            while (Entries.Count > Capacity) Entries.Dequeue();
            _revision++;
            Pending.Writer.TryWrite(entry);
        }
        if (EchoToConsole) Console.WriteLine(entry);
    }

    private static async Task PersistAsync()
    {
        await foreach (var entry in Pending.Reader.ReadAllAsync())
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length >= MaxFileBytes)
                {
                    for (var i = 3; i >= 1; i--)
                    {
                        var previous = i == 1 ? FilePath : FilePath + "." + (i - 1);
                        if (File.Exists(previous)) File.Move(previous, FilePath + "." + i, true);
                    }
                }
                File.AppendAllText(FilePath, entry + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                lock (Gate)
                {
                    _fileError = $"File logging unavailable: {ex.Message}. In-memory logging continues.";
                    _revision++;
                }
                // Drain the bounded queue without further disk attempts after a failure.
                await foreach (var unused in Pending.Reader.ReadAllAsync()) { }
                return;
            }
        }
    }

    public static void Initialize(string? directory = null)
    {
        lock (Gate)
        {
            if (_writer != null) return;
            if (directory != null) DirectoryPath = directory;
            _writer = Task.Run(PersistAsync);
        }
        Write(LogLevel.Information, "Application", $"YALCY {typeof(AppLog).Assembly.GetName().Version} starting. Logs: {FilePath}");
        // Retain recent sessions, while each running session also has size-based rotation.
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            foreach (var file in new DirectoryInfo(DirectoryPath).GetFiles("yalcy-*.log*")
                         .Where(f => f.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-7)))
                file.Delete();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Write(LogLevel.Warning, "Application", $"Could not clean up old logs: {ex.Message}");
        }
    }

    public static void Shutdown()
    {
        Pending.Writer.TryComplete();
        _writer?.Wait(TimeSpan.FromSeconds(2));
    }
}
