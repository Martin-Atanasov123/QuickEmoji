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
    // WM_APP..0xBFFF is reserved for private application messages.
    private const uint ShutdownMessage = PInvoke.WM_NULL + 1;

    // Window procedures are static (required by [UnmanagedCallersOnly]), so instances register themselves here
    // by HWND for the brief window between CreateWindowEx and the message loop starting on the same thread.
    private static readonly ConcurrentDictionary<nint, Action<uint, WPARAM, LPARAM>> Routes = new();

    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private HWND _hwnd;
    private Exception? _startupError;
    private bool _disposed;

    public MessageOnlyWindow(string className, Action<uint, WPARAM, LPARAM> onMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(className);
        ArgumentNullException.ThrowIfNull(onMessage);

        _thread = new Thread(() => Run(className, onMessage)) { IsBackground = true, Name = className };
        _thread.Start();
        _ready.Wait();
        if (_startupError is not null)
        {
            throw new InvalidOperationException("Failed to create the message-only window.", _startupError);
        }
    }

    /// <summary>The window handle, valid only while the owning thread is alive.</summary>
    public unsafe nint Handle => (nint)_hwnd.Value;

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

    private unsafe void Run(string className, Action<uint, WPARAM, LPARAM> onMessage)
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

            Routes[(nint)_hwnd.Value] = onMessage;
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe LRESULT WndProc(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        if (message == ShutdownMessage)
        {
            PInvoke.DestroyWindow(hwnd);
            PInvoke.PostQuitMessage(0);
            return new LRESULT(0);
        }

        if (Routes.TryGetValue((nint)hwnd.Value, out var handler))
        {
            handler(message, wParam, lParam);
        }

        return PInvoke.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private sealed class Win32Exception(string api) : Exception($"{api} failed (Win32 error {Marshal.GetLastWin32Error()}).");
}
