using QuickEmoji.Core.Emoji;

namespace QuickEmoji.Core.Tests.Emoji;

public sealed class SkinToneApplierTests
{
    private static EmojiEntry MakeEntry(IReadOnlyList<EmojiVariant> variants) => new()
    {
        Emoji = "👍",
        Name = "thumbs up",
        Version = new EmojiVersion(6, 0),
        Group = "People & Body",
        Subgroup = "hand-fingers-closed",
        Variants = variants,
    };

    [Fact]
    public void None_preferred_returns_the_base_emoji_even_when_variants_exist()
    {
        var entry = MakeEntry([new EmojiVariant("👍🏽", [SkinTone.Medium], new EmojiVersion(8, 0))]);

        Assert.Equal("👍", SkinToneApplier.Apply(entry, SkinTone.None));
    }

    [Fact]
    public void A_preferred_tone_with_a_matching_uniform_variant_returns_that_variant()
    {
        var entry = MakeEntry([
            new EmojiVariant("👍🏻", [SkinTone.Light], new EmojiVersion(8, 0)),
            new EmojiVariant("👍🏽", [SkinTone.Medium], new EmojiVersion(8, 0)),
        ]);

        Assert.Equal("👍🏽", SkinToneApplier.Apply(entry, SkinTone.Medium));
    }

    [Fact]
    public void A_base_emoji_with_no_variants_falls_back_to_itself_for_any_preference()
    {
        var entry = MakeEntry([]);

        Assert.Equal("👍", SkinToneApplier.Apply(entry, SkinTone.Dark));
    }

    [Fact]
    public void A_preferred_tone_with_no_matching_variant_falls_back_to_the_base()
    {
        var entry = MakeEntry([new EmojiVariant("👍🏻", [SkinTone.Light], new EmojiVersion(8, 0))]);

        Assert.Equal("👍", SkinToneApplier.Apply(entry, SkinTone.Dark));
    }

    [Fact]
    public void A_multi_person_emoji_only_matches_a_variant_that_is_uniformly_the_preferred_tone()
    {
        var entry = MakeEntry([
            new EmojiVariant("🧑🏻‍🤝‍🧑🏿", [SkinTone.Light, SkinTone.Dark], new EmojiVersion(12, 0)),
            new EmojiVariant("🧑🏽‍🤝‍🧑🏽", [SkinTone.Medium, SkinTone.Medium], new EmojiVersion(12, 0)),
        ]);

        Assert.Equal("🧑🏽‍🤝‍🧑🏽", SkinToneApplier.Apply(entry, SkinTone.Medium));
        Assert.Equal("👍", SkinToneApplier.Apply(entry, SkinTone.Dark)); // mixed-tone only, no uniform Dark variant here
    }

    [Fact]
    public void A_null_entry_throws()
    {
        Assert.Throws<ArgumentNullException>(() => SkinToneApplier.Apply(null!, SkinTone.Light));
    }
}
