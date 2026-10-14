using QuickEmoji.Core.Emoji;

namespace QuickEmoji.Core.Search;

/// <summary>Keyword languages searched (Settings → Search).</summary>
[Flags]
public enum SearchLanguages
{
    None = 0,
    English = 1,
    Bulgarian = 2,
    All = English | Bulgarian,
}

/// <summary>Which field produced a result's match; the popup explains non-name matches ("matched: affection").</summary>
public enum MatchKind
{
    /// <summary>Empty query: frecency or first-run defaults.</summary>
    Recent,
    Name,
    Shortcode,
    Keyword,
    Synonym,
    Fuzzy,
    Group,
}

public sealed record SearchOptions(SearchLanguages Languages = SearchLanguages.All)
{
    public static SearchOptions Default { get; } = new();
}

/// <summary>One ranked result. <see cref="MatchedTerm"/> is the normalised index term that matched.</summary>
public sealed record SearchResult(EmojiEntry Entry, double Score, MatchKind Kind, string MatchedTerm)
{
    /// <summary>True when the popup should show a "matched: …" line (the match did not come from the name).</summary>
    public bool ExplainsMatch => Kind is MatchKind.Keyword or MatchKind.Synonym or MatchKind.Fuzzy or MatchKind.Shortcode;
}
