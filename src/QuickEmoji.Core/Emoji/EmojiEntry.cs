namespace QuickEmoji.Core.Emoji;

/// <summary>
/// A base emoji (no skin-tone modifier) with its search metadata and skin-tone variants.
/// <see cref="Emoji"/> is the exact fully-qualified sequence; it is never re-normalised.
/// </summary>
public sealed class EmojiEntry
{
    public required string Emoji { get; init; }

    /// <summary>CLDR English short name, e.g. "red heart".</summary>
    public required string Name { get; init; }

    /// <summary>CLDR Bulgarian short name, or null when CLDR has none.</summary>
    public string? NameBg { get; init; }

    public required EmojiVersion Version { get; init; }

    public required string Group { get; init; }

    public required string Subgroup { get; init; }

    /// <summary>gemoji aliases such as "thumbsup".</summary>
    public IReadOnlyList<string> Shortcodes { get; init; } = [];

    /// <summary>CLDR English keywords.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>CLDR Bulgarian keywords.</summary>
    public IReadOnlyList<string> KeywordsBg { get; init; } = [];

    /// <summary>gemoji tags (English), e.g. 😀 → "happy".</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Hand-maintained slang and synonyms, English and Bulgarian.</summary>
    public IReadOnlyList<EmojiSynonym> Synonyms { get; init; } = [];

    /// <summary>1 = most used; null when outside the popularity list.</summary>
    public int? PopularityRank { get; init; }

    public IReadOnlyList<EmojiVariant> Variants { get; init; } = [];

    public bool SupportsSkinTones => Variants.Count > 0;

    internal EmojiEntry WithVariants(IReadOnlyList<EmojiVariant> variants) => new()
    {
        Emoji = Emoji,
        Name = Name,
        NameBg = NameBg,
        Version = Version,
        Group = Group,
        Subgroup = Subgroup,
        Shortcodes = Shortcodes,
        Keywords = Keywords,
        KeywordsBg = KeywordsBg,
        Tags = Tags,
        Synonyms = Synonyms,
        PopularityRank = PopularityRank,
        Variants = variants,
    };

    public override string ToString() => $"{Emoji} {Name}";
}
