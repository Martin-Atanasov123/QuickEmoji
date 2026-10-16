using Windows.Win32;
using Windows.Win32.Foundation;

namespace QuickEmoji.Platform.Targeting;

/// <summary>
/// SPEC §4.2 step 2: call <c>SetForegroundWindow</c>, then poll <c>GetForegroundWindow</c> until it returns the
/// target, for at most 250 ms. Never blocks a thread while waiting (CLAUDE.md: use <c>await Task.Delay</c>, never
/// <c>Thread.Sleep</c>) — this runs on the UI thread between showing the popup and injecting into the target.
/// </summary>
public static class ForegroundActivator
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// True once <paramref name="targetWindowHandle"/> is the foreground window. False if the window is gone or
    /// something else took the foreground within <paramref name="timeout"/> — SPEC §4.2: "do not inject" in that case.
    /// </summary>
    public static async Task<bool> ActivateAsync(nint targetWindowHandle, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (targetWindowHandle == 0)
        {
            return false;
        }

        unsafe
        {
            PInvoke.SetForegroundWindow(new HWND((void*)targetWindowHandle));
        }

        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!IsForeground(targetWindowHandle))
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private static unsafe bool IsForeground(nint targetWindowHandle) =>
        (nint)PInvoke.GetForegroundWindow().Value == targetWindowHandle;
}
