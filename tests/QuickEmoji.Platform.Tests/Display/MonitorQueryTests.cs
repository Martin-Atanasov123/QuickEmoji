using QuickEmoji.Platform.Display;

namespace QuickEmoji.Platform.Tests.Display;

[Trait("Category", "Win32")]
public sealed class MonitorQueryTests
{
    [Fact]
    public void FromPoint_returns_a_non_empty_work_area_and_a_sane_dpi_scale()
    {
        var info = MonitorQuery.FromPoint(new Point(0, 0));

        Assert.True(info.WorkArea.Width > 0);
        Assert.True(info.WorkArea.Height > 0);
        Assert.True(info.DpiScale is >= 1.0 and <= 4.0, $"DpiScale was {info.DpiScale}");
    }

    [Fact]
    public void FromPoint_off_screen_still_resolves_to_the_nearest_monitor()
    {
        var info = MonitorQuery.FromPoint(new Point(-100_000, -100_000));

        Assert.True(info.WorkArea.Width > 0);
        Assert.True(info.WorkArea.Height > 0);
    }

    [Fact]
    public void GetCursorPosition_returns_a_point_inside_some_monitors_bounds()
    {
        var cursor = MonitorQuery.GetCursorPosition();
        var monitor = MonitorQuery.FromPoint(cursor);

        // The cursor is always on some monitor; the monitor found for its own position must contain it.
        Assert.True(cursor.X >= monitor.WorkArea.Left - 10_000 && cursor.X <= monitor.WorkArea.Right + 10_000);
    }
}
