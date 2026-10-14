namespace QuickEmoji.Core.Search;

/// <summary>Where an index term came from, for one emoji. Several flags combine when sources agree.</summary>
[Flags]
internal enum TermSource : ushort
{
    None = 0,

    /// <summary>The whole English CLDR name.</summary>
    EnglishName = 1 << 0,

    /// <summary>The whole Bulgarian CLDR name. Scored as a keyword (see DECISIONS.md).</summary>
    BulgarianName = 1 << 1,

    /// <summary>A word of the English or Bulgarian name.</summary>
    NameToken = 1 << 2,

    /// <summary>A whole gemoji alias.</summary>
    Shortcode = 1 << 3,

    /// <summary>A word of a multi-word gemoji alias (family_man_woman).</summary>
    ShortcodeToken = 1 << 4,

    /// <summary>A whole CLDR keyword.</summary>
    Keyword = 1 << 5,

    /// <summary>A whole gemoji tag.</summary>
    Tag = 1 << 6,

    /// <summary>A word of a multi-word keyword or tag.</summary>
    KeywordToken = 1 << 7,

    Synonym = 1 << 8,

    /// <summary>A word of the Unicode group or subgroup.</summary>
    GroupWord = 1 << 9,
}

/// <summary>Maps <see cref="TermSource"/> flags to tiers. All scores come from <see cref="RankingWeights"/>.</summary>
internal static class TermTiers
{
    private const TermSource NameFamily = TermSource.EnglishName | TermSource.BulgarianName | TermSource.NameToken;
    private const TermSource GemojiFamily = TermSource.Shortcode | TermSource.ShortcodeToken | TermSource.Tag;
    private const TermSource KeywordMatch = TermSource.Keyword | TermSource.Tag | TermSource.BulgarianName;

    /// <summary>The query token equals this term. <paramref name="synonymRank"/> is 0 unless the term is a synonym.</summary>
    public static (double Score, MatchKind Kind) Exact(TermSource s, int synonymRank)
    {
        var lexical = ExactWithoutSynonym(s);
        if ((s & TermSource.Synonym) != 0)
        {
            var synonym = RankingWeights.ExactSynonym - ((synonymRank - 1) * RankingWeights.SynonymRankStep);
            if (synonym > lexical.Score)
            {
                return (synonym, MatchKind.Synonym);
            }
        }

        return lexical;
    }

    /// <summary>The query token is a proper prefix of this term. Synonyms and group words never prefix-match.</summary>
    public static (double Score, MatchKind Kind) Prefix(TermSource s)
    {
        if ((s & NameFamily) != 0)
        {
            return (RankingWeights.NameTokenPrefix, MatchKind.Name);
        }

        if ((s & (TermSource.Keyword | TermSource.Tag | TermSource.KeywordToken)) != 0)
        {
            return (RankingWeights.KeywordPrefix, MatchKind.Keyword);
        }

        return (s & (TermSource.Shortcode | TermSource.ShortcodeToken)) != 0
            ? (RankingWeights.KeywordPrefix, MatchKind.Shortcode)
            : (0, MatchKind.Name);
    }

    /// <summary>The query token is a near miss of this term: placed inside the fuzzy band by the term's exact tier.</summary>
    public static (double Score, MatchKind Kind) Fuzzy(TermSource s, int synonymRank)
    {
        if ((s & ~TermSource.GroupWord) == 0)
        {
            return (0, MatchKind.Fuzzy);
        }

        var exact = Exact(s, synonymRank).Score;
        var strength = Math.Clamp(
            (exact - RankingWeights.ExactKeyword) / (RankingWeights.ExactNameOrShortcode - RankingWeights.ExactKeyword), 0, 1);
        return (RankingWeights.Fuzzy + (strength * RankingWeights.FuzzyTierSpread), MatchKind.Fuzzy);
    }

    private static (double Score, MatchKind Kind) ExactWithoutSynonym(TermSource s)
    {
        if ((s & TermSource.EnglishName) != 0)
        {
            return (RankingWeights.ExactNameOrShortcode, MatchKind.Name);
        }

        if ((s & TermSource.Shortcode) != 0)
        {
            return (RankingWeights.ExactNameOrShortcode, MatchKind.Shortcode);
        }

        if ((s & KeywordMatch) != 0)
        {
            var bonus = IsCorroborated(s) ? RankingWeights.CorroboratedKeywordBonus : 0;
            var kind = (s & TermSource.BulgarianName) != 0 ? MatchKind.Name : MatchKind.Keyword;
            return (RankingWeights.ExactKeyword + bonus, kind);
        }

        if ((s & TermSource.NameToken) != 0)
        {
            return (RankingWeights.NameTokenPrefix, MatchKind.Name);
        }

        if ((s & TermSource.KeywordToken) != 0)
        {
            return (RankingWeights.KeywordPrefix, MatchKind.Keyword);
        }

        if ((s & TermSource.ShortcodeToken) != 0)
        {
            return (RankingWeights.KeywordPrefix, MatchKind.Shortcode);
        }

        return (s & TermSource.GroupWord) != 0 ? (RankingWeights.GroupWord, MatchKind.Group) : (0, MatchKind.Name);
    }

    /// <summary>
    /// An exact keyword match that our own synonyms also name for this exact word.
    /// CLDR keywords and gemoji shortcodes are excluded from counting as a second, independent source: both
    /// routinely repeat words already in the emoji's own name or in each other (SPEC's own "football" example —
    /// ⚽ and 🏈 both list "football" as a keyword, and 🏉's gemoji alias "rugby_football" tokenizes to the same
    /// word), so treating name/keyword/gemoji agreement as corroboration rewards coincidence, not relevance.
    /// Curated synonyms are the one source QuickEmoji's own authors chose deliberately, so agreement with one
    /// is a real second opinion.
    /// </summary>
    private static bool IsCorroborated(TermSource s) =>
        (s & TermSource.Keyword) != 0 && (s & TermSource.Synonym) != 0;
}
