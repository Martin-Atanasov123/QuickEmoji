namespace QuickEmoji.Platform.Display;

/// <summary>Where the popup anchors itself (SPEC §6.1 "Position" setting).</summary>
public enum PopupPositionMode
{
    /// <summary>Near the text caret; falls back to near cursor when no caret rectangle is available.</summary>
    NearCaret,
    NearCursor,
    Centered,
}

/// <summary>The popup's size in effective (DPI-independent) pixels, as WinUI/XAML measures it.</summary>
public readonly record struct PopupSize(double EffectiveWidth, double EffectiveHeight);

/// <summary>The popup's top-left corner and size, both in physical pixels — what AppWindow.MoveAndResize needs.</summary>
public readonly record struct PlacedPopup(Point Position, int Width, int Height);

/// <summary>
/// Pure positioning math for the popup window (SPEC §6.1): clamps to the monitor's work area and flips above
/// the anchor when there is no room below. Takes no Win32 dependency, so every case is unit-testable; the real
/// monitor/DPI/caret/cursor values come from Win32 queries elsewhere in Platform.
/// </summary>
public static class PopupPlacement
{
    /// <summary>Physical-pixel gap between the anchor and the popup.</summary>
    public const int AnchorGap = 8;

    /// <summary>Returns the popup's position and device-pixel size, clamped to <paramref name="monitor"/>'s work area.</summary>
    public static PlacedPopup Compute(
        PopupPositionMode mode,
        PopupSize effectiveSize,
        MonitorInfo monitor,
        Rect? caretRectangle,
        Point cursorPosition)
    {
        var size = ToDevicePixels(effectiveSize, monitor.DpiScale);

        if (mode == PopupPositionMode.Centered)
        {
            return new PlacedPopup(Clamp(Center(monitor.WorkArea, size), monitor.WorkArea, size), size.Width, size.Height);
        }

        var anchor = mode == PopupPositionMode.NearCaret && caretRectangle is { } rect && !IsEmpty(rect)
            ? rect
            : PointRect(cursorPosition);

        var position = Clamp(NearAnchor(anchor, size, monitor.WorkArea), monitor.WorkArea, size);
        return new PlacedPopup(position, size.Width, size.Height);
    }

    public static (int Width, int Height) ToDevicePixels(PopupSize size, double dpiScale) =>
        ((int)Math.Round(size.EffectiveWidth * dpiScale), (int)Math.Round(size.EffectiveHeight * dpiScale));

    private static Point Center(Rect workArea, (int Width, int Height) size) => new(
        workArea.Left + ((workArea.Width - size.Width) / 2),
        workArea.Top + ((workArea.Height - size.Height) / 2));

    /// <summary>Below-left of the anchor; above-left when there is no room below.</summary>
    private static Point NearAnchor(Rect anchor, (int Width, int Height) size, Rect workArea)
    {
        var fitsBelow = anchor.Bottom + AnchorGap + size.Height <= workArea.Bottom;
        var top = fitsBelow ? anchor.Bottom + AnchorGap : anchor.Top - AnchorGap - size.Height;
        return new Point(anchor.Left, top);
    }

    private static Point Clamp(Point topLeft, Rect workArea, (int Width, int Height) size)
    {
        var maxX = Math.Max(workArea.Left, workArea.Right - size.Width);
        var maxY = Math.Max(workArea.Top, workArea.Bottom - size.Height);
        return new Point(
            Math.Clamp(topLeft.X, workArea.Left, maxX),
            Math.Clamp(topLeft.Y, workArea.Top, maxY));
    }

    private static Rect PointRect(Point p) => new(p.X, p.Y, p.X, p.Y);

    private static bool IsEmpty(Rect r) => r.Width <= 0 || r.Height <= 0;
}
