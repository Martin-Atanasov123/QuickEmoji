using QuickEmoji.Core.Search;

namespace QuickEmoji.Core.Tests.Search;

public sealed class DamerauLevenshteinTests
{
    [Theory]
    [InlineData("happy", "happy", 0)]
    [InlineData("hapy", "happy", 1)] // one deletion
    [InlineData("happyy", "happy", 1)] // one insertion
    [InlineData("hsppy", "happy", 1)] // one substitution
    [InlineData("ahppy", "happy", 1)] // one transposition
    [InlineData("hapy", "happpy", 2)]
    public void IsWithin_accepts_edit_distances_at_or_below_the_bound(string a, string b, int distance)
    {
        Assert.True(DamerauLevenshtein.IsWithin(a, b, distance));
        if (distance > 0)
        {
            Assert.False(DamerauLevenshtein.IsWithin(a, b, distance - 1));
        }
    }

    [Fact]
    public void IsWithin_rejects_distances_above_the_bound()
    {
        Assert.False(DamerauLevenshtein.IsWithin("kitten", "sitting", 2));
        Assert.True(DamerauLevenshtein.IsWithin("kitten", "sitting", 3));
    }

    [Fact]
    public void IsWithin_is_symmetric()
    {
        Assert.Equal(DamerauLevenshtein.IsWithin("cofee", "coffee", 1), DamerauLevenshtein.IsWithin("coffee", "cofee", 1));
    }

    [Fact]
    public void A_length_difference_above_the_bound_fails_fast()
    {
        Assert.False(DamerauLevenshtein.IsWithin("a", "abcdefgh", 2));
    }

    [Fact]
    public void Empty_strings_are_zero_apart()
    {
        Assert.True(DamerauLevenshtein.IsWithin("", "", 0));
    }
}
