using System.Diagnostics;
using System.Text;
using QuickEmoji.Core.Emoji;

namespace QuickEmoji.Core.Tests.Emoji;

/// <summary>Stage 1 acceptance tests against the committed emoji.json.</summary>
public sealed class EmojiCatalogDataTests
{
    private static readonly EmojiCatalog Catalog = EmojiCatalogLoader.LoadEmbedded();

    [Fact]
    public void Every_fully_qualified_emoji_of_the_pinned_version_loads_exactly_once()
    {
        var loaded = Catalog.AllSequences().Select(s => s.Emoji).ToList();
        var expected = PinnedEmojiTest.FullyQualified.Select(e => e.Emoji).ToList();

        Assert.Equal(3963, expected.Count);
        Assert.Equal(expected.Count, loaded.Count);
        Assert.Equal(loaded.Count, loaded.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(expected.Except(loaded, StringComparer.Ordinal));
    }

    [Fact]
    public void Every_sequence_keeps_its_emoji_test_version()
    {
        var versions = Catalog.AllSequences().ToDictionary(s => s.Emoji, s => s.Version, StringComparer.Ordinal);

        foreach (var (emoji, version, name) in PinnedEmojiTest.FullyQualified)
        {
            Assert.True(versions[emoji] == version, $"{name}: expected E{version}, loaded E{versions[emoji]}");
        }
    }

    [Fact]
    public void Base_emoji_never_carry_a_skin_tone()
    {
        Assert.All(Catalog.Entries, e => Assert.Empty(SkinTones.In(e.Emoji)));
    }

    [Fact]
    public void Every_variant_carries_the_tones_of_its_own_sequence()
    {
        foreach (var entry in Catalog.Entries)
        {
            foreach (var variant in entry.Variants)
            {
                Assert.Equal(SkinTones.In(variant.Emoji), variant.Tones);
            }
        }
    }

    [Theory]
    [InlineData("👍", "👍🏽", new[] { SkinTone.Medium })]
    [InlineData("☝️", "☝🏿", new[] { SkinTone.Dark })]
    [InlineData("👩‍❤️‍👨", "👩🏻‍❤️‍👨🏼", new[] { SkinTone.Light, SkinTone.MediumLight })]
    [InlineData("🧑‍🤝‍🧑", "🧑🏻‍🤝‍🧑🏿", new[] { SkinTone.Light, SkinTone.Dark })]
    [InlineData("💏", "🧑🏻‍❤️‍💋‍🧑🏼", new[] { SkinTone.Light, SkinTone.MediumLight })]
    [InlineData("🤝", "🫱🏻‍🫲🏿", new[] { SkinTone.Light, SkinTone.Dark })]
    [InlineData("👨‍🦰", "👨🏽‍🦰", new[] { SkinTone.Medium })]
    public void Skin_tone_variants_group_under_their_base(string baseEmoji, string variantEmoji, SkinTone[] tones)
    {
        Assert.True(Catalog.TryGet(baseEmoji, out var entry), $"{baseEmoji} is not a base emoji");

        var variant = Assert.Single(entry.Variants, v => v.Emoji == variantEmoji);
        Assert.Equal(tones, variant.Tones);
    }

    [Fact]
    public void Single_person_emoji_have_one_uniform_variant_per_tone()
    {
        Assert.True(Catalog.TryGet("👍", out var thumbsUp));

        Assert.Equal(5, thumbsUp.Variants.Count);
        foreach (var tone in Enum.GetValues<SkinTone>().Where(t => t != SkinTone.None))
        {
            Assert.Single(thumbsUp.Variants, v => v.IsUniform(tone));
        }
    }

    [Fact]
    public void Emoji_without_skin_tones_have_no_variants()
    {
        Assert.True(Catalog.TryGet("❤️", out var heart));

        Assert.False(heart.SupportsSkinTones);
    }

    [Fact]
    public void A_variant_newer_than_its_base_keeps_its_own_version()
    {
        Assert.True(Catalog.TryGet("🤝", out var handshake));

        Assert.Equal(new EmojiVersion(3, 0), handshake.Version);
        Assert.All(handshake.Variants, v => Assert.Equal(new EmojiVersion(14, 0), v.Version));
    }

    [Theory]
    [InlineData(new[] { 0x1F468, 0x200D, 0x1F469, 0x200D, 0x1F467, 0x200D, 0x1F466 })] // 👨‍👩‍👧‍👦
    [InlineData(new[] { 0x1F44D, 0x1F3FD })] // 👍🏽
    [InlineData(new[] { 0x1F3F3, 0xFE0F, 0x200D, 0x1F308 })] // 🏳️‍🌈
    [InlineData(new[] { 0x1F3F4, 0xE0067, 0xE0062, 0xE0073, 0xE0063, 0xE0074, 0xE007F })] // 🏴󠁧󠁢󠁳󠁣󠁴󠁿
    [InlineData(new[] { 0x0031, 0xFE0F, 0x20E3 })] // 1️⃣
    [InlineData(new[] { 0x2764, 0xFE0F })] // ❤️
    public void Sequences_round_trip_byte_for_byte(int[] codePoints)
    {
        var expected = PinnedEmojiTest.Sequence(codePoints);

        var loaded = Catalog.AllSequences().Single(s => s.Emoji == expected).Emoji;

        Assert.Equal(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(loaded));
    }

    [Fact]
    public void Every_zwj_and_tag_sequence_round_trips_byte_for_byte()
    {
        var loaded = Catalog.AllSequences().Select(s => s.Emoji).ToHashSet(StringComparer.Ordinal);
        var complex = PinnedEmojiTest.FullyQualified
            .Select(e => e.Emoji)
            .Where(e => e.Contains('‍', StringComparison.Ordinal) || e.EnumerateRunes().Any(r => r.Value is >= 0xE0020 and <= 0xE007F))
            .ToList();

        Assert.NotEmpty(complex);
        Assert.All(complex, e => Assert.Contains(e, loaded));
    }

    [Fact]
    public void Every_base_emoji_has_an_english_name_and_bulgarian_coverage_is_near_complete()
    {
        Assert.All(Catalog.Entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Name)));
        Assert.True(Catalog.Entries.Count(e => e.NameBg is not null) >= Catalog.Entries.Count - 20);
    }

    [Fact]
    public void Curated_data_is_merged()
    {
        Assert.True(Catalog.TryGet("😂", out var tears));
        Assert.True(Catalog.TryGet("👍", out var thumbsUp));

        Assert.Contains(tears.Synonyms, s => s.Word == "lol" && s.Rank == 1);
        Assert.Contains(thumbsUp.Synonyms, s => s.Word == "ок" && s.Rank == 2);
        Assert.Contains("thumbsup", thumbsUp.Shortcodes);
        Assert.Contains("палец", thumbsUp.KeywordsBg);
        Assert.Equal(1, tears.PopularityRank);
        Assert.True(Catalog.Entries.Count(e => e.PopularityRank is not null) >= 500);
        Assert.True(Catalog.Entries.Sum(e => e.Synonyms.Count) >= 150);
    }

    [Fact]
    public void Index_loads_within_150_ms()
    {
        // .NET's tiered JIT needs more than one call before a method reaches its optimized (Tier 1) form; a
        // single warm-up call left this test flaky on loaded CI hardware (still running unoptimized Tier 0 code,
        // or caught by a GC/scheduler pause). 10 warm-up loads give JIT tiering room to settle; 11 measured loads
        // and the median (not the mean) keep one remaining GC-pause outlier from failing the budget by itself.
        for (var i = 0; i < 10; i++)
        {
            _ = EmojiCatalogLoader.LoadEmbedded();
        }

        var timings = new List<double>();
        for (var i = 0; i < 11; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            _ = EmojiCatalogLoader.LoadEmbedded();
            timings.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        var median = timings.Order().ElementAt(timings.Count / 2);
        Assert.True(median <= 150, $"Median load time {median:F1} ms exceeds 150 ms ({string.Join(", ", timings.Select(t => t.ToString("F1")))}).");
    }
}
