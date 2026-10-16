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

    [Fact]
    public async Task PrepareForPasteAsync_snapshots_before_setting_the_new_text()
    {
        var fake = new FakeClipboard { CurrentSequenceNumber = 10 };
        fake.SeedSnapshot(new ClipboardSnapshot("original", null, null, null, null, false, false));
        var guard = new ClipboardGuard(fake);

        var result = await guard.PrepareForPasteAsync("❤️");

        Assert.False(result.TooLarge);
        Assert.Equal("original", result.Snapshot!.Text);
        Assert.Equal("❤️", fake.LastText);
        Assert.False(fake.LastAllowedInHistory);
    }

    [Fact]
    public async Task PrepareForPasteAsync_returns_the_sequence_number_observed_right_after_setting()
    {
        var fake = new FakeClipboard { CurrentSequenceNumber = 10 };
        fake.SeedSnapshot(ClipboardSnapshot.Empty);
        fake.SequenceNumberAfterNextSet = 11;
        var guard = new ClipboardGuard(fake);

        var result = await guard.PrepareForPasteAsync("❤️");

        Assert.Equal(11, result.SequenceNumberAfterSet);
    }

    [Fact]
    public async Task PrepareForPasteAsync_does_not_touch_the_clipboard_when_the_snapshot_is_too_large()
    {
        var fake = new FakeClipboard();
        fake.SeedSnapshot(ClipboardSnapshot.TooLarge);
        var guard = new ClipboardGuard(fake);

        var result = await guard.PrepareForPasteAsync("❤️");

        Assert.True(result.TooLarge);
        Assert.Null(result.Snapshot);
        Assert.Equal(0, fake.SetTextCallCount);
    }

    [Fact]
    public async Task RestoreIfUnchangedAsync_restores_when_the_sequence_number_still_matches()
    {
        var fake = new FakeClipboard { CurrentSequenceNumber = 42 };
        var snapshot = new ClipboardSnapshot("original", null, null, null, null, false, false);
        var guard = new ClipboardGuard(fake);

        var restored = await guard.RestoreIfUnchangedAsync(snapshot, expectedSequenceNumber: 42);

        Assert.True(restored);
        Assert.Same(snapshot, fake.LastRestored);
    }

    [Fact]
    public async Task RestoreIfUnchangedAsync_does_nothing_when_the_sequence_number_changed()
    {
        // SPEC §4.2.6 step 5: something else (the paste itself, or another app) touched the clipboard meanwhile.
        var fake = new FakeClipboard { CurrentSequenceNumber = 43 };
        var snapshot = new ClipboardSnapshot("original", null, null, null, null, false, false);
        var guard = new ClipboardGuard(fake);

        var restored = await guard.RestoreIfUnchangedAsync(snapshot, expectedSequenceNumber: 42);

        Assert.False(restored);
        Assert.Null(fake.LastRestored);
    }

    [Fact]
    public async Task RestoreIfUnchangedAsync_does_nothing_for_an_empty_snapshot_even_if_the_sequence_matches()
    {
        var fake = new FakeClipboard { CurrentSequenceNumber = 5 };
        var guard = new ClipboardGuard(fake);

        var restored = await guard.RestoreIfUnchangedAsync(ClipboardSnapshot.Empty, expectedSequenceNumber: 5);

        Assert.False(restored);
        Assert.Null(fake.LastRestored);
    }

    [Fact]
    public async Task RestoreIfUnchangedAsync_does_nothing_for_a_too_large_snapshot()
    {
        var fake = new FakeClipboard { CurrentSequenceNumber = 5 };
        var guard = new ClipboardGuard(fake);

        var restored = await guard.RestoreIfUnchangedAsync(ClipboardSnapshot.TooLarge, expectedSequenceNumber: 5);

        Assert.False(restored);
        Assert.Null(fake.LastRestored);
    }

    private sealed class FakeClipboard : IClipboard
    {
        private ClipboardSnapshot _nextSnapshot = ClipboardSnapshot.Empty;

        public string? LastText { get; private set; }

        public bool LastAllowedInHistory { get; private set; }

        public bool LastRoamable { get; private set; }

        public int SetTextCallCount { get; private set; }

        public ClipboardSnapshot? LastRestored { get; private set; }

        public long CurrentSequenceNumber { get; set; }

        public long? SequenceNumberAfterNextSet { get; set; }

        public void SeedSnapshot(ClipboardSnapshot snapshot) => _nextSnapshot = snapshot;

        public void SetText(string text, bool allowedInHistory, bool roamable)
        {
            LastText = text;
            LastAllowedInHistory = allowedInHistory;
            LastRoamable = roamable;
            SetTextCallCount++;
            if (SequenceNumberAfterNextSet is { } seq)
            {
                CurrentSequenceNumber = seq;
            }
        }

        public Task<ClipboardSnapshot> SnapshotAsync() => Task.FromResult(_nextSnapshot);

        public Task RestoreAsync(ClipboardSnapshot snapshot)
        {
            LastRestored = snapshot;
            return Task.CompletedTask;
        }

        public long GetSequenceNumber() => CurrentSequenceNumber;
    }
}
