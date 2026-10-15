using QuickEmoji.Platform.Clipboard;

namespace QuickEmoji.Platform.Tests.Clipboard;

public sealed class ClipboardGuardTests
{
    [Fact]
    public void Copy_sets_the_given_text()
    {
        var fake = new FakeClipboard();
        var guard = new ClipboardGuard(fake);

        guard.Copy("❤️");

        Assert.Equal("❤️", fake.LastText);
    }

    [Fact]
    public void Copy_excludes_the_entry_from_clipboard_history_and_cloud_sync()
    {
        var fake = new FakeClipboard();
        var guard = new ClipboardGuard(fake);

        guard.Copy("😂");

        Assert.False(fake.LastAllowedInHistory);
        Assert.False(fake.LastRoamable);
    }

    [Fact]
    public void Copy_rejects_a_null_string()
    {
        var guard = new ClipboardGuard(new FakeClipboard());

        Assert.Throws<ArgumentNullException>(() => guard.Copy(null!));
    }

    [Fact]
    public void Copy_passes_a_zwj_sequence_through_unchanged()
    {
        var fake = new FakeClipboard();
        var guard = new ClipboardGuard(fake);

        guard.Copy("👨‍👩‍👧‍👦");

        Assert.Equal("👨‍👩‍👧‍👦", fake.LastText);
    }

    [Fact]
    public void Copy_calls_SetText_exactly_once()
    {
        var fake = new FakeClipboard();
        var guard = new ClipboardGuard(fake);

        guard.Copy("🔥");

        Assert.Equal(1, fake.SetTextCallCount);
    }

    private sealed class FakeClipboard : IClipboard
    {
        public string? LastText { get; private set; }

        public bool LastAllowedInHistory { get; private set; }

        public bool LastRoamable { get; private set; }

        public int SetTextCallCount { get; private set; }

        public void SetText(string text, bool allowedInHistory, bool roamable)
        {
            LastText = text;
            LastAllowedInHistory = allowedInHistory;
            LastRoamable = roamable;
            SetTextCallCount++;
        }
    }
}
