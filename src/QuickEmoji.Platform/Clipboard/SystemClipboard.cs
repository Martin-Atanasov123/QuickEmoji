using Windows.ApplicationModel.DataTransfer;
using Windows.Win32;

namespace QuickEmoji.Platform.Clipboard;

/// <summary>
/// The real clipboard, via the WinRT <see cref="Windows.ApplicationModel.DataTransfer"/> APIs (part of the OS SDK
/// projections; no WinAppSDK/XAML reference needed, so this stays inside Platform's "no UI types" rule), plus one
/// raw Win32 call for the sequence number, which WinRT does not expose. Must be called from a thread with COM
/// initialized as STA — the running WinUI app's UI thread, which <c>Program.Main</c> sets up before anything in
/// Platform runs. Only <see cref="ClipboardGuard"/> constructs this.
/// </summary>
public sealed class SystemClipboard : IClipboard
{
    /// <summary>SPEC §4.3: "Snapshots over 32 MB: skip the clipboard method and use Copy fallback."</summary>
    private const long MaxSnapshotBytes = 32 * 1024 * 1024;

    public void SetText(string text, bool allowedInHistory, bool roamable)
    {
        var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        package.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContentWithOptions(
            package, new ClipboardContentOptions { IsAllowedInHistory = allowedInHistory, IsRoamable = roamable });
    }

    public async Task<ClipboardSnapshot> SnapshotAsync()
    {
        DataPackageView content;
        try
        {
            content = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        }
        catch (Exception)
        {
            // No clipboard owner, or another app is mid-write; nothing we can safely capture.
            return ClipboardSnapshot.Empty;
        }

        if (content.AvailableFormats.Count == 0)
        {
            return ClipboardSnapshot.Empty;
        }

        var text = content.Contains(StandardDataFormats.Text) ? await content.GetTextAsync() : null;
        var html = content.Contains(StandardDataFormats.Html) ? await content.GetHtmlFormatAsync() : null;
        var rtf = content.Contains(StandardDataFormats.Rtf) ? await content.GetRtfAsync() : null;
        var bitmap = content.Contains(StandardDataFormats.Bitmap) ? await content.GetBitmapAsync() : null;
        var storageItems = content.Contains(StandardDataFormats.StorageItems) ? await content.GetStorageItemsAsync() : null;

        var estimatedBytes = EstimateUtf16Bytes(text) + EstimateUtf16Bytes(html) + EstimateUtf16Bytes(rtf);
        if (bitmap is not null)
        {
            using var stream = await bitmap.OpenReadAsync();
            estimatedBytes += (long)stream.Size;
        }

        return estimatedBytes > MaxSnapshotBytes
            ? ClipboardSnapshot.TooLarge
            : new ClipboardSnapshot(text, html, rtf, bitmap, storageItems, WasEmpty: false, TooLargeToCapture: false);
    }

    public Task RestoreAsync(ClipboardSnapshot snapshot)
    {
        if (!snapshot.IsRestorable)
        {
            return Task.CompletedTask;
        }

        var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        if (snapshot.Text is not null)
        {
            package.SetText(snapshot.Text);
        }

        if (snapshot.Html is not null)
        {
            package.SetHtmlFormat(snapshot.Html);
        }

        if (snapshot.Rtf is not null)
        {
            package.SetRtf(snapshot.Rtf);
        }

        if (snapshot.Bitmap is Windows.Storage.Streams.RandomAccessStreamReference bitmap)
        {
            package.SetBitmap(bitmap);
        }

        if (snapshot.StorageItems is { Count: > 0 })
        {
            package.SetStorageItems(snapshot.StorageItems);
        }

        // Restoring the user's own content: full history/roaming behavior, unlike the transient emoji we put
        // there (SetText above excludes that). No new formats were written, so there is nothing more to await.
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContentWithOptions(
            package, new ClipboardContentOptions { IsAllowedInHistory = true, IsRoamable = true });
        return Task.CompletedTask;
    }

    public long GetSequenceNumber() => PInvoke.GetClipboardSequenceNumber();

    private static long EstimateUtf16Bytes(string? text) => text is null ? 0 : text.Length * 2L;
}
