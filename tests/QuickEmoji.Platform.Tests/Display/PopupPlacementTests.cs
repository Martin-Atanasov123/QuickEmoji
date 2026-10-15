using QuickEmoji.Platform.Display;

namespace QuickEmoji.Platform.Tests.Display;

public sealed class PopupPlacementTests
{
    // A 1920x1080 monitor at 100% scale, work area excluding a 40px taskbar at the bottom.
    private static readonly MonitorInfo Monitor100 = new(new Rect(0, 0, 1920, 1040), DpiScale: 1.0);
    private static readonly PopupSize Size = new(400, 300);

    [Fact]
    public void Near_caret_places_the_popup_below_left_of_the_caret()
    {
        var caret = new Rect(500, 300, 510, 320);

        var placed = PopupPlacement.Compute(PopupPositionMode.NearCaret, Size, Monitor100, caret, cursorPosition: default);

        Assert.Equal(new Point(500, 320 + PopupPlacement.AnchorGap), placed.Position);
    }

    [Fact]
    public void Near_caret_flips_above_when_there_is_no_room_below()
    {
        var caret = new Rect(500, 900, 510, 920); // near the bottom of a 1040-tall work area

        var placed = PopupPlacement.Compute(PopupPositionMode.NearCaret, Size, Monitor100, caret, cursorPosition: default);

        Assert.Equal(900 - PopupPlacement.AnchorGap - 300, placed.Position.Y);
    }

    [Fact]
    public void Near_caret_falls_back_to_near_cursor_when_there_is_no_caret()
    {
        var cursor = new Point(600, 400);

        var placed = PopupPlacement.Compute(PopupPositionMode.NearCaret, Size, Monitor100, caretRectangle: null, cursor);

        Assert.Equal(new Point(600, 400 + PopupPlacement.AnchorGap), placed.Position);
    }

    [Fact]
    public void Near_caret_falls_back_to_near_cursor_when_the_caret_rectangle_is_empty()
    {
        var cursor = new Point(600, 400);

        var placed = PopupPlacement.Compute(PopupPositionMode.NearCaret, Size, Monitor100, Rect.Empty, cursor);

        Assert.Equal(new Point(600, 400 + PopupPlacement.AnchorGap), placed.Position);
    }

    [Fact]
    public void Near_cursor_places_the_popup_below_left_of_the_cursor()
    {
        var cursor = new Point(700, 500);

        var placed = PopupPlacement.Compute(PopupPositionMode.NearCursor, Size, Monitor100, caretRectangle: null, cursor);

        Assert.Equal(new Point(700, 500 + PopupPlacement.AnchorGap), placed.Position);
    }

    [Fact]
    public void Near_cursor_ignores_a_caret_rectangle_even_when_one_is_given()
    {
        var caret = new Rect(10, 10, 20, 30);
        var cursor = new Point(700, 500);

        var placed = PopupPlacement.Compute(PopupPositionMode.NearCursor, Size, Monitor100, caret, cursor);

        Assert.Equal(new Point(700, 500 + PopupPlacement.AnchorGap), placed.Position);
    }

    [Fact]
    public void Centered_places_the_popup_in_the_middle_of_the_work_area()
    {
        var placed = PopupPlacement.Compute(PopupPositionMode.Centered, Size, Monitor100, caretRectangle: null, cursorPosition: default);

        Assert.Equal(new Point((1920 - 400) / 2, (1040 - 300) / 2), placed.Position);
    }

    [Fact]
    public void Clamps_to_the_left_edge_when_the_anchor_is_near_it()
    {
        var caret = new Rect(-50, 300, -40, 320);

        var placed = PopupPlacement.Compute(PopupPositionMode.NearCaret, Size, Monitor100, caret, cursorPosition: default);

        Assert.Equal(0, placed.Position.X);
    }

    [Fact]
    public void Clamps_to_the_right_edge_when_the_anchor_is_near_it()
    {
        var caret = new Rect(1800, 300, 1810, 320);

        var placed = PopupPlacement.Compute(PopupPositionMode.NearCaret, Size, Monitor100, caret, cursorPosition: default);

        Assert.Equal(1920 - 400, placed.Position.X);
    }

    [Fact]
    public void Clamps_to_the_top_edge_when_flipping_above_would_go_off_screen()
    {
        var caret = new Rect(500, 50, 510, 70); // near the top; flip-above math alone would put this off-screen

        var placed = PopupPlacement.Compute(PopupPositionMode.NearCaret, Size, Monitor100, caret, cursorPosition: default);

        Assert.True(placed.Position.Y >= 0);
    }

    [Fact]
    public void Clamps_to_the_bottom_work_area_edge_even_without_a_flip()
    {
        // A popup taller than the remaining space below the caret, but still fits above less badly than clamped-below.
        var tallSize = new PopupSize(400, 2000);
        var caret = new Rect(500, 500, 510, 520);

        var placed = PopupPlacement.Compute(PopupPositionMode.NearCaret, tallSize, Monitor100, caret, cursorPosition: default);

        Assert.True(placed.Position.Y >= Monitor100.WorkArea.Top);
        Assert.True(placed.Position.Y <= Monitor100.WorkArea.Bottom - 2000 || placed.Position.Y == Monitor100.WorkArea.Top);
    }

    [Theory]
    [InlineData(1.0, 400, 300)]
    [InlineData(1.5, 600, 450)]
    [InlineData(2.0, 800, 600)]
    public void Effective_size_is_scaled_to_device_pixels_by_the_monitor_dpi(double scale, int expectedWidth, int expectedHeight)
    {
        var (width, height) = PopupPlacement.ToDevicePixels(Size, scale);

        Assert.Equal(expectedWidth, width);
        Assert.Equal(expectedHeight, height);
    }

    [Fact]
    public void A_150_percent_monitor_positions_using_device_pixel_size_not_effective_size()
    {
        var monitor150 = Monitor100 with { DpiScale = 1.5 };
        var caret = new Rect(1700, 300, 1710, 320); // device-pixel width (600) would clip; effective (400) would not

        var placed = PopupPlacement.Compute(PopupPositionMode.NearCaret, Size, monitor150, caret, cursorPosition: default);

        Assert.Equal(1920 - 600, placed.Position.X);
    }

    [Fact]
    public void A_popup_wider_than_the_work_area_still_clamps_without_throwing()
    {
        var hugeSize = new PopupSize(3000, 300);

        var placed = PopupPlacement.Compute(PopupPositionMode.Centered, hugeSize, Monitor100, caretRectangle: null, cursorPosition: default);

        Assert.Equal(Monitor100.WorkArea.Left, placed.Position.X);
    }
}
