using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace QuickEmoji.Platform.Hotkeys;

/// <summary>
/// A message-only window (<c>HWND_MESSAGE</c>) with its own background thread and message loop (SPEC §5).
/// Receives no messages from the system except what is explicitly sent or posted to it (WM_HOTKEY, and the
/// internal shutdown message), so it needs no visible window and never appears in Alt+Tab or the taskbar.
/// </summary>
internal sealed class MessageOnlyWindow : IDisposable
{
    // WM_APP (0xC000)..0xFFFF is reserved for private application messages (never WM_NULL+1 — that
    // collides with WM_CREATE, which Windows sends synchronously inside CreateWindowEx itself).
    private const uint ShutdownMessage = PInvoke.WM_APP + 1;

    // Win32 window APIs (RegisterHotKey among them) fail with ERROR_WINDOW_OF_OTHER_THREAD unless called from
    // the thread that owns the window, so callers marshal work onto this thread with Invoke/InvokeMessage.
    private const uint InvokeMessage = PInvoke.WM_APP + 2;

    // Window procedures are static (required by [UnmanagedCallersOnly]), so instances register themselves here
    // by HWND for the brief window between CreateWindowEx and the message loop starting on the same thread.
    private static readonly ConcurrentDictionary<nint, MessageOnlyWindow> Routes = new();

    private readonly Action<uint, WPARAM, LPARAM> _onMessage;
    private readonly ConcurrentQueue<Action> _invokeQueue = new();
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private HWND _hwnd;
    private Exception? _startupError;
    private bool _disposed;

    public MessageOnlyWindow(string className, Action<uint, WPARAM, LPARAM> onMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(className);
        ArgumentNullException.ThrowIfNull(onMessage);

        _onMessage = onMessage;
        _thread = new Thread(() => Run(className)) { IsBackground = true, Name = className };
        _thread.Start();
        _ready.Wait();
        if (_startupError is not null)
        {
            throw new InvalidOperationException("Failed to create the message-only window.", _startupError);
        }
    }

    /// <summary>The window handle, valid only while the owning thread is alive.</summary>
    public unsafe nint Handle => (nint)_hwnd.Value;

    /// <summary>
    /// Runs <paramref name="action"/> on the window's own thread and waits for it to finish. Required for any
    /// Win32 call whose target is this window — RegisterHotKey and similar fail with ERROR_WINDOW_OF_OTHER_THREAD
    /// otherwise. Exceptions thrown by <paramref name="action"/> propagate to the caller.
    /// </summary>
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var done = new ManualResetEventSlim(false);
        Exception? error = null;
        _invokeQueue.Enqueue(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                done.Set();
            }
        });
        unsafe
        {
            PInvoke.PostMessage(_hwnd, InvokeMessage, default, default);
        }

        done.Wait();
        if (error is not null)
        {
            throw new InvalidOperationException("The action invoked on the message-only window's thread threw.", error);
        }
    }

    /// <inheritdoc cref="Invoke(Action)"/>
    public T Invoke<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        var result = default(T)!;
        // A statement-bodied lambda (not an expression lambda) so this binds to Invoke(Action), not back to
        // Invoke<T>(Func<T>) itself — an expression lambda here is ambiguous and recurses infinitely.
        Invoke(() => { result = func(); });
        return result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        unsafe
        {
            PInvoke.PostMessage(_hwnd, ShutdownMessage, default, default);
        }

        _thread.Join(TimeSpan.FromSeconds(2));
        _ready.Dispose();
    }

    private unsafe void Run(string className)
    {
        try
        {
            fixed (char* classNamePtr = className)
            {
                var windowClass = new WNDCLASSEXW
                {
                    cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                    lpfnWndProc = &WndProc,
                    lpszClassName = classNamePtr,
                };
                if (PInvoke.RegisterClassEx(&windowClass) == 0)
                {
                    throw new Win32Exception("RegisterClassEx");
                }

                _hwnd = PInvoke.CreateWindowEx(
                    0, new PCWSTR(classNamePtr), new PCWSTR(classNamePtr), 0, 0, 0, 0, 0,
                    HWND.HWND_MESSAGE, (HMENU)IntPtr.Zero, (HINSTANCE)IntPtr.Zero, null);
            }

            if (_hwnd.IsNull)
            {
                throw new Win32Exception("CreateWindowEx");
            }

            Routes[(nint)_hwnd.Value] = this;
        }
        catch (Exception ex)
        {
            _startupError = ex;
            _ready.Set();
            return;
        }

        _ready.Set();
        MSG msg;
        while (PInvoke.GetMessage(out msg, _hwnd, 0, 0))
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }

        Routes.TryRemove((nint)_hwnd.Value, out _);
    }

    /// <summary>Runs on this window's own thread, dispatched from <see cref="WndProc"/>.</summary>
    private void HandleMessage(uint message, WPARAM wParam, LPARAM lParam)
    {
        if (message == InvokeMessage)
        {
            if (_invokeQueue.TryDequeue(out var action))
            {
                action();
            }

            return;
        }

        _onMessage(message, wParam, lParam);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe LRESULT WndProc(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        if (message == ShutdownMessage)
        {
            PInvoke.DestroyWindow(hwnd);
            PInvoke.PostQuitMessage(0);
            return new LRESULT(0);
        }

        if (Routes.TryGetValue((nint)hwnd.Value, out var window))
        {
            window.HandleMessage(message, wParam, lParam);
        }

        return PInvoke.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private sealed class Win32Exception(string api) : Exception($"{api} failed (Win32 error {Marshal.GetLastWin32Error()}).");
}
