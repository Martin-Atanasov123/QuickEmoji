using System.Text;
using QuickEmoji.Core.Emoji;
using QuickEmoji.DataBuilder.Parsing;

namespace QuickEmoji.DataBuilder.Tests;

public sealed class ParserTests
{
    private const string EmojiTestFixture = """
        # emoji-test.txt fixture
        # group: Smileys & Emotion

        # subgroup: face-smiling
        1F600                                                  ; fully-qualified     # 😀 E1.0 grinning face

        # subgroup: heart
        2764 FE0F                                              ; fully-qualified     # ❤️ E0.6 red heart
        2764                                                   ; unqualified         # ❤ E0.6 red heart

        # group: People & Body
        # subgroup: hand-fingers-closed
        1F44D                                                  ; fully-qualified     # 👍 E0.6 thumbs up
        1F44D 1F3FD                                            ; fully-qualified     # 👍🏽 E1.0 thumbs up: medium skin tone
        1F3FB                                                  ; component           # 🏻 E1.0 light skin tone

        # Status Counts
        # fully-qualified : 4
        """;

    [Fact]
    public void EmojiTest_keeps_fully_qualified_lines_with_group_and_version()
    {
        var records = EmojiTestParser.ParseFullyQualified(new StringReader(EmojiTestFixture));

        Assert.Equal(["😀", "❤️", "👍", "👍🏽"], records.Select(r => r.Emoji));
        Assert.Equal(new EmojiTestRecord("❤️", "red heart", new EmojiVersion(0, 6), "Smileys & Emotion", "heart"), records[1]);
        Assert.Equal("People & Body", records[3].Group);
        Assert.Equal("thumbs up: medium skin tone", records[3].Name);
        Assert.Equal(new EmojiVersion(1, 0), records[3].Version);
    }

    [Fact]
    public void EmojiTest_builds_the_string_from_code_points_including_vs16()
    {
        var records = EmojiTestParser.ParseFullyQualified(new StringReader(EmojiTestFixture));

        Assert.Equal("❤️", records[1].Emoji);
    }

    [Fact]
    public void EmojiTest_rejects_a_glyph_that_disagrees_with_the_code_points()
    {
        const string bad = "# group: g\n# subgroup: s\n1F600 ; fully-qualified # 😃 E1.0 grinning face\n";

        Assert.Throws<FormatException>(() => EmojiTestParser.ParseFullyQualified(new StringReader(bad)));
    }

    [Theory]
    [InlineData("1F600 ; fully-qualified # 😀 E1.0 grinning face\n")] // no group header
    [InlineData("# group: g\n# subgroup: s\nZZZZ ; fully-qualified # x E1.0 bad\n")]
    [InlineData("# group: g\n# subgroup: s\n1F600 ; fully-qualified # 😀 grinning face\n")]
    [InlineData("# group: g\n# subgroup: s\n1F600 fully-qualified\n")]
    public void EmojiTest_rejects_malformed_lines(string text)
    {
        Assert.Throws<FormatException>(() => EmojiTestParser.ParseFullyQualified(new StringReader(text)));
    }

    [Fact]
    public void Cldr_keys_are_stored_without_vs16_and_merge_prefers_base_annotations()
    {
        const string annotations = """{"annotations":{"identity":{},"annotations":{"❤":{"default":["love","heart"],"tts":["red heart"]}}}}""";
        const string derived = """{"annotationsDerived":{"identity":{},"annotations":{"❤":{"default":["x"],"tts":["wrong"]},"👍🏽":{"default":["+1"],"tts":["thumbs up: medium skin tone"]}}}}""";

        var merged = CldrAnnotationParser.Merge(CldrAnnotationParser.Parse(Utf8(annotations)), CldrAnnotationParser.Parse(Utf8(derived)));

        Assert.Equal(new[] { "love", "heart" }, merged["❤"].Keywords);
        Assert.Equal("red heart", merged["❤"].Name);
        Assert.Equal("thumbs up: medium skin tone", merged["👍🏽"].Name);
    }

    [Fact]
    public void Cldr_rejects_a_file_without_an_annotations_root()
    {
        Assert.Throws<InvalidDataException>(() => CldrAnnotationParser.Parse(Utf8("""{"other":{}}""")));
    }

    [Fact]
    public void Gemoji_maps_aliases_and_tags_by_vs16_free_key()
    {
        const string json = """[{"emoji":"❤️","aliases":["heart"],"tags":["love"]},{"emoji":"👍","aliases":["+1","thumbsup"]}]""";

        var gemoji = GemojiParser.Parse(Utf8(json));

        Assert.Equal(["heart"], gemoji["❤"].Aliases);
        Assert.Equal(["love"], gemoji["❤"].Tags);
        Assert.Empty(gemoji["👍"].Tags);
    }

    [Fact]
    public void Synonyms_keep_order_and_targets()
    {
        var synonyms = CuratedDataParser.ParseSynonyms(Utf8("""{ "gg": ["🤝", "🏆"], "ок": ["👌"] }"""));

        Assert.Equal("gg", synonyms[0].Word);
        Assert.Equal(["🤝", "🏆"], synonyms[0].Emoji);
        Assert.Equal("ок", synonyms[1].Word);
    }

    [Theory]
    [InlineData("""{ "LOL": ["😂"] }""")]
    [InlineData("""{ " lol": ["😂"] }""")]
    [InlineData("""{ "lol": [] }""")]
    [InlineData("""{ "lol": ["😂"], "lol": ["🤣"] }""")]
    [InlineData("""{ "ОК": ["👌"] }""")]
    public void Synonyms_reject_invalid_entries(string json)
    {
        Assert.Throws<InvalidDataException>(() => CuratedDataParser.ParseSynonyms(Utf8(json)));
    }

    [Fact]
    public void Popularity_reads_the_order_array()
    {
        var order = CuratedDataParser.ParsePopularity(Utf8("""{ "comment": "x", "order": ["😂", "❤️"] }"""));

        Assert.Equal(["😂", "❤️"], order);
    }

    private static MemoryStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));
}
