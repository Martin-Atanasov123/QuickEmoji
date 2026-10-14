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

    private static readonly TimeSpan RankingWeightsHalfLife = RankingWeights.FrecencyHalfLife;

    private static void AssertApprox(double expected, double actual, double tolerance = 1e-6) =>
        Assert.True(Math.Abs(expected - actual) < tolerance, $"expected {expected}, got {actual}");
}
