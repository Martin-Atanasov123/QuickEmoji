using System.Globalization;
using QuickEmoji.Core.Emoji;
using QuickEmoji.Core.Personalization;
using QuickEmoji.Core.Search;

namespace QuickEmoji.Core.Tests.Search;

/// <summary>SPEC §3.6: the golden set defines ranking quality. 100 % top-1 and ≥ 95 % top-3-set cases must pass.</summary>
public sealed class GoldenRankingTests
{
    private static readonly EmojiSearchEngine Engine = new(EmojiCatalogLoader.LoadEmbedded(), new FrecencyStore());

    public static IReadOnlyList<GoldenCase> Cases { get; } = GoldenCase.Load();

    [Fact]
    public void Golden_set_has_at_least_120_cases_covering_every_spec_category()
    {
        Assert.True(Cases.Count >= 120, $"only {Cases.Count} cases");
        foreach (var category in new[] { "brief", "bulgarian", "slang", "typos", "multi-word", "ambiguous" })
        {
            Assert.Contains(Cases, c => c.Category == category);
        }

        foreach (var required in new[] { "love", "fire", "happy", "dog", "coffee", "money", "sad", "party", "сърце", "кафе", "куче", "огън", "тъжен",
                     "lol", "gg", "sus", "goat", "hapy", "cofee", "firre", "thumbs up", "red heart", "face with tears", "bank", "star", "cool" })
        {
            Assert.Contains(Cases, c => c.Query == required);
        }
    }

    [Fact]
    public void Every_top1_expectation_passes()
    {
        var failures = Cases
            .Select(c => (Case: c, Results: Engine.Search(c.Query)))
            .Where(r => r.Results.Count == 0 || r.Results[0].Entry.Emoji != r.Case.Top1)
            .Select(r => Describe(r.Case, r.Results))
            .ToList();

        Assert.True(failures.Count == 0, $"{failures.Count} of {Cases.Count} top-1 cases failed:\n{string.Join("\n", failures)}");
    }

    [Fact]
    public void At_least_95_percent_of_top3_set_expectations_pass()
    {
        var failures = Cases
            .Select(c => (Case: c, Results: Engine.Search(c.Query)))
            .Where(r => !r.Case.Top3.All(e => r.Results.Any(x => x.Entry.Emoji == e)))
            .Select(r => Describe(r.Case, r.Results))
            .ToList();

        var passRate = 1.0 - ((double)failures.Count / Cases.Count);
        Assert.True(passRate >= 0.95, $"top-3 pass rate {passRate:P1}; failures:\n{string.Join("\n", failures)}");
    }

    private static string Describe(GoldenCase c, IReadOnlyList<SearchResult> results) =>
        $"  [{c.Category}] '{c.Query}': expected {c.Top1} / {{{string.Join(" ", c.Top3)}}}, got "
        + string.Join(", ", results.Select(r => string.Create(CultureInfo.InvariantCulture, $"{r.Entry.Emoji} {r.Score:F0} {r.Kind}:{r.MatchedTerm}")));

    public sealed record GoldenCase(string Category, string Query, string Top1, IReadOnlyList<string> Top3)
    {
        public static IReadOnlyList<GoldenCase> Load()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "data", "ranking-golden.tsv");
            var cases = new List<GoldenCase>();
            var category = "";
            foreach (var line in File.ReadLines(path))
            {
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    category = line[3..].Trim();
                    continue;
                }

                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var columns = line.Split('\t');
                if (columns.Length != 3)
                {
                    throw new FormatException($"ranking-golden.tsv: expected 3 tab-separated columns in '{line}'.");
                }

                cases.Add(new GoldenCase(category, columns[0], columns[1], columns[2].Split(' ', StringSplitOptions.RemoveEmptyEntries)));
            }

            return cases;
        }
    }
}
