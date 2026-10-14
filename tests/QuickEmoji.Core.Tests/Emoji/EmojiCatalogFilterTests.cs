using QuickEmoji.Core.Emoji;

namespace QuickEmoji.Core.Tests.Emoji;

public sealed class EmojiCatalogFilterTests
{
    private static readonly EmojiCatalog Catalog = EmojiCatalogLoader.LoadEmbedded();

    [Fact]
    public void No_filter_keeps_everything()
    {
        var filtered = Catalog.Apply(EmojiCatalogFilter.None);

        Assert.Equal(Catalog.AllSequences().Count(), filtered.AllSequences().Count());
    }

    [Fact]
    public void Max_version_removes_newer_base_emoji_and_variants()
    {
        var max = new EmojiVersion(15, 0);

        var filtered = Catalog.Apply(new EmojiCatalogFilter(MaxVersion: max));

        Assert.All(filtered.AllSequences(), s => Assert.True(s.Version <= max, $"{s.Emoji} is E{s.Version}"));
        Assert.False(filtered.TryGet("🫩", out _)); // E16.0
        Assert.True(filtered.TryGet("🫨", out _)); // E15.0
    }

    [Fact]
    public void Max_version_keeps_an_old_base_but_drops_its_newer_variants()
    {
        var filtered = Catalog.Apply(new EmojiCatalogFilter(MaxVersion: new EmojiVersion(13, 1)));

        Assert.True(filtered.TryGet("🤝", out var handshake)); // base E3.0, variants E14.0
        Assert.Empty(handshake.Variants);
        Assert.False(handshake.SupportsSkinTones);
    }

    [Fact]
    public void Max_version_keeps_exactly_the_pinned_emoji_at_or_below_it()
    {
        var max = new EmojiVersion(12, 1);
        var expected = PinnedEmojiTest.FullyQualified.Count(e => e.Version <= max);

        var filtered = Catalog.Apply(new EmojiCatalogFilter(MaxVersion: max));

        Assert.Equal(expected, filtered.AllSequences().Count());
    }

    [Fact]
    public void Unrenderable_base_emoji_are_removed_with_their_variants()
    {
        var filtered = Catalog.Apply(new EmojiCatalogFilter(Unrenderable: new HashSet<string>(StringComparer.Ordinal) { "👍", "🏴󠁧󠁢󠁳󠁣󠁴󠁿" }));

        Assert.False(filtered.TryGet("👍", out _));
        Assert.False(filtered.TryGet("🏴󠁧󠁢󠁳󠁣󠁴󠁿", out _));
        Assert.DoesNotContain(filtered.AllSequences(), s => s.Emoji.StartsWith("👍", StringComparison.Ordinal));
        Assert.True(filtered.TryGet("❤️", out _));
    }

    [Fact]
    public void Unrenderable_variants_are_removed_individually()
    {
        var filtered = Catalog.Apply(new EmojiCatalogFilter(Unrenderable: new HashSet<string>(StringComparer.Ordinal) { "👍🏽" }));

        Assert.True(filtered.TryGet("👍", out var thumbsUp));
        Assert.Equal(4, thumbsUp.Variants.Count);
        Assert.DoesNotContain(thumbsUp.Variants, v => v.Emoji == "👍🏽");
    }

    [Fact]
    public void Filtering_does_not_change_the_source_catalog()
    {
        var before = Catalog.AllSequences().Count();

        _ = Catalog.Apply(new EmojiCatalogFilter(MaxVersion: new EmojiVersion(1, 0)));

        Assert.Equal(before, Catalog.AllSequences().Count());
    }
}
