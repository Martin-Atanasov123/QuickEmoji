using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace QuickEmoji.Platform.Injection;

/// <summary>
/// SPEC §4.2 step 3 / CLAUDE.md gotcha: wait until Ctrl, Shift, Alt, Win and Enter are all released before
/// injecting, otherwise the target receives a modifier combined with the emoji input. Polls with
/// <c>await Task.Delay</c>, never <c>Thread.Sleep</c> (CLAUDE.md).
/// </summary>
public static class KeyReleaseWaiter
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    // VK_CONTROL/VK_SHIFT/VK_MENU each report either physical side being held; Win has no combined code.
    private static readonly VIRTUAL_KEY[] Keys =
    [
        VIRTUAL_KEY.VK_CONTROL, VIRTUAL_KEY.VK_SHIFT, VIRTUAL_KEY.VK_MENU,
        VIRTUAL_KEY.VK_LWIN, VIRTUAL_KEY.VK_RWIN, VIRTUAL_KEY.VK_RETURN,
    ];

    /// <summary>True once every tracked key is up. False if <paramref name="timeout"/> (SPEC: 300 ms) elapses first — injection proceeds anyway, since waiting forever is worse than one possibly-combined keystroke.</summary>
    public static async Task<bool> WaitForReleaseAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!AllReleased())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private static bool AllReleased()
    {
        foreach (var key in Keys)
        {
            // High bit set means the key is currently down (GetAsyncKeyState).
            if ((PInvoke.GetAsyncKeyState((int)key) & 0x8000) != 0)
            {
                return false;
            }
        }

        return true;
    }
}
