using QuickEmoji.Core.Search;

namespace QuickEmoji.Core.Tests.Search;

public sealed class QueryNormalizerTests
{
    [Theory]
    [InlineData("  Love  ", "love")]
    [InlineData("LOVE", "love")]
    [InlineData("  thumbs   up  ", "thumbs up")]
    [InlineData("café", "cafe")]
    [InlineData("naïve", "naive")]
    [InlineData("RÉSUMÉ", "resume")]
    public void Normalize_trims_collapses_whitespace_lowercases_and_strips_latin_diacritics(string input, string expected)
    {
        Assert.Equal(expected, QueryNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("сърце", "сърце")]
    [InlineData("СЪРЦЕ", "сърце")]
    [InlineData("Куче", "куче")]
    public void Normalize_leaves_cyrillic_as_typed_except_case(string input, string expected)
    {
        Assert.Equal(expected, QueryNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData(null, 0)]
    [InlineData("love", 1)]
    [InlineData("thumbs up", 2)]
    [InlineData("  red   heart  ", 2)]
    public void Tokenize_splits_on_whitespace(string? input, int expectedTokenCount)
    {
        Assert.Equal(expectedTokenCount, QueryNormalizer.Tokenize(input).Count);
    }

    [Fact]
    public void Tokenize_splits_on_punctuation_but_keeps_plus_hash_and_asterisk_in_a_token()
    {
        Assert.Equal(["face", "with", "tears"], QueryNormalizer.Tokenize("face, with-tears!"));
        Assert.Equal(["+1"], QueryNormalizer.Tokenize("+1"));
        Assert.Equal(["#1"], QueryNormalizer.Tokenize("#1"));
    }

    [Theory]
    [InlineData("woman's", "womans")]
    [InlineData("don't", "dont")]
    [InlineData("rock’n’roll", "rocknroll")]
    public void Tokenize_drops_apostrophes_rather_than_splitting_on_them(string input, string joined)
    {
        Assert.Equal(joined, string.Concat(QueryNormalizer.Tokenize(input)));
    }

    [Fact]
    public void Normalize_of_empty_or_whitespace_is_empty_string()
    {
        Assert.Equal("", QueryNormalizer.Normalize(""));
        Assert.Equal("", QueryNormalizer.Normalize("   "));
        Assert.Equal("", QueryNormalizer.Normalize(null));
    }
}
