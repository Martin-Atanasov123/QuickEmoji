using Microsoft.Extensions.Time.Testing;
using QuickEmoji.Core.Personalization;
using QuickEmoji.Core.Search;

namespace QuickEmoji.Core.Tests.Personalization;

public sealed class FrecencyStoreTests
{
    [Fact]
    public void A_fresh_store_has_no_uses()
    {
        var store = new FrecencyStore();

        Assert.Equal(0, store.QueryUses("love", "❤️"));
        Assert.Equal(0, store.GlobalUses("❤️"));
    }

    [Fact]
    public void RecordUse_increments_both_the_query_specific_and_global_counts()
    {
        var store = new FrecencyStore();

        store.RecordUse("love", "❤️");

        AssertApprox(1, store.QueryUses("love", "❤️"));
        AssertApprox(1, store.GlobalUses("❤️"));
    }

    [Fact]
    public void RecordUse_does_not_affect_a_different_query_or_emoji()
    {
        var store = new FrecencyStore();

        store.RecordUse("love", "❤️");

        Assert.Equal(0, store.QueryUses("fire", "❤️"));
        Assert.Equal(0, store.QueryUses("love", "🔥"));
        Assert.Equal(0, store.GlobalUses("🔥"));
    }

    [Fact]
    public void An_empty_query_only_updates_the_global_count()
    {
        var store = new FrecencyStore();

        store.RecordUse("", "❤️");

        AssertApprox(0, store.QueryUses("", "❤️"));
        AssertApprox(1, store.GlobalUses("❤️"));
    }

    [Fact]
    public void Repeated_uses_accumulate()
    {
        var store = new FrecencyStore();

        store.RecordUse("love", "❤️");
        store.RecordUse("love", "❤️");
        store.RecordUse("love", "❤️");

        AssertApprox(3, store.QueryUses("love", "❤️"));
    }

    [Fact]
    public void A_use_decays_by_half_after_one_half_life()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new FrecencyStore(time);

        store.RecordUse("love", "❤️");
        time.Advance(RankingWeightsHalfLife);

        Assert.True(Math.Abs(store.QueryUses("love", "❤️") - 0.5) < 0.01, $"got {store.QueryUses("love", "❤️")}");
    }

    [Fact]
    public void A_use_decays_to_a_quarter_after_two_half_lives()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new FrecencyStore(time);

        store.RecordUse("love", "❤️");
        time.Advance(RankingWeightsHalfLife * 2);

        Assert.True(Math.Abs(store.QueryUses("love", "❤️") - 0.25) < 0.01, $"got {store.QueryUses("love", "❤️")}");
    }

    [Fact]
    public void Decay_does_not_apply_until_time_advances()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new FrecencyStore(time);

        store.RecordUse("love", "❤️");

        Assert.True(Math.Abs(store.QueryUses("love", "❤️") - 1) < 1e-9, $"got {store.QueryUses("love", "❤️")}");
    }

    [Fact]
    public void A_fresh_use_after_decay_is_added_to_the_decayed_remainder()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new FrecencyStore(time);

        store.RecordUse("love", "❤️");
        time.Advance(RankingWeightsHalfLife);
        store.RecordUse("love", "❤️");

        Assert.True(Math.Abs(store.QueryUses("love", "❤️") - 1.5) < 0.01, $"got {store.QueryUses("love", "❤️")}");
    }

    [Fact]
    public void MostUsed_orders_by_decayed_global_count_descending()
    {
        var store = new FrecencyStore();
        store.RecordUse("love", "❤️");
        store.RecordUse("love", "❤️");
        store.RecordUse("love", "❤️");
        store.RecordUse("haha", "😂");
        store.RecordUse("haha", "😂");
        store.RecordUse("fire", "🔥");

        Assert.Equal(["❤️", "😂", "🔥"], store.MostUsed(3));
    }

    [Fact]
    public void MostUsed_returns_at_most_the_requested_count()
    {
        var store = new FrecencyStore();
        store.RecordUse("a", "❤️");
        store.RecordUse("b", "🔥");

        Assert.Single(store.MostUsed(1));
        Assert.Empty(store.MostUsed(0));
    }

    [Fact]
    public void Snapshot_captures_the_current_decayed_value_and_timestamp_for_each_entry()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var store = new FrecencyStore(time);
        store.RecordUse("love", "❤️");
        time.Advance(RankingWeightsHalfLife);
        store.RecordUse("love", "❤️");

        var snapshot = store.Snapshot();
        var entry = Assert.Single(snapshot.ByQuery, e => e.Query == "love" && e.Emoji == "❤️");

        AssertApprox(store.QueryUses("love", "❤️"), entry.Value);
        Assert.Equal(time.GetUtcNow(), entry.Updated);
    }

    [Fact]
    public void A_store_rebuilt_from_a_snapshot_decays_identically_from_a_brand_new_clock()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var original = new FrecencyStore(time);
        original.RecordUse("love", "❤️");
        time.Advance(RankingWeightsHalfLife);
        var snapshotTakenAt = time.GetUtcNow();
        var snapshot = original.Snapshot();

        // A separate clock, starting exactly where the snapshot was taken — if Snapshot()/FromSnapshot() did not
        // really carry the per-entry Updated timestamp through (e.g. resetting it to "now"), decay computed
        // against this independent clock would diverge from the original's own decay.
        var rebuildTime = new FakeTimeProvider(snapshotTakenAt);
        var rebuilt = FrecencyStore.FromSnapshot(snapshot, rebuildTime);

        AssertApprox(original.QueryUses("love", "❤️"), rebuilt.QueryUses("love", "❤️"));

        time.Advance(RankingWeightsHalfLife);
        rebuildTime.Advance(RankingWeightsHalfLife);
        AssertApprox(original.QueryUses("love", "❤️"), rebuilt.QueryUses("love", "❤️"));
    }

    [Fact]
    public void Clear_discards_both_per_query_and_global_counts()
    {
        var store = new FrecencyStore();
        store.RecordUse("love", "❤️");
        store.RecordUse("", "😂");

        store.Clear();

        Assert.Equal(0, store.QueryUses("love", "❤️"));
        Assert.Equal(0, store.GlobalUses("❤️"));
        Assert.Empty(store.MostUsed(10));
    }

    [Fact]
    public void An_empty_store_snapshots_to_empty_lists()
    {
        var snapshot = new FrecencyStore().Snapshot();

        Assert.Empty(snapshot.ByQuery);
        Assert.Empty(snapshot.Global);
    }

    private static readonly TimeSpan RankingWeightsHalfLife = RankingWeights.FrecencyHalfLife;

    private static void AssertApprox(double expected, double actual, double tolerance = 1e-6) =>
        Assert.True(Math.Abs(expected - actual) < tolerance, $"expected {expected}, got {actual}");
}
