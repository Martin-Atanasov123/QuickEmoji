namespace QuickEmoji.Platform.Display;

public readonly record struct Point(int X, int Y);

public readonly record struct Rect(int Left, int Top, int Right, int Bottom)
{
    public static Rect Empty { get; } = default;

    public int Width => Right - Left;

    public int Height => Bottom - Top;
}

/// <summary>
/// The monitor geometry and scale <see cref="PopupPlacement"/> positions against. Produced from a real
/// <c>MONITORINFO</c> + DPI query (<c>Display/MonitorQuery.cs</c>, Win32-dependent), kept separate so the
/// placement math itself takes no Win32 dependency and is fully unit-testable.
/// </summary>
public readonly record struct MonitorInfo(Rect WorkArea, double DpiScale);
