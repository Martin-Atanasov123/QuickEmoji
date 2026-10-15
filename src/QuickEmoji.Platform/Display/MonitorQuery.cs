using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace QuickEmoji.Platform.Display;

/// <summary>Real Win32 monitor and DPI queries (per-monitor DPI awareness v2, SPEC §6.1). Thin; the math lives in <see cref="PopupPlacement"/>.</summary>
public static class MonitorQuery
{
    /// <summary>The monitor under <paramref name="physicalPoint"/>, falling back to the nearest monitor if the point is off-screen.</summary>
    public static unsafe MonitorInfo FromPoint(Point physicalPoint)
    {
        var point = new System.Drawing.Point(physicalPoint.X, physicalPoint.Y);
        var monitor = PInvoke.MonitorFromPoint(point, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        return FromHandle(monitor);
    }

    /// <summary>The monitor that most overlaps the window, falling back to the nearest when it has none.</summary>
    public static unsafe MonitorInfo FromWindow(nint windowHandle)
    {
        var monitor = PInvoke.MonitorFromWindow(new HWND((void*)windowHandle), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        return FromHandle(monitor);
    }

    /// <summary>The cursor position in physical pixels.</summary>
    public static Point GetCursorPosition()
    {
        PInvoke.GetCursorPos(out var point);
        return new Point(point.X, point.Y);
    }

    private static unsafe MonitorInfo FromHandle(HMONITOR monitor)
    {
        var info = new MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        PInvoke.GetMonitorInfo(monitor, ref info);
        var workArea = new Rect(info.rcWork.left, info.rcWork.top, info.rcWork.right, info.rcWork.bottom);

        var dpi = 96u;
        if (PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _).Succeeded)
        {
            dpi = dpiX;
        }

        return new MonitorInfo(workArea, dpi / 96.0);
    }
}
