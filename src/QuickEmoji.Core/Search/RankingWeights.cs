namespace QuickEmoji.Core.Search;

/// <summary>
/// Every ranking constant (SPEC §3.4). Nothing else in the code base may hard-code a score.
/// Deviations from the SPEC table are recorded in docs/DECISIONS.md.
/// </summary>
public static class RankingWeights
{
    // Tiers: a candidate scores the best tier it matches.
    public const double ExactNameOrShortcode = 1000;

    /// <summary>
    /// A hand-curated synonym, for the emoji listed first under that word. SPEC §3.4 says 450; raised so curated
    /// slang can beat incidental CLDR keywords and name prefixes ("sus" → 🤨, not 🍣 "sushi"). See DECISIONS.md.
    /// </summary>
    public const double ExactSynonym = 950;

    /// <summary>
    /// Subtracted from <see cref="ExactSynonym"/> for each later position in the word's list. Larger than
    /// <see cref="PopularityMax"/> so the curator's stated order always wins over the popularity tie-breaker.
    /// </summary>
    public const double SynonymRankStep = 65;

    public const double ExactKeyword = 800;
    public const double NameTokenPrefix = 600;
    public const double KeywordPrefix = 500;
    public const double Fuzzy = 300;
    public const double GroupWord = 200;

    /// <summary>
    /// Fuzzy matches spread over [<see cref="Fuzzy"/>, <see cref="Fuzzy"/> + this], following where the corrected word
    /// would rank as an exact match (keyword → bottom, name or shortcode → top), so a typo ranks like the word it fixes.
    /// </summary>
    public const double FuzzyTierSpread = 100;

    /// <summary>
    /// Added to an exact keyword match when a second, independent source agrees (the emoji's name, gemoji, or our
    /// synonyms). Keeps peripheral CLDR keywords on very popular emoji (😂 → "happy") from winning. See DECISIONS.md.
    /// </summary>
    public const double CorroboratedKeywordBonus = 100;

    /// <summary>Multi-word query whose full phrase is a prefix of the emoji's name ("thumbs up", "red heart").</summary>
    public const double PhrasePrefixOfNameBonus = 150;

    // Adjustments.
    public const double PopularityMax = 60;
    public const double FrecencyMax = 150;
    public static readonly TimeSpan FrecencyHalfLife = TimeSpan.FromDays(14);

    /// <summary>Weight of one (decayed) use of this emoji for exactly this query.</summary>
    public const double FrecencyQueryUseWeight = 1.0;

    /// <summary>Weight of one (decayed) use of this emoji for any query.</summary>
    public const double FrecencyGlobalUseWeight = 0.25;

    /// <summary>Weighted uses at which the frecency bonus reaches 63 % of <see cref="FrecencyMax"/>.</summary>
    public const double FrecencySaturationUses = 2.0;

    public const double MinimumScore = 150;

    // Fuzzy matching: Damerau-Levenshtein ≤ 1 for tokens of 4–6 characters, ≤ 2 from 7; none below 4.
    public const int FuzzyMinTokenLength = 4;
    public const int FuzzyOneEditMaxTokenLength = 6;

    public const int MaxResults = 3;
    public const int QueryCacheCapacity = 256;

    /// <summary>Shown for an empty query until the user has picked anything (SPEC §2).</summary>
    public static IReadOnlyList<string> FirstRunDefaults { get; } = ["❤️", "😂", "👍"];
}
