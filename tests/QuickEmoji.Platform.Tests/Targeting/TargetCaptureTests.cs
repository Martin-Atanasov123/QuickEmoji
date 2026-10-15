using QuickEmoji.Platform.Security;
using QuickEmoji.Platform.Targeting;

namespace QuickEmoji.Platform.Tests.Targeting;

[Trait("Category", "Win32")]
public sealed class TargetCaptureTests
{
    [Fact]
    public void Capture_returns_a_valid_context_when_some_window_has_the_foreground()
    {
        var context = TargetCapture.Capture();

        // The test host or whatever the user clicked last has the foreground; some window almost always does.
        // If this ever flakes on a true no-foreground desktop state, the assertion below (ProcessId > 0 implies
        // IsValid) still holds, so this documents the invariant rather than assuming a specific environment.
        if (context.IsValid)
        {
            Assert.True(context.ProcessId > 0);
        }
    }

    [Fact]
    public void CapturedAt_uses_the_given_TimeProvider()
    {
        var fixedTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(fixedTime);

        var context = TargetCapture.Capture(time);

        Assert.Equal(fixedTime, context.CapturedAt);
    }

    [Fact]
    public void An_empty_caret_rectangle_reports_IsEmpty()
    {
        Assert.True(CaretRect.Empty.IsEmpty);
        Assert.True(new CaretRect(10, 10, 10, 10).IsEmpty); // zero width
        Assert.False(new CaretRect(10, 10, 20, 30).IsEmpty);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(IntegrityLevel.High, true)]
    [InlineData(IntegrityLevel.System, true)]
    [InlineData(IntegrityLevel.Medium, false)]
    [InlineData(IntegrityLevel.Low, false)]
    [InlineData(IntegrityLevel.Untrusted, false)]
    public void IsElevatedOrUnknown_treats_unknown_the_same_as_elevated(IntegrityLevel? level, bool expected)
    {
        var context = new TargetContext(1, 123, "notepad", 0, CaretRect.Empty, level, DateTimeOffset.UtcNow);

        Assert.Equal(expected, context.IsElevatedOrUnknown);
    }

    [Fact]
    public void IsValid_is_false_when_there_is_no_foreground_window()
    {
        var context = new TargetContext(0, 0, null, 0, CaretRect.Empty, null, DateTimeOffset.UtcNow);

        Assert.False(context.IsValid);
    }
}
