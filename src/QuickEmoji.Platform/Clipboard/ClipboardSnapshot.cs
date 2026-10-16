using Windows.Storage;
using Windows.Storage.Streams;

namespace QuickEmoji.Platform.Clipboard;

/// <summary>
/// A captured clipboard state (SPEC §4.3): CF_UNICODETEXT, HTML Format, RTF, CF_DIB/CF_DIBV5/PNG (as one bitmap
/// reference) and CF_HDROP are restored exactly; anything else on the clipboard is not captured (best effort).
/// </summary>
public sealed record ClipboardSnapshot(
    string? Text,
    string? Html,
    string? Rtf,
    IRandomAccessStreamReference? Bitmap,
    IReadOnlyList<IStorageItem>? StorageItems,
    bool WasEmpty,
    bool TooLargeToCapture)
{
    /// <summary>The clipboard had nothing we recognise (or nothing at all) to restore.</summary>
    public static ClipboardSnapshot Empty { get; } = new(null, null, null, null, null, WasEmpty: true, TooLargeToCapture: false);

    /// <summary>SPEC §4.3: "Snapshots over 32 MB: skip the clipboard method and use Copy fallback."</summary>
    public static ClipboardSnapshot TooLarge { get; } = new(null, null, null, null, null, WasEmpty: false, TooLargeToCapture: true);

    /// <summary>True when there is nothing to put back (never captured, or there was nothing there to begin with).</summary>
    public bool IsRestorable => !WasEmpty && !TooLargeToCapture;
}
