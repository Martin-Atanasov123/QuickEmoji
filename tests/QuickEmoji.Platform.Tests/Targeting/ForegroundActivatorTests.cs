using System.Diagnostics;
using QuickEmoji.Platform.Targeting;

namespace QuickEmoji.Platform.Tests.Targeting;

[Trait("Category", "Win32")]
public sealed class ForegroundActivatorTests
{
    [Fact]
    public async Task A_zero_window_handle_fails_immediately_without_polling()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var stopwatch = Stopwatch.StartNew();

        var activated = await ForegroundActivator.ActivateAsync(0, TimeSpan.FromMilliseconds(250), cancellationToken);

        Assert.False(activated);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(50), $"took {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task A_handle_that_can_never_become_foreground_times_out_at_the_given_timeout()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        // An arbitrary non-zero value that is not a real window: SetForegroundWindow/GetForegroundWindow simply
        // never agree on it, so this exercises the full poll-until-timeout path (SPEC §4.2: "do not inject").
        var bogusHandle = new nint(0x7FFFFFFF);
        var timeout = TimeSpan.FromMilliseconds(100);
        var stopwatch = Stopwatch.StartNew();

        var activated = await ForegroundActivator.ActivateAsync(bogusHandle, timeout, cancellationToken);

        Assert.False(activated);
        Assert.True(stopwatch.Elapsed >= timeout - TimeSpan.FromMilliseconds(20), $"returned early after {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task Honors_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ForegroundActivator.ActivateAsync(new nint(0x7FFFFFFF), TimeSpan.FromSeconds(1), cts.Token));
    }
}
