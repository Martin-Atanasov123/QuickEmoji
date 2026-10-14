using QuickEmoji.Core.Emoji;

namespace QuickEmoji.Core.Search;

/// <summary>One emoji's sources for one term, kept per language so a disabled language can be ignored.</summary>
internal readonly record struct Posting(int Entry, TermSource English, TermSource Bulgarian, int SynonymRank)
{
    public TermSource For(SearchLanguages languages) =>
        ((languages & SearchLanguages.English) != 0 ? English : TermSource.None)
        | ((languages & SearchLanguages.Bulgarian) != 0 ? Bulgarian : TermSource.None);
}

/// <summary>A lexical match before popularity and frecency are added.</summary>
internal readonly record struct Candidate(int Entry, double Lexical, MatchKind Kind, string Term);

/// <summary>
/// Immutable inverted index over the catalog: exact lookup by term, prefix scan over the sorted term array, and
/// length buckets for bounded fuzzy matching. Built once at startup.
/// </summary>
internal sealed class SearchIndex
{
    private readonly string[] _terms;
    private readonly Posting[][] _postings;
    private readonly Dictionary<string, int> _termIndex;
    private readonly int[][] _fuzzyTermsByLength;
    private readonly string[] _englishNames;
    private readonly string?[] _bulgarianNames;

    private SearchIndex(
        EmojiEntry[] entries,
        double[] popularity,
        Dictionary<string, int> entryByEmoji,
        string[] terms,
        Posting[][] postings,
        string[] englishNames,
        string?[] bulgarianNames)
    {
        Entries = entries;
        Popularity = popularity;
        EntryByEmoji = entryByEmoji;
        _terms = terms;
        _postings = postings;
        _englishNames = englishNames;
        _bulgarianNames = bulgarianNames;
        _termIndex = new Dictionary<string, int>(terms.Length, StringComparer.Ordinal);
        for (var i = 0; i < terms.Length; i++)
        {
            _termIndex[terms[i]] = i;
        }

        var maxLength = terms.Length == 0 ? 0 : terms.Max(t => t.Length);
        _fuzzyTermsByLength = Enumerable.Range(0, maxLength + 1)
            .Select(length => Enumerable.Range(0, terms.Length)
                .Where(i => terms[i].Length == length && !terms[i].Contains(' ', StringComparison.Ordinal))
                .ToArray())
            .ToArray();
    }

    public IReadOnlyList<EmojiEntry> Entries { get; }

    /// <summary>Popularity prior per entry, 0..<see cref="RankingWeights.PopularityMax"/>.</summary>
    public IReadOnlyList<double> Popularity { get; }

    /// <summary>Base emoji and every skin-tone variant, mapped to the base entry's index.</summary>
    public IReadOnlyDictionary<string, int> EntryByEmoji { get; }

    public static SearchIndex Build(EmojiCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var entries = catalog.Entries.ToArray();
        var sources = new Dictionary<string, Dictionary<int, PostingBuilder>>(StringComparer.Ordinal);
        var englishNames = new string[entries.Length];
        var bulgarianNames = new string?[entries.Length];
        var entryByEmoji = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            entryByEmoji[entry.Emoji] = i;
            foreach (var variant in entry.Variants)
            {
                entryByEmoji[variant.Emoji] = i;
            }

            englishNames[i] = QueryNormalizer.Normalize(entry.Name);
            bulgarianNames[i] = entry.NameBg is null ? null : QueryNormalizer.Normalize(entry.NameBg);

            AddPhrase(sources, i, entry.Name, TermSource.EnglishName, TermSource.NameToken, bulgarian: false);
            if (entry.NameBg is not null)
            {
                AddPhrase(sources, i, entry.NameBg, TermSource.BulgarianName, TermSource.NameToken, bulgarian: true);
            }

            foreach (var shortcode in entry.Shortcodes)
            {
                AddPhrase(sources, i, shortcode, TermSource.Shortcode, TermSource.ShortcodeToken, bulgarian: false);
            }

            foreach (var keyword in entry.Keywords)
            {
                AddPhrase(sources, i, keyword, TermSource.Keyword, TermSource.KeywordToken, bulgarian: false);
            }

            foreach (var keyword in entry.KeywordsBg)
            {
                AddPhrase(sources, i, keyword, TermSource.Keyword, TermSource.KeywordToken, bulgarian: true);
            }

            foreach (var tag in entry.Tags)
            {
                AddPhrase(sources, i, tag, TermSource.Tag, TermSource.KeywordToken, bulgarian: false);
            }

            foreach (var synonym in entry.Synonyms)
            {
                AddPhrase(sources, i, synonym.Word, TermSource.Synonym, TermSource.None, bulgarian: IsCyrillic(synonym.Word), synonym.Rank);
            }

            foreach (var word in QueryNormalizer.Tokenize(entry.Group).Concat(QueryNormalizer.Tokenize(entry.Subgroup)))
            {
                Add(sources, i, word, TermSource.GroupWord, bulgarian: false);
            }
        }

