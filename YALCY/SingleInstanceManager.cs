using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using YALCY.Diagnostics;

namespace YALCY;

/// <summary>
/// Ensures only a single instance of YALCY runs at any time,
/// and allows new instances to signal the running instance to bring its window to focus.
/// </summary>
public static class SingleInstanceManager
{
    private const string MutexName = @"Local\YALCY_SingleInstance_4A9B3E1F-7D2C-4E8A-93E1-65C0A72F6B8E";
    private const string PipeName = "YALCY_IPC_4A9B3E1F-7D2C-4E8A-93E1-65C0A72F6B8E";
    private const uint ASFW_ANY = 0xFFFFFFFF;
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint dwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private static Mutex? _mutex;
    private static FileStream? _lockFileStream;
    private static CancellationTokenSource? _ipcTokenSource;

    /// <summary>
    /// Attempts to acquire single instance ownership.
    /// Returns true if this is the only running instance, false otherwise.
    /// </summary>
    public static bool TryAcquire()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                _mutex = new Mutex(true, MutexName, out bool createdNew);
                if (createdNew)
                {
                    return true;
                }

                // If not newly created, check whether the previous owner abandoned it
                try
                {
                    if (_mutex.WaitOne(0, false))
                    {
                        return true;
                    }
                }
                catch (AbandonedMutexException)
                {
                    // Mutex was abandoned by a terminated process; we now own it
                    return true;
                }

                _mutex.Dispose();
                _mutex = null;
                return false;
            }
            catch (Exception ex)
            {
                AppLog.Write(LogLevel.Warning, "Application", $"Mutex check failed: {ex.Message}");
                return false;
            }
        }
        else
        {
            try
            {
                string lockDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YALCY");
                Directory.CreateDirectory(lockDir);
                string lockPath = Path.Combine(lockDir, "yalcy.lock");

                _lockFileStream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (Exception ex)
            {
                AppLog.Write(LogLevel.Warning, "Application", $"File lock check failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Signals the currently running primary instance with the given command line arguments,
    /// or "SHOW" by default.
    /// </summary>
    public static void NotifyExistingInstance(string[]? args = null)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                AllowSetForegroundWindow(ASFW_ANY);
            }
            catch
            {
                // Ignore if not permitted
            }
        }

        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1500);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            if (args != null && args.Length > 0)
            {
                writer.WriteLine("ARGS:" + string.Join("\u001F", args));
            }
            else
            {
                writer.WriteLine("SHOW");
            }
        }
        catch (Exception ex)
        {
            AppLog.Write(LogLevel.Debug, "Application", $"Could not notify existing instance: {ex.Message}");
        }
    }

    /// <summary>
    /// Starts the background IPC server to listen for signals and arguments from subsequent instances.
    /// </summary>
    public static void StartIpcServer(Action<string[]> onArgumentsReceived)
    {
        _ipcTokenSource = new CancellationTokenSource();
        var token = _ipcTokenSource.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                    using var reader = new StreamReader(server);
                    var line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(line))
                    {
                        if (line.StartsWith("ARGS:", StringComparison.Ordinal))
                        {
                            var argTokens = line.Substring(5).Split('\u001F');
                            onArgumentsReceived(argTokens);
                        }
                        else if (string.Equals(line, "SHOW", StringComparison.OrdinalIgnoreCase))
                        {
                            onArgumentsReceived(Array.Empty<string>());
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AppLog.Write(LogLevel.Debug, "Application", $"IPC server error: {ex.Message}");
                    try
                    {
                        await Task.Delay(250, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                finally
                {
                    try { server?.Dispose(); } catch { }
                }
            }
        }, token);
    }

    /// <summary>
    /// Brings the specified Avalonia window to the foreground on the desktop.
    /// </summary>
    public static void BringWindowToForeground(Window window)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var handle = window.TryGetPlatformHandle()?.Handle;
                if (handle.HasValue && handle.Value != IntPtr.Zero)
                {
                    ShowWindow(handle.Value, SW_RESTORE);
                    SetForegroundWindow(handle.Value);
                }
            }
            catch (Exception ex)
            {
                AppLog.Write(LogLevel.Debug, "Application", $"Failed to set foreground window: {ex.Message}");
            }
        }

        try
        {
            window.Topmost = true;
            window.Topmost = false;
        }
        catch
        {
            // Ignore if platform does not support topmost toggle
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;

        public PROPERTYKEY(Guid guid, uint id)
        {
            fmtid = guid;
            pid = id;
        }
    }

    private static readonly PROPERTYKEY PKEY_AppUserModel_ID = new PROPERTYKEY(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5
    );

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        [PreserveSig] int Commit();
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pwszVal;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHGetPropertyStoreForWindow(
        IntPtr hwnd,
        ref Guid riid,
        [Out][MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppv
    );

    /// <summary>
    /// Sets an explicit AppUserModelID for a window so Windows shell and PowerToys Workspaces
    /// can identify and group it uniquely.
    /// </summary>
    public static void SetWindowAppId(Window window, string appId)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var handle = window.TryGetPlatformHandle()?.Handle;
            if (handle.HasValue && handle.Value != IntPtr.Zero)
            {
                Guid iidIPropertyStore = typeof(IPropertyStore).GUID;
                SHGetPropertyStoreForWindow(handle.Value, ref iidIPropertyStore, out IPropertyStore propertyStore);
                if (propertyStore != null)
                {
                    PROPVARIANT pv = new PROPVARIANT
                    {
                        vt = 31, // VT_LPWSTR
                        pwszVal = Marshal.StringToCoTaskMemUni(appId)
                    };
                    try
                    {
                        var key = PKEY_AppUserModel_ID;
                        propertyStore.SetValue(ref key, ref pv);
                        propertyStore.Commit();
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(pv.pwszVal);
                    }
                }
            }
        }
        catch
        {
            // Ignore if shell property store is unavailable
        }
    }

    /// <summary>
    /// Releases single instance locks and stops the IPC server.
    /// </summary>
    public static void Release()
    {
        if (_ipcTokenSource != null)
        {
            try
            {
                _ipcTokenSource.Cancel();
                _ipcTokenSource.Dispose();
            }
            catch { }
            _ipcTokenSource = null;
        }

        if (_mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch { }
            try
            {
                _mutex.Dispose();
            }
            catch { }
            _mutex = null;
        }

        if (_lockFileStream != null)
        {
            try
            {
                _lockFileStream.Dispose();
            }
            catch { }
            _lockFileStream = null;
        }
    }
}
