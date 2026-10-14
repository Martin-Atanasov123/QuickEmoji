using System.Text;
using QuickEmoji.Core.Emoji;
using QuickEmoji.Core.Emoji.Serialization;
using QuickEmoji.DataBuilder.Building;
using QuickEmoji.DataBuilder.Output;
using QuickEmoji.DataBuilder.Parsing;

namespace QuickEmoji.DataBuilder.Tests;

public sealed class BuildingTests
{
    private static readonly EmojiVersion V06 = new(0, 6);
    private static readonly EmojiVersion V14 = new(14, 0);

    private static EmojiTestRecord R(string emoji, string name, EmojiVersion? version = null) =>
        new(emoji, name, version ?? V06, "People & Body", "test");

    [Fact]
    public void Toned_sequences_join_the_base_with_the_same_untoned_sequence()
    {
        var groups = SkinToneGrouper.Group(
        [
            R("👍", "thumbs up"),
            R("👍🏽", "thumbs up: medium skin tone"),
            R("☝️", "index pointing up"),
            R("☝🏿", "index pointing up: dark skin tone"),
            R("👩‍❤️‍👨", "couple with heart: woman, man"),
            R("👩🏻‍❤️‍👨🏼", "couple with heart: woman, man, light skin tone, medium-light skin tone"),
        ]);

        Assert.Equal(["👍", "☝️", "👩‍❤️‍👨"], groups.Select(g => g.Base.Emoji));
        Assert.Equal("👍🏽", Assert.Single(groups[0].Variants).Record.Emoji);
        Assert.Equal("☝🏿", Assert.Single(groups[1].Variants).Record.Emoji);
        Assert.Equal([SkinTone.Light, SkinTone.MediumLight], Assert.Single(groups[2].Variants).Tones);
    }

    [Fact]
    public void Toned_sequences_without_an_untoned_twin_fall_back_to_the_name_before_the_colon()
    {
        var groups = SkinToneGrouper.Group(
        [
            R("💏", "kiss"),
            R("🧑🏻‍❤️‍💋‍🧑🏼", "kiss: person, person, light skin tone, medium-light skin tone"),
            R("🤝", "handshake"),
            R("🫱🏻‍🫲🏿", "handshake: light skin tone, dark skin tone", V14),
        ]);

        Assert.Equal("🧑🏻‍❤️‍💋‍🧑🏼", Assert.Single(groups[0].Variants).Record.Emoji);
        Assert.Equal("🫱🏻‍🫲🏿", Assert.Single(groups[1].Variants).Record.Emoji);
    }

    [Fact]
    public void A_toned_sequence_without_any_base_fails_the_build()
    {
        Assert.Throws<InvalidDataException>(() => SkinToneGrouper.Group([R("👍🏽", "thumbs up: medium skin tone")]));
    }

    [Fact]
    public void Assembler_merges_sources_and_falls_back_to_the_emoji_test_name()
    {
        var data = Assemble(
            [R("❤️", "red heart"), R("🫩", "face with bags under eyes", new EmojiVersion(16, 0))],
            synonyms: [("ily", ["❤"])],
            popularity: ["❤️"]);

        var heart = data.Emoji[0];
        Assert.Equal("❤️", heart.Emoji);
        Assert.Equal("red heart", heart.Name);
        Assert.Equal("червено сърце", heart.NameBg);
        Assert.Equal(["love", "heart"], heart.Keywords);
        Assert.Equal(["love", "romance"], heart.Tags);
        Assert.Equal(["heart"], heart.Shortcodes);
        Assert.Equal("ily", Assert.Single(heart.Synonyms!).Word);
        Assert.Equal(1, heart.Synonyms![0].Rank);
        Assert.Equal(1, heart.Popularity);

        var uncovered = data.Emoji[1];
        Assert.Equal("face with bags under eyes", uncovered.Name);
        Assert.Null(uncovered.NameBg);
        Assert.Null(uncovered.Keywords);
        Assert.Null(uncovered.Popularity);
    }

