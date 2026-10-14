namespace QuickEmoji.Core.Emoji;

/// <summary>The versions of the upstream data that produced emoji.json.</summary>
public sealed record EmojiSourceVersions(string EmojiTest, string Cldr, string Gemoji);

/// <summary>An immutable set of base emoji, in emoji-test.txt order.</summary>
public sealed class EmojiCatalog
{
    private readonly Dictionary<string, EmojiEntry> _byEmoji;

    public EmojiCatalog(IReadOnlyList<EmojiEntry> entries, EmojiSourceVersions sources)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(sources);
        Entries = entries;
        Sources = sources;
        _byEmoji = entries.ToDictionary(e => e.Emoji, StringComparer.Ordinal);
    }

    public IReadOnlyList<EmojiEntry> Entries { get; }

    public EmojiSourceVersions Sources { get; }

    /// <summary>Exact (ordinal) lookup of a base emoji.</summary>
    public bool TryGet(string emoji, out EmojiEntry entry) => _byEmoji.TryGetValue(emoji, out entry!);

    /// <summary>Every sequence in the catalog, base emoji and skin-tone variants alike.</summary>
    public IEnumerable<(string Emoji, EmojiVersion Version)> AllSequences()
    {
        foreach (var entry in Entries)
        {
            yield return (entry.Emoji, entry.Version);
            foreach (var variant in entry.Variants)
            {
                yield return (variant.Emoji, variant.Version);
            }
        }
    }

    /// <summary>
    /// A catalog without the emoji <paramref name="filter"/> rejects. A rejected base emoji takes its variants with it.
    /// </summary>
    public EmojiCatalog Apply(EmojiCatalogFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var kept = new List<EmojiEntry>(Entries.Count);
        foreach (var entry in Entries)
        {
            if (!filter.Allows(entry.Emoji, entry.Version))
            {
                continue;
            }

            var variants = entry.Variants.Where(v => filter.Allows(v.Emoji, v.Version)).ToList();
            kept.Add(variants.Count == entry.Variants.Count ? entry : entry.WithVariants(variants));
        }

        return new EmojiCatalog(kept, Sources);
    }
}
