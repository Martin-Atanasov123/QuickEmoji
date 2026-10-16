using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;

namespace QuickEmoji.E2E;

/// <summary>
/// Reads and writes the clipboard directly via raw Win32, for test setup/assertions only (CLAUDE.md's "only
/// ClipboardGuard may read or write the clipboard" governs the shipped app; this is the E2E test driver's own
/// oracle). Raw Win32 clipboard calls need no STA/COM initialization, unlike <c>System.Windows.Forms.Clipboard</c>
/// or the WinRT clipboard — both of which throw off the xUnit test-runner's non-STA thread.
/// </summary>
internal static unsafe class ClipboardOracle
{
    private const uint CfUnicodeText = 13;

    /// <summary>Sets the clipboard's CF_UNICODETEXT content, for seeding the "clipboard unchanged" oracle.</summary>
    public static void WriteText(string text)
    {
        if (!PInvoke.OpenClipboard(default))
        {
            throw new InvalidOperationException("Could not open the clipboard for writing.");
        }

        try
        {
            PInvoke.EmptyClipboard();

            var byteCount = (uint)((text.Length + 1) * sizeof(char));
            var handle = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, byteCount);
            if (handle.Value == null)
            {
                throw new InvalidOperationException("GlobalAlloc failed while writing the clipboard.");
            }

            var pointer = PInvoke.GlobalLock(handle);
            if (pointer is null)
            {
                throw new InvalidOperationException("GlobalLock failed while writing the clipboard.");
            }

            try
            {
                text.AsSpan().CopyTo(new Span<char>(pointer, text.Length));
                ((char*)pointer)[text.Length] = '\0';
            }
            finally
            {
                PInvoke.GlobalUnlock(handle);
            }

            if (PInvoke.SetClipboardData(CfUnicodeText, (HANDLE)handle.Value).IsNull)
            {
                throw new InvalidOperationException("SetClipboardData failed while writing the clipboard.");
            }
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }

    /// <summary>The clipboard's CF_UNICODETEXT content, or null if there is none.</summary>
    public static string? ReadText()
    {
        if (!PInvoke.OpenClipboard(default))
        {
            throw new InvalidOperationException("Could not open the clipboard for reading.");
        }

        try
        {
            var handle = PInvoke.GetClipboardData(CfUnicodeText);
            if (handle.IsNull)
            {
                return null;
            }

            var pointer = PInvoke.GlobalLock((HGLOBAL)handle.Value);
            if (pointer is null)
            {
                return null;
            }

            try
            {
                return new string((char*)pointer);
            }
            finally
            {
                PInvoke.GlobalUnlock((HGLOBAL)handle.Value);
            }
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }
}
