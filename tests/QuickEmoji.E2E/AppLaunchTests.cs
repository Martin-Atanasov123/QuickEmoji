using System.Diagnostics;

namespace QuickEmoji.E2E;

[Trait("Category", "E2E")]
public sealed class AppLaunchTests
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task App_starts_and_exits_cleanly_in_startup_check_mode()
    {
        using var process = Process.Start(new ProcessStartInfo(AppUnderTest.LocateExecutable(), "--startup-check")
        {
            UseShellExecute = false,
        })!;

        using var timeout = new CancellationTokenSource(StartupTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"QuickEmoji did not exit within {StartupTimeout.TotalSeconds} s in --startup-check mode.");
        }

        Assert.Equal(0, process.ExitCode);
    }
}
