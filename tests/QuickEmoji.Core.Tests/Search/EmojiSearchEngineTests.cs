using Microsoft.Extensions.Time.Testing;
using QuickEmoji.Core.Emoji;
using QuickEmoji.Core.Personalization;
using QuickEmoji.Core.Search;

namespace QuickEmoji.Core.Tests.Search;

public sealed class EmojiSearchEngineTests
{
    private static readonly EmojiCatalog Catalog = EmojiCatalogLoader.LoadEmbedded();

    private static EmojiSearchEngine NewEngine(FrecencyStore? frecency = null) => new(Catalog, frecency ?? new FrecencyStore());

    [Fact]
    public void An_unknown_word_returns_no_results()
    {
        var engine = NewEngine();

        Assert.Empty(engine.Search("zzxxqqnonsenseword"));
    }

    [Fact]
    public void Results_are_capped_at_three()
    {
        var engine = NewEngine();

        var results = engine.Search("face"); // many emoji have "face" as a keyword

        Assert.True(results.Count <= 3);
    }

    [Fact]
    public void Results_never_repeat_the_same_base_emoji()
    {
        var engine = NewEngine();

        var results = engine.Search("heart");

        Assert.Equal(results.Select(r => r.Entry.Emoji).Distinct(StringComparer.Ordinal).Count(), results.Count);
    }

    [Fact]
    public void Every_result_meets_the_minimum_score()
    {
        var engine = NewEngine();

        foreach (var query in new[] { "love", "fire", "xyznosuchword", "group" })
        {
            Assert.All(engine.Search(query), r => Assert.True(r.Score >= RankingWeights.MinimumScore));
        }
    }

    [Fact]
    public void Multi_word_queries_require_every_token_to_match_AND_not_OR()
    {
        var engine = NewEngine();

        // "thumbs" alone matches 👍 and others; "nonexistentxyz" matches nothing, so the AND of both must be empty.
        Assert.Empty(engine.Search("thumbs nonexistentxyz"));
    }

    [Fact]
    public void Multi_word_query_only_returns_entries_matching_both_tokens()
    {
        var engine = NewEngine();

        var results = engine.Search("red heart");

        Assert.NotEmpty(results);
        Assert.Equal("❤️", results[0].Entry.Emoji);
    }

    [Fact]
    public void An_empty_query_returns_the_first_run_defaults_when_nothing_was_used()
    {
        var engine = NewEngine();

        var results = engine.Search("");

        Assert.Equal(RankingWeights.FirstRunDefaults, results.Select(r => r.Entry.Emoji));
    }

    [Fact]
    public void An_empty_query_returns_the_most_recently_frequent_picks()
    {
        var frecency = new FrecencyStore();
        frecency.RecordUse("fire", "🔥");
        frecency.RecordUse("fire", "🔥");
        frecency.RecordUse("fire", "🔥");
        var engine = NewEngine(frecency);

        var results = engine.Search("");

        Assert.Equal("🔥", results[0].Entry.Emoji);
    }

    [Fact]
    public void RecordUse_through_a_skin_tone_variant_credits_the_base_emoji()
    {
        var frecency = new FrecencyStore();
        var engine = NewEngine(frecency);

        engine.RecordUse("thumbs up", "👍🏽");

        Assert.True(Math.Abs(frecency.QueryUses("thumbs up", "👍") - 1) < 1e-6);
    }

    [Fact]
    public void RecordUse_of_an_unknown_string_is_ignored()
    {
        var frecency = new FrecencyStore();
        var engine = NewEngine(frecency);

        engine.RecordUse("whatever", "not an emoji");

        Assert.Equal(0, frecency.GlobalUses("not an emoji"));
    }

    [Fact]
    public void Picking_an_emoji_for_a_query_raises_its_rank_for_that_query()
    {
        var frecency = new FrecencyStore();
        var engine = NewEngine(frecency);

        var before = engine.Search("cat");
        var runnerUp = before.Count > 1 ? before[1].Entry.Emoji : before[0].Entry.Emoji;

        for (var i = 0; i < 10; i++)
        {
            engine.RecordUse("cat", runnerUp);
        }

        var after = engine.Search("cat");
        Assert.Equal(runnerUp, after[0].Entry.Emoji);
    }

    [Fact]
    public void Recording_a_use_changes_the_next_search_for_that_query_even_though_it_was_already_cached()
    {
        var frecency = new FrecencyStore();
        var engine = NewEngine(frecency);

        var before = engine.Search("fire")[0].Score;
        engine.RecordUse("fire", "🔥");
        var after = engine.Search("fire")[0].Score;

        Assert.True(after > before, $"before={before} after={after}");
    }

    [Fact]
    public void Repeated_searches_for_the_same_query_return_consistent_results()
    {
        var engine = NewEngine();

        var first = engine.Search("love");
        var second = engine.Search("love");

        Assert.Equal(first.Select(r => (r.Entry.Emoji, r.Score)), second.Select(r => (r.Entry.Emoji, r.Score)));
    }

    [Fact]
    public void Disabling_a_language_removes_matches_that_only_exist_in_it()
    {
        var engine = NewEngine();

        var bulgarianOnly = engine.Search("сърце", new SearchOptions(SearchLanguages.Bulgarian));
        var englishOnly = engine.Search("сърце", new SearchOptions(SearchLanguages.English));

        Assert.NotEmpty(bulgarianOnly);
        Assert.Empty(englishOnly);
    }

    [Fact]
    public void A_case_insensitive_and_whitespace_different_query_still_matches()
    {
        var engine = NewEngine();

        var results = engine.Search("  LOVE  ");

        Assert.NotEmpty(results);
        Assert.Equal("❤️", results[0].Entry.Emoji);
    }

    [Fact]
    public void Non_name_matches_explain_themselves_and_name_matches_do_not()
    {
        var engine = NewEngine();

        var nameMatch = engine.Search("red heart")[0];
        Assert.False(nameMatch.ExplainsMatch);

        var keywordMatch = engine.Search("lol")[0];
        Assert.True(keywordMatch.ExplainsMatch);
    }
}
