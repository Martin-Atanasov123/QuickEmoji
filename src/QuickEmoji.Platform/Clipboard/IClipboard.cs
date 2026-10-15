namespace QuickEmoji.Platform.Clipboard;

/// <summary>
/// The clipboard operations <see cref="ClipboardGuard"/> needs. Minimal for Stage 3 (<see cref="SetText"/> only);
/// Stage 4 extends it with snapshot, format-by-format restore, and a sequence-number check (SPEC §4.2.6, §4.3).
/// Exists so <see cref="ClipboardGuard"/>'s logic is testable against <c>FakeClipboard</c> instead of the real
/// clipboard — the real one (<see cref="SystemClipboard"/>) needs an STA thread with COM initialized, which only
/// the running WinUI app provides; a bare test host does not, so a real-clipboard test would be unreliable there.
/// </summary>
public interface IClipboard
{
    /// <summary>
    /// Puts <paramref name="text"/> on the clipboard. <paramref name="allowedInHistory"/> and
    /// <paramref name="roamable"/> map to SPEC §4.2.6's <c>CanIncludeInClipboardHistory</c> and
    /// <c>CanUploadToCloudClipboard</c>.
    /// </summary>
    void SetText(string text, bool allowedInHistory, bool roamable);
}
