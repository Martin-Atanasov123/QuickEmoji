using QuickEmoji.Core.Emoji;
using QuickEmoji.Platform.Fonts;

namespace QuickEmoji.Platform.Tests.Fonts;

public sealed class FontCoverageCalculatorTests
{
    private static readonly EmojiVersion V15 = new(15, 0);
    private static readonly EmojiVersion V16 = new(16, 0);
    private static readonly EmojiVersion V17 = new(17, 0);

    [Fact]
    public void Cap_is_the_newest_version_that_renders()
    {
        var coverage = FontCoverageCalculator.Compute(
        [
            ("a", V15, true), ("b", V15, true),
            ("c", V16, true), ("d", V16, true),
            ("e", V17, false), ("f", V17, false),
        ]);

        Assert.Equal(V16, coverage.MaxEmojiVersion);
        Assert.Equal(["e", "f"], coverage.Unrenderable.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_few_failures_in_an_old_version_do_not_lower_the_cap_but_are_excluded()
    {
        // Mirrors the measured Windows 11 result: three tag flags (E5.0) fail while E5.0 as a whole renders.
        var results = Enumerable.Range(0, 97).Select(i => ($"ok{i}", new EmojiVersion(5, 0), true))
            .Concat([("🏴󠁧󠁢󠁥󠁮󠁧󠁿", new EmojiVersion(5, 0), false), ("🏴󠁧󠁢󠁳󠁣󠁴󠁿", new EmojiVersion(5, 0), false), ("🏴󠁧󠁢󠁷󠁬󠁳󠁿", new EmojiVersion(5, 0), false)]);

        var coverage = FontCoverageCalculator.Compute(results);

        Assert.Equal(new EmojiVersion(5, 0), coverage.MaxEmojiVersion);
        Assert.Equal(3, coverage.Unrenderable.Count);
        Assert.Contains("🏴󠁧󠁢󠁳󠁣󠁴󠁿", coverage.Unrenderable);
    }

    [Fact]
    public void A_version_below_the_threshold_does_not_set_the_cap()
    {
        var results = Enumerable.Range(0, 8).Select(i => ($"x{i}", V17, i < 7)) // 7 of 8 = 87.5 %
            .Append(("y", V16, true));

        var coverage = FontCoverageCalculator.Compute(results);

        Assert.Equal(V16, coverage.MaxEmojiVersion);
    }

    [Fact]
    public void Nothing_renderable_means_no_cap_and_everything_excluded()
    {
        var coverage = FontCoverageCalculator.Compute([("a", V15, false)]);

        Assert.Null(coverage.MaxEmojiVersion);
        Assert.Single(coverage.Unrenderable);
    }

    [Fact]
    public void ToFilter_carries_both_rules()
    {
        var coverage = new FontCoverage(V16, new HashSet<string>(StringComparer.Ordinal) { "z" });

        var filter = coverage.ToFilter();

        Assert.True(filter.Allows("a", V16));
        Assert.False(filter.Allows("a", V17));
        Assert.False(filter.Allows("z", V15));
    }
}
