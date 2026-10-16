using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace QuickEmoji.Platform.Injection;

/// <summary>
/// Builds the <c>INPUT[]</c> array <see cref="TextInjector"/> sends in one <c>SendInput</c> call (SPEC §4.2 step 5).
/// Pure: no Win32 call here, so every sequence shape is unit-testable without a target window.
/// </summary>
internal static class InputBuilder
{
    /// <summary>
    /// One key-down and one key-up <c>KEYEVENTF_UNICODE</c> event per UTF-16 code unit of <paramref name="text"/>.
    /// A C# <see cref="string"/> already <i>is</i> UTF-16, so a surrogate pair becomes two code units sent in
    /// order — exactly how Windows expects a supplementary-plane character delivered via Unicode SendInput.
    /// Nothing here inspects or re-groups code units: ZWJ, VS16, skin-tone modifiers and tag characters all pass
    /// through unchanged (CLAUDE.md: never split or normalise a sequence).
    /// </summary>
    public static INPUT[] BuildUnicodeText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var events = new INPUT[text.Length * 2];
        for (var i = 0; i < text.Length; i++)
        {
            events[(i * 2) + 0] = KeyEvent(text[i], KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE);
            events[(i * 2) + 1] = KeyEvent(text[i], KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP);
        }

        return events;
    }

    /// <summary>
    /// A modifier-down, key-down, key-up, modifier-up sequence for a paste shortcut (SPEC §4.2 step 6.3):
    /// Ctrl+V or Shift+Insert. Virtual-key events, not Unicode ones — this is a real keystroke, not text.
    /// </summary>
    public static INPUT[] BuildPasteShortcut(VIRTUAL_KEY modifier, VIRTUAL_KEY key) =>
    [
        KeyEvent(modifier, default),
        KeyEvent(key, default),
        KeyEvent(key, KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP),
        KeyEvent(modifier, KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP),
    ];

    private static INPUT KeyEvent(char unicodeChar, KEYBD_EVENT_FLAGS flags)
    {
        var input = new INPUT { type = INPUT_TYPE.INPUT_KEYBOARD };
        input.Anonymous.ki.wVk = 0; // must be 0 when KEYEVENTF_UNICODE is set
        input.Anonymous.ki.wScan = unicodeChar;
        input.Anonymous.ki.dwFlags = flags;
        return input;
    }

    private static INPUT KeyEvent(VIRTUAL_KEY virtualKey, KEYBD_EVENT_FLAGS flags)
    {
        var input = new INPUT { type = INPUT_TYPE.INPUT_KEYBOARD };
        input.Anonymous.ki.wVk = virtualKey;
        input.Anonymous.ki.dwFlags = flags;
        return input;
    }
}
