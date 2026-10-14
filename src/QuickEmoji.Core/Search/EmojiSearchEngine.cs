using QuickEmoji.Core.Emoji;
using QuickEmoji.Core.Personalization;

namespace QuickEmoji.Core.Search;

/// <summary>
/// Ranks base emoji for a query (SPEC §3): tiered lexical score, popularity prior, frecency, minimum score, top 3.
/// The query cache holds frecency-independent candidates, so a pick never leaves a stale entry behind.
/// </summary>
public sealed class EmojiSearchEngine
{
    private readonly SearchIndex _index;
    private readonly FrecencyStore _frecency;
    private readonly LruCache<(SearchLanguages Languages, string Query), Candidate[]> _cache =
        new(RankingWeights.QueryCacheCapacity);

    public EmojiSearchEngine(EmojiCatalog catalog, FrecencyStore frecency)
    {
        ArgumentNullException.ThrowIfNull(frecency);
        _index = SearchIndex.Build(catalog);
        _frecency = frecency;
    }

    public IReadOnlyList<SearchResult> Search(string? query, SearchOptions? options = null)
    {
        options ??= SearchOptions.Default;
        var tokens = QueryNormalizer.Tokenize(query);
        if (tokens.Count == 0)
        {
            return MostUsed();
        }

        var normalized = string.Join(' ', tokens);
        var key = (options.Languages, normalized);
        if (!_cache.TryGet(key, out var candidates))
        {
            candidates = _index.Match(tokens, options.Languages);
            _cache.Set(key, candidates);
        }

        return candidates
            .Select(c => (Candidate: c, Score: c.Lexical + _index.Popularity[c.Entry] + FrecencyBonus(normalized, c.Entry)))
            .Where(s => s.Score >= RankingWeights.MinimumScore)
            .OrderByDescending(s => s.Score)
            .ThenBy(s => _index.Entries[s.Candidate.Entry].PopularityRank ?? int.MaxValue)
            .ThenBy(s => s.Candidate.Entry)
            .Take(RankingWeights.MaxResults)
            .Select(s => new SearchResult(_index.Entries[s.Candidate.Entry], s.Score, s.Candidate.Kind, s.Candidate.Term))
            .ToList();
    }

    /// <summary>
    /// Records that <paramref name="emoji"/> (a base emoji or one of its skin-tone variants) was picked for
    /// <paramref name="query"/>. Unknown emoji are ignored.
    /// </summary>
    public void RecordUse(string? query, string emoji)
    {
        if (_index.EntryByEmoji.TryGetValue(emoji, out var entry))
        {
            _frecency.RecordUse(QueryNormalizer.Normalize(query), _index.Entries[entry].Emoji);
        }
    }

    private double FrecencyBonus(string normalizedQuery, int entry)
    {
        var emoji = _index.Entries[entry].Emoji;
        var weightedUses = (RankingWeights.FrecencyQueryUseWeight * _frecency.QueryUses(normalizedQuery, emoji))
            + (RankingWeights.FrecencyGlobalUseWeight * _frecency.GlobalUses(emoji));
        return RankingWeights.FrecencyMax * (1 - Math.Exp(-weightedUses / RankingWeights.FrecencySaturationUses));
    }

    /// <summary>Empty query: the most picked emoji, topped up with the first-run defaults (SPEC §2).</summary>
    private List<SearchResult> MostUsed()
    {
        var results = new List<SearchResult>(RankingWeights.MaxResults);
        foreach (var emoji in _frecency.MostUsed(RankingWeights.MaxResults).Concat(RankingWeights.FirstRunDefaults))
        {
            if (results.Count == RankingWeights.MaxResults)
            {
                break;
            }

            if (_index.EntryByEmoji.TryGetValue(emoji, out var entry) && results.All(r => r.Entry.Emoji != _index.Entries[entry].Emoji))
            {
                results.Add(new SearchResult(_index.Entries[entry], _frecency.GlobalUses(_index.Entries[entry].Emoji), MatchKind.Recent, ""));
            }
        }

        return results;
    }
}
