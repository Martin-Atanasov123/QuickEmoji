using Windows.ApplicationModel.DataTransfer;

namespace QuickEmoji.Platform.Clipboard;

/// <summary>
/// The real clipboard, via the WinRT <see cref="Windows.ApplicationModel.DataTransfer"/> APIs (part of the OS SDK
/// projections; no WinAppSDK/XAML reference needed, so this stays inside Platform's "no UI types" rule). Must be
/// called from a thread with COM initialized as STA — the running WinUI app's UI thread, which <c>Program.Main</c>
/// sets up before anything in Platform runs. Only <see cref="ClipboardGuard"/> constructs this.
/// </summary>
public sealed class SystemClipboard : IClipboard
{
    public void SetText(string text, bool allowedInHistory, bool roamable)
    {
        var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        package.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContentWithOptions(
            package, new ClipboardContentOptions { IsAllowedInHistory = allowedInHistory, IsRoamable = roamable });
    }
}