    [Fact]
    public void Assembler_records_a_variant_version_only_when_it_differs()
    {
        var data = Assemble([R("🤝", "handshake", new EmojiVersion(3, 0)), R("🤝🏻", "handshake: light skin tone", V14)]);

        var variant = Assert.Single(data.Emoji[0].Variants!);
        Assert.Equal("14.0", variant.Version);
        Assert.Equal([1], variant.Tones);
    }

    [Theory]
    [InlineData("👍🏽")] // carries a skin tone
    [InlineData("🦖🦖")] // not an emoji
    public void Assembler_rejects_synonyms_that_do_not_name_a_base_emoji(string target)
    {
        Assert.Throws<InvalidDataException>(() =>
            Assemble([R("👍", "thumbs up"), R("👍🏽", "thumbs up: medium skin tone")], synonyms: [("ok", [target])]));
    }

    [Fact]
    public void Assembler_rejects_duplicate_popularity_entries_even_with_different_vs16()
    {
        Assert.Throws<InvalidDataException>(() => Assemble([R("❤️", "red heart")], popularity: ["❤️", "❤"]));
    }

    [Fact]
    public void Writer_output_round_trips_through_the_runtime_loader()
    {
        var data = Assemble(
        [
            R("👨‍👩‍👧‍👦", "family: man, woman, girl, boy", new EmojiVersion(2, 0)),
            R("🏴󠁧󠁢󠁳󠁣󠁴󠁿", "flag: Scotland", new EmojiVersion(5, 0)),
            R("1️⃣", "keycap: 1"),
            R("👍", "thumbs up"),
            R("👍🏽", "thumbs up: medium skin tone", new EmojiVersion(1, 0)),
        ]);

        var bytes = EmojiJsonWriter.ToUtf8(data);
        var catalog = EmojiCatalogLoader.Load(new MemoryStream(bytes));

        Assert.Equal(["👨‍👩‍👧‍👦", "🏴󠁧󠁢󠁳󠁣󠁴󠁿", "1️⃣", "👍", "👍🏽"], catalog.AllSequences().Select(s => s.Emoji));
        Assert.Equal(new EmojiVersion(1, 0), catalog.Entries[3].Variants[0].Version);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"emoji\":\"🏴󠁧󠁢󠁳󠁣󠁴󠁿\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', text);
        Assert.NotEqual(0xEF, bytes[0]); // no BOM
    }

    [Theory]
    [InlineData("plain", "\"plain\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData("a\\b", "\"a\\\\b\"")]
    [InlineData("line\nbreak", "\"line\\nbreak\"")]
    [InlineData("\u0001", "\"\\u0001\"")]
    [InlineData("Japanese “here” button", "\"Japanese “here” button\"")]
    public void Writer_escapes_only_what_json_requires(string value, string expected)
    {
        Assert.Equal(expected, EmojiJsonWriter.Quote(value));
    }

    private static EmojiDataFile Assemble(
        IReadOnlyList<EmojiTestRecord> records,
        IReadOnlyList<(string, IReadOnlyList<string>)>? synonyms = null,
        IReadOnlyList<string>? popularity = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? exclusions = null)
    {
        var english = new Dictionary<string, CldrAnnotation>(StringComparer.Ordinal)
        {
            ["❤"] = new("red heart", ["love", "heart"]),
        };
        var bulgarian = new Dictionary<string, CldrAnnotation>(StringComparer.Ordinal)
        {
            ["❤"] = new("червено сърце", ["любов"]),
        };
        var gemoji = new Dictionary<string, GemojiRecord>(StringComparer.Ordinal)
        {
            ["❤"] = new(["heart"], ["love", "romance"]),
        };
        var sources = new SourceData(records, english, bulgarian, gemoji, synonyms ?? [], popularity ?? [], exclusions ?? new Dictionary<string, IReadOnlyList<string>>());
        return EmojiDataAssembler.Assemble(sources, new EmojiDataSources { EmojiTest = "18.0", Cldr = "48.2.3", Gemoji = "4.1.0" });
    }
}
