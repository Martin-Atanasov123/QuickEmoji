using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace QuickEmoji.Platform.Injection;

/// <summary>Paste shortcuts SPEC §4.2 step 6.3 supports, by per-app override.</summary>
public enum PasteShortcut
{
    CtrlV,
    ShiftInsert,
}

/// <summary>
/// The only type allowed to call SendInput, apart from the E2E test driver (CLAUDE.md). Sends everything for one
/// commit in a single SendInput call (SPEC §4.2 step 5), so the whole sequence is atomic from the target's
/// point of view — no other input can interleave partway through an emoji.
/// </summary>
public static class TextInjector
{
    /// <summary>Returns the number of events the target actually accepted; SPEC §4.1's UIPI gotcha is exactly why this can silently be less than expected.</summary>
    public static uint SendUnicodeText(string text) => PInvoke.SendInput(InputBuilder.BuildUnicodeText(text), InputSize);

    public static uint SendPasteShortcut(PasteShortcut shortcut)
    {
        var (modifier, key) = shortcut switch
        {
            PasteShortcut.CtrlV => (VIRTUAL_KEY.VK_CONTROL, VIRTUAL_KEY.VK_V),
            PasteShortcut.ShiftInsert => (VIRTUAL_KEY.VK_SHIFT, VIRTUAL_KEY.VK_INSERT),
            _ => throw new ArgumentOutOfRangeException(nameof(shortcut)),
        };
        return PInvoke.SendInput(InputBuilder.BuildPasteShortcut(modifier, key), InputSize);
    }

    private static unsafe int InputSize => sizeof(INPUT);
}
