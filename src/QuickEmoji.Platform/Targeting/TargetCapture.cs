using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using QuickEmoji.Platform.Security;

namespace QuickEmoji.Platform.Targeting;

/// <summary>
/// Captures <see cref="TargetContext"/> at hotkey time (SPEC §4.1). Must run inside the WM_HOTKEY handler, before
/// anything else takes the foreground — that is the only moment QuickEmoji is guaranteed the right foreground
/// window is still the one the user was typing into.
/// </summary>
public static class TargetCapture
{
    public static TargetContext Capture(TimeProvider? time = null)
    {
        var now = (time ?? TimeProvider.System).GetUtcNow();
        var foreground = PInvoke.GetForegroundWindow();
        if (foreground.IsNull)
        {
            return new TargetContext(0, 0, null, 0, CaretRect.Empty, null, now);
        }

        uint processId;
        uint threadId;
        unsafe
        {
            uint pid;
            threadId = PInvoke.GetWindowThreadProcessId(foreground, &pid);
            processId = pid;
        }

        string? processName = null;
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
            processName = process.ProcessName;
        }
        catch (ArgumentException)
        {
            // The process exited between GetWindowThreadProcessId and here; leave the name unknown.
        }

        var (focusedChild, caret) = CaptureGuiThreadInfo(threadId);
        var integrityLevel = processId > 0 ? IntegrityLevelReader.TryRead((int)processId) : null;

        unsafe
        {
            return new TargetContext((nint)foreground.Value, (int)processId, processName, focusedChild, caret, integrityLevel, now);
        }
    }

    private static unsafe (nint FocusedChild, CaretRect Caret) CaptureGuiThreadInfo(uint threadId)
    {
        var info = new GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<GUITHREADINFO>() };
        if (!PInvoke.GetGUIThreadInfo(threadId, &info))
        {
            return (0, CaretRect.Empty);
        }

        var caret = info.rcCaret is { left: 0, top: 0, right: 0, bottom: 0 }
            ? CaretRect.Empty
            : new CaretRect(info.rcCaret.left, info.rcCaret.top, info.rcCaret.right, info.rcCaret.bottom);
        return ((nint)info.hwndFocus.Value, caret);
    }
}