        var terms = sources.Keys.Order(StringComparer.Ordinal).ToArray();
        var postings = terms
            .Select(t => sources[t].Select(p => new Posting(p.Key, p.Value.English, p.Value.Bulgarian, p.Value.SynonymRank)).ToArray())
            .ToArray();

        return new SearchIndex(entries, PopularityPrior(entries), entryByEmoji, terms, postings, englishNames, bulgarianNames);
    }

    /// <summary>Every entry that matches all tokens (AND), scored by the mean of the per-token tiers (SPEC §3.4).</summary>
    public Candidate[] Match(IReadOnlyList<string> tokens, SearchLanguages languages)
    {
        var perToken = new Dictionary<int, Candidate>[tokens.Count];
        for (var t = 0; t < tokens.Count; t++)
        {
            perToken[t] = MatchToken(tokens[t], languages);
        }

        var smallest = perToken.MinBy(m => m.Count)!;
        var result = new Dictionary<int, Candidate>(smallest.Count);
        foreach (var entry in smallest.Keys)
        {
            var sum = 0.0;
            Candidate? weakest = null;
            var all = true;
            foreach (var map in perToken)
            {
                if (!map.TryGetValue(entry, out var match))
                {
                    all = false;
                    break;
                }

                sum += match.Lexical;
                if (weakest is null || match.Lexical < weakest.Value.Lexical)
                {
                    weakest = match;
                }
            }

            if (all)
            {
                result[entry] = weakest!.Value with { Lexical = sum / perToken.Length };
            }
        }

        if (tokens.Count > 1)
        {
            ApplyPhrase(string.Join(' ', tokens), languages, result);
        }

        return [.. result.Values];
    }

    private void ApplyPhrase(string phrase, SearchLanguages languages, Dictionary<int, Candidate> result)
    {
        if (_termIndex.TryGetValue(phrase, out var termIndex))
        {
            foreach (var posting in _postings[termIndex])
            {
                var source = posting.For(languages);
                var (score, kind) = TermTiers.Exact(source, posting.SynonymRank);
                if (score > 0 && (!result.TryGetValue(posting.Entry, out var existing) || existing.Lexical < score))
                {
                    result[posting.Entry] = new Candidate(posting.Entry, score, kind, phrase);
                }
            }
        }

        foreach (var entry in result.Keys.ToArray())
        {
            var englishPrefix = (languages & SearchLanguages.English) != 0 && _englishNames[entry].StartsWith(phrase, StringComparison.Ordinal);
            var bulgarianPrefix = (languages & SearchLanguages.Bulgarian) != 0 && _bulgarianNames[entry]?.StartsWith(phrase, StringComparison.Ordinal) == true;
            if (englishPrefix || bulgarianPrefix)
            {
                var candidate = result[entry];
                result[entry] = candidate with
                {
                    Lexical = candidate.Lexical + RankingWeights.PhrasePrefixOfNameBonus,
                    Kind = MatchKind.Name,
                    Term = englishPrefix ? _englishNames[entry] : _bulgarianNames[entry]!,
                };
            }
        }
    }

    private Dictionary<int, Candidate> MatchToken(string token, SearchLanguages languages)
    {
        var best = new Dictionary<int, Candidate>();
        var exactIndex = _termIndex.TryGetValue(token, out var found) ? found : -1;
        if (exactIndex >= 0)
        {
            Offer(best, exactIndex, languages, TermTiers.Exact);
        }

        var start = Array.BinarySearch(_terms, token, StringComparer.Ordinal);
        for (var i = start >= 0 ? start + 1 : ~start; i < _terms.Length && _terms[i].StartsWith(token, StringComparison.Ordinal); i++)
        {
            Offer(best, i, languages, static (s, _) => TermTiers.Prefix(s));
        }

        if (token.Length >= RankingWeights.FuzzyMinTokenLength)
        {
            OfferFuzzy(best, token, languages);
        }

        return best;
    }

    private void OfferFuzzy(Dictionary<int, Candidate> best, string token, SearchLanguages languages)
    {
        var maxEdits = token.Length <= RankingWeights.FuzzyOneEditMaxTokenLength ? 1 : 2;
        var from = Math.Max(0, token.Length - maxEdits);
        var to = Math.Min(_fuzzyTermsByLength.Length - 1, token.Length + maxEdits);
        for (var length = from; length <= to; length++)
        {
            foreach (var termIndex in _fuzzyTermsByLength[length])
            {
                var term = _terms[termIndex];
                if (term.StartsWith(token, StringComparison.Ordinal) || !DamerauLevenshtein.IsWithin(token, term, maxEdits))
                {
                    continue;
                }

                Offer(best, termIndex, languages, TermTiers.Fuzzy);
            }
        }
    }

    private void Offer(
        Dictionary<int, Candidate> best,
        int termIndex,
        SearchLanguages languages,
        Func<TermSource, int, (double Score, MatchKind Kind)> tier)
    {
        foreach (var posting in _postings[termIndex])
        {
            var source = posting.For(languages);
            if (source == TermSource.None)
            {
                continue;
            }

            var (score, kind) = tier(source, posting.SynonymRank);
            if (score > 0 && (!best.TryGetValue(posting.Entry, out var existing) || existing.Lexical < score))
            {
                best[posting.Entry] = new Candidate(posting.Entry, score, kind, _terms[termIndex]);
            }
        }
    }

    private static void AddPhrase(
        Dictionary<string, Dictionary<int, PostingBuilder>> sources,
        int entry,
        string text,
        TermSource whole,
        TermSource token,
        bool bulgarian,
        int synonymRank = 0)
    {
        var tokens = QueryNormalizer.Tokenize(text);
        if (tokens.Count == 0)
        {
            return;
        }

        Add(sources, entry, string.Join(' ', tokens), whole, bulgarian, synonymRank);
        if (token != TermSource.None)
        {
            foreach (var word in tokens)
            {
                Add(sources, entry, word, token, bulgarian);
            }
        }
    }

    private static void Add(
        Dictionary<string, Dictionary<int, PostingBuilder>> sources,
        int entry,
        string term,
        TermSource source,
        bool bulgarian,
        int synonymRank = 0)
    {
        if (!sources.TryGetValue(term, out var byEntry))
        {
            sources[term] = byEntry = [];
        }

        byEntry.TryGetValue(entry, out var current);
        byEntry[entry] = new PostingBuilder(
            bulgarian ? current.English : current.English | source,
            bulgarian ? current.Bulgarian | source : current.Bulgarian,
            synonymRank == 0 ? current.SynonymRank : current.SynonymRank == 0 ? synonymRank : Math.Min(current.SynonymRank, synonymRank));
    }

    private readonly record struct PostingBuilder(TermSource English, TermSource Bulgarian, int SynonymRank);

    /// <summary>Linear prior: rank 1 earns <see cref="RankingWeights.PopularityMax"/>, the last listed rank almost 0.</summary>
    private static double[] PopularityPrior(EmojiEntry[] entries)
    {
        var maxRank = entries.Max(e => e.PopularityRank ?? 0);
        return entries
            .Select(e => e.PopularityRank is { } rank && maxRank > 0
                ? RankingWeights.PopularityMax * (maxRank - rank + 1) / maxRank
                : 0)
            .ToArray();
    }

    private static bool IsCyrillic(string text) => text.Any(c => c is >= 'Ѐ' and <= 'ԯ');
}
