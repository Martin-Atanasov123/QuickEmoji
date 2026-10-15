namespace QuickEmoji.Platform.Clipboard;

/// <summary>
/// The only type allowed to read or write the clipboard (CLAUDE.md). Stage 3 implements only <see cref="Copy"/>,
/// for the copy-to-clipboard-and-toast path (SPEC §4.6) used before the insertion engine exists. Stage 4 extends
/// this same class with the snapshot/paste/restore sequence (SPEC §4.2.6, §4.3) — never a second class.
/// </summary>
public sealed class ClipboardGuard(IClipboard? clipboard = null)
{
    private readonly IClipboard _clipboard = clipboard ?? new SystemClipboard();

    /// <summary>
    /// Puts <paramref name="text"/> on the clipboard, excluded from Win+V clipboard history and Cloud Clipboard
    /// (SPEC §4.2.6 step 2) — the user asked to copy one emoji, not to add a permanent history entry.
    /// </summary>
    public void Copy(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _clipboard.SetText(text, allowedInHistory: false, roamable: false);
    }
}
