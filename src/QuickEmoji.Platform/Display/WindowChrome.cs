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
}
