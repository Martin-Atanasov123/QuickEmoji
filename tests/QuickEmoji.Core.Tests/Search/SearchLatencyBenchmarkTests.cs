using QuickEmoji.Core.Emoji;
using QuickEmoji.Core.Personalization;
using QuickEmoji.Core.Search;

namespace QuickEmoji.Core.Tests.Search;

/// <summary>SPEC §3.5: per-query search latency at p95 must be 5 ms or less.</summary>
public sealed class SearchLatencyBenchmarkTests
{
    // A mix of hit shapes: single-word exact, prefix-heavy, multi-word AND, fuzzy, empty, and a miss (worst case:
    // every token scans the whole fuzzy band since nothing matches exactly or by prefix).
    private static readonly string[] Queries =
    [
        "love", "fire", "happy", "dog", "coffee", "thumbs up", "red heart", "face with tears",
        "hapy", "cofee", "firre", "birthay", "сърце", "кафе", "l", "fa", "smi", "", "zzxxqqnonsenseword",
    ];

    [Fact]
    public void P95_query_latency_is_at_most_5_milliseconds()
    {
        var engine = new EmojiSearchEngine(EmojiCatalogLoader.LoadEmbedded(), new FrecencyStore());

        // Warm the JIT and the LRU cache; the budget is for steady-state search, matching how the popup behaves
        // once it has been shown (results are re-queried on every keystroke of queries the user is still typing).
        foreach (var query in Queries)
        {
            engine.Search(query);
        }

        var timings = new List<double>();
        for (var i = 0; i < 500; i++)
        {
            var query = Queries[i % Queries.Length];
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            engine.Search(query);
            timings.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        timings.Sort();
        var p95 = timings[(int)(timings.Count * 0.95)];
        Assert.True(p95 <= 5.0, $"p95 {p95:F3} ms exceeds the 5 ms budget. Timings (ms): {string.Join(", ", timings.Select(t => t.ToString("F2")))}");
    }

    [Fact]
    public void P95_latency_for_cold_never_before_seen_queries_is_at_most_5_milliseconds()
    {
        // Every query here is distinct and not pre-warmed, so the cache never helps: this is the uncached path.
        var engine = new EmojiSearchEngine(EmojiCatalogLoader.LoadEmbedded(), new FrecencyStore());
        var cold = Enumerable.Range(0, 300).Select(i => $"nonrepeatingquery{i}xyz").ToArray();

        var timings = new List<double>(cold.Length);
        foreach (var query in cold)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            engine.Search(query);
            timings.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        timings.Sort();
        var p95 = timings[(int)(timings.Count * 0.95)];
        Assert.True(p95 <= 5.0, $"cold p95 {p95:F3} ms exceeds the 5 ms budget.");
    }
}
