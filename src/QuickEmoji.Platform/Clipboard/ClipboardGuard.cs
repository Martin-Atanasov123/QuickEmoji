namespace QuickEmoji.Platform.Clipboard;

/// <summary>The outcome of preparing the clipboard for a paste (SPEC §4.2.6 steps 1-2).</summary>
public sealed record ClipboardPrepareResult(bool TooLarge, ClipboardSnapshot? Snapshot, long SequenceNumberAfterSet)
{
    public static ClipboardPrepareResult TooLargeResult { get; } = new(true, null, 0);
}

/// <summary>
/// The only type allowed to read or write the clipboard (CLAUDE.md). <see cref="Copy"/> is the copy-to-clipboard
/// path used by Ctrl+Enter and the elevated/error fallback toast (SPEC §4.6). <see cref="PrepareForPasteAsync"/>
/// and <see cref="RestoreIfUnchangedAsync"/> implement the full snapshot/paste/restore sequence for the
/// clipboard-paste insertion method (SPEC §4.2.6, §4.3), used only when a per-app override asks for it.
/// </summary>
public sealed class ClipboardGuard(IClipboard? clipboard = null)
{
    private readonly IClipboard _clipboard = clipboard ?? new SystemClipboard();

    /// <summary>
    /// Puts <paramref name="text"/> on the clipboard, excluded from Win+V clipboard history and Cloud Clipboard
    /// (SPEC §4.2.6 step 2) — the user asked to copy one emoji, not to add a permanent history entry. This
    /// deliberately overwrites whatever was there (SPEC §4.6): the user needs to paste it.
    /// </summary>
    public void Copy(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _clipboard.SetText(text, allowedInHistory: false, roamable: false);
    }

    /// <summary>
    /// SPEC §4.2.6 steps 1-2: snapshots the current clipboard, then puts <paramref name="text"/> on it, excluded
    /// from history/roaming. If the current clipboard is too large to safely capture (SPEC §4.3: over 32 MB),
    /// does <b>not</b> touch the clipboard at all and returns <see cref="ClipboardPrepareResult.TooLarge"/> — the
    /// caller falls back to <see cref="Copy"/> instead (SPEC §4.3: "skip the clipboard method").
    /// </summary>
    public async Task<ClipboardPrepareResult> PrepareForPasteAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var snapshot = await _clipboard.SnapshotAsync().ConfigureAwait(false);
        if (snapshot.TooLargeToCapture)
        {
            return ClipboardPrepareResult.TooLargeResult;
        }

        _clipboard.SetText(text, allowedInHistory: false, roamable: false);
        return new ClipboardPrepareResult(false, snapshot, _clipboard.GetSequenceNumber());
    }

    /// <summary>
    /// SPEC §4.2.6 step 5: restores <paramref name="snapshot"/> only if the clipboard's sequence number still
    /// matches <paramref name="expectedSequenceNumber"/> — the value observed right after
    /// <see cref="PrepareForPasteAsync"/> set the emoji. If it differs, something else (the paste itself, or
    /// another app) changed the clipboard in the meantime, and restoring would clobber that newer content, so
    /// this does nothing and returns false. Also does nothing for an empty snapshot (nothing to put back).
    /// </summary>
    public async Task<bool> RestoreIfUnchangedAsync(ClipboardSnapshot snapshot, long expectedSequenceNumber)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.IsRestorable || _clipboard.GetSequenceNumber() != expectedSequenceNumber)
        {
            return false;
        }

        await _clipboard.RestoreAsync(snapshot).ConfigureAwait(false);
        return true;
    }
}
