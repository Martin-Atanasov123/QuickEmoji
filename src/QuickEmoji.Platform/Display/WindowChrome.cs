using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace QuickEmoji.Platform.Display;

/// <summary>
/// DWM window chrome that WinUI 3's own window APIs do not expose: rounded corners on Windows 11 (SPEC §6.1,
/// flat on Windows 10 — DwmSetWindowAttribute simply fails harmlessly there, which is the correct fallback).
/// </summary>
public static class WindowChrome
{
    public static void ApplyRoundedCorners(nint windowHandle)
    {
        var preference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
        unsafe
        {
            PInvoke.DwmSetWindowAttribute(
                new HWND((void*)windowHandle),
                DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE,
                &preference,
                (uint)sizeof(DWM_WINDOW_CORNER_PREFERENCE));
        }
    }

    /// <summary>
    /// Calls <c>SetForegroundWindow</c> directly on the popup's own handle. WinUI 3's <c>Window.Activate()</c>
    /// is not reliable here: called from the dispatcher callback that handles WM_HOTKEY, it can leave the popup
    /// visible but without keyboard focus, so typed keys keep going to whatever app the user was in before
    /// (CLAUDE.md: "the process may take the foreground only while handling WM_HOTKEY" — this is that call).
    /// </summary>
    public static void BringToForeground(nint windowHandle)
    {
        unsafe
        {
            PInvoke.SetForegroundWindow(new HWND((void*)windowHandle));
        }
    }
}
