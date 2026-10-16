namespace QuickEmoji.Platform.Clipboard;

/// <summary>
/// The clipboard operations <see cref="ClipboardGuard"/> needs. Exists so <see cref="ClipboardGuard"/>'s logic
/// is testable against <c>FakeClipboard</c> instead of the real clipboard — the real one
/// (<see cref="SystemClipboard"/>) needs an STA thread with COM initialized, which only the running WinUI app
/// provides; a bare test host does not (see docs/DECISIONS.md).
/// </summary>
public interface IClipboard
{
    /// <summary>
    /// Puts <paramref name="text"/> on the clipboard. <paramref name="allowedInHistory"/> and
    /// <paramref name="roamable"/> map to SPEC §4.2.6's <c>CanIncludeInClipboardHistory</c> and
    /// <c>CanUploadToCloudClipboard</c>.
    /// </summary>
    void SetText(string text, bool allowedInHistory, bool roamable);

    /// <summary>Captures the current clipboard (SPEC §4.3), or <see cref="ClipboardSnapshot.TooLarge"/> if it exceeds the 32 MB cap.</summary>
    Task<ClipboardSnapshot> SnapshotAsync();

    /// <summary>Puts a previously captured snapshot back. A no-op for an empty or too-large snapshot.</summary>
    Task RestoreAsync(ClipboardSnapshot snapshot);

    /// <summary>The OS clipboard sequence number — increments on every change, from any process (SPEC §4.2.6 step 5).</summary>
    long GetSequenceNumber();
}
