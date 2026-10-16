using System.Diagnostics;
using QuickEmoji.Platform.Injection;

namespace QuickEmoji.Platform.Tests.Injection;

[Trait("Category", "Win32")]
public sealed class KeyReleaseWaiterTests
{
    [Fact]
    public async Task Returns_true_quickly_when_no_modifier_or_enter_key_is_held()
    {
        // An unattended CI/dev machine holds no keys; this is the overwhelmingly common real case too. The
        // all-released fast path returns before the first Task.Delay, so there is nothing to meaningfully test
        // for cancellation here — see ForegroundActivatorTests for a case that genuinely polls.
        var stopwatch = Stopwatch.StartNew();

        var released = await KeyReleaseWaiter.WaitForReleaseAsync(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);

        Assert.True(released);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(250), $"took {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task A_pre_cancelled_token_still_returns_once_all_keys_are_already_released()
    {
        // The fast path (all keys already up) completes without ever awaiting, so it must not throw even with a
        // cancelled token — cancellation is only observed if the method actually has to wait.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var released = await KeyReleaseWaiter.WaitForReleaseAsync(TimeSpan.FromSeconds(1), cts.Token);

        Assert.True(released);
    }
}
