using QuickEmoji.Core.Emoji.Serialization;
using QuickEmoji.DataBuilder.Parsing;

namespace QuickEmoji.DataBuilder.Building;

internal sealed record SourceData(
    IReadOnlyList<EmojiTestRecord> EmojiTest,
    Dictionary<string, CldrAnnotation> CldrEnglish,
    Dictionary<string, CldrAnnotation> CldrBulgarian,
    Dictionary<string, GemojiRecord> Gemoji,
    IReadOnlyList<(string Word, IReadOnlyList<string> Emoji)> Synonyms,
    IReadOnlyList<string> Popularity,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ShortcodeExclusions);

/// <summary>Merges every source into the emoji.json model and fails on any reference it cannot resolve.</summary>
internal static class EmojiDataAssembler
{
    public static EmojiDataFile Assemble(SourceData sources, EmojiDataSources versions)
    {
        var grouped = SkinToneGrouper.Group(sources.EmojiTest);
        var baseByKey = grouped.ToDictionary(g => EmojiKey.Of(g.Base.Emoji), g => g.Base.Emoji, StringComparer.Ordinal);
        var synonyms = InvertSynonyms(sources.Synonyms, baseByKey);
        var popularity = RankPopularity(sources.Popularity, baseByKey);
        var exclusions = ResolveExclusions(sources.ShortcodeExclusions, sources.Gemoji, baseByKey);

        var records = grouped.Select(g => ToRecord(g, sources, synonyms, popularity, exclusions)).ToList();
        return new EmojiDataFile { SchemaVersion = EmojiDataFile.CurrentSchemaVersion, Sources = versions, Emoji = records };
    }

    private static EmojiRecord ToRecord(
        GroupedEmoji group,
        SourceData sources,
        Dictionary<string, List<SynonymRecord>> synonyms,
        Dictionary<string, int> popularity,
        Dictionary<string, IReadOnlyList<string>> exclusions)
    {
        var emoji = group.Base.Emoji;
        var key = EmojiKey.Of(emoji);
        sources.CldrEnglish.TryGetValue(key, out var english);
        sources.CldrBulgarian.TryGetValue(key, out var bulgarian);
        sources.Gemoji.TryGetValue(key, out var gemoji);
        var excluded = exclusions.GetValueOrDefault(emoji) ?? [];

        return new EmojiRecord
        {
            Emoji = emoji,
            Name = english?.Name ?? group.Base.Name,
            NameBg = bulgarian?.Name,
            Version = group.Base.Version.ToString(),
            Group = group.Base.Group,
            Subgroup = group.Base.Subgroup,
            Shortcodes = NullIfEmpty(Distinct((gemoji?.Aliases ?? []).Where(a => !excluded.Contains(a, StringComparer.Ordinal)))),
            Keywords = NullIfEmpty(Distinct(english?.Keywords ?? [])),
            KeywordsBg = NullIfEmpty(Distinct(bulgarian?.Keywords ?? [])),
            Tags = NullIfEmpty(Distinct(gemoji?.Tags ?? [])),
            Synonyms = synonyms.TryGetValue(emoji, out var words) ? words : null,
            Popularity = popularity.TryGetValue(emoji, out var rank) ? rank : null,
            Variants = group.Variants.Count == 0
                ? null
                : group.Variants.Select(v => new VariantRecord
                {
                    Emoji = v.Record.Emoji,
                    Tones = v.Tones.Select(t => (int)t).ToList(),
                    Version = v.Record.Version == group.Base.Version ? null : v.Record.Version.ToString(),
                }).ToList(),
        };
    }

    private static Dictionary<string, List<SynonymRecord>> InvertSynonyms(
        IReadOnlyList<(string Word, IReadOnlyList<string> Emoji)> synonyms,
        Dictionary<string, string> baseByKey)
    {
        var result = new Dictionary<string, List<SynonymRecord>>(StringComparer.Ordinal);
        foreach (var (word, targets) in synonyms)
        {
            for (var position = 0; position < targets.Count; position++)
            {
                var emoji = ResolveBase(targets[position], baseByKey, $"synonyms.json '{word}'");
                var records = result.TryGetValue(emoji, out var list) ? list : result[emoji] = [];
                if (records.Any(r => r.Word == word))
                {
                    throw new InvalidDataException($"synonyms.json '{word}' lists {targets[position]} twice.");
                }

                records.Add(new SynonymRecord { Word = word, Rank = position + 1 });
            }
        }

        return result;
    }

    private static Dictionary<string, int> RankPopularity(IReadOnlyList<string> order, Dictionary<string, string> baseByKey)
    {
        var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < order.Count; i++)
        {
            var emoji = ResolveBase(order[i], baseByKey, "popularity.json");
            if (!ranks.TryAdd(emoji, i + 1))
            {
                throw new InvalidDataException($"popularity.json lists {order[i]} twice.");
            }
        }

        return ranks;
    }

    /// <summary>Every excluded alias must exist in gemoji, so the file cannot silently go stale.</summary>
    private static Dictionary<string, IReadOnlyList<string>> ResolveExclusions(
        IReadOnlyDictionary<string, IReadOnlyList<string>> exclusions,
        Dictionary<string, GemojiRecord> gemoji,
        Dictionary<string, string> baseByKey)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (target, aliases) in exclusions)
        {
            var emoji = ResolveBase(target, baseByKey, "shortcode-exclusions.json");
            var known = gemoji.TryGetValue(EmojiKey.Of(emoji), out var record) ? record.Aliases : [];
            foreach (var alias in aliases.Where(a => !known.Contains(a, StringComparer.Ordinal)))
            {
                throw new InvalidDataException($"shortcode-exclusions.json: {emoji} has no gemoji alias '{alias}'.");
            }

            result[emoji] = aliases;
        }

        return result;
    }

    private static string ResolveBase(string emoji, Dictionary<string, string> baseByKey, string context)
    {
        if (EmojiKey.HasSkinTone(emoji))
        {
            throw new InvalidDataException($"{context}: {emoji} carries a skin tone; reference the base emoji instead.");
        }

        return baseByKey.TryGetValue(EmojiKey.Of(emoji), out var resolved)
            ? resolved
            : throw new InvalidDataException($"{context}: {emoji} is not a fully-qualified emoji in emoji-test.txt.");
    }

    private static List<string> Distinct(IEnumerable<string> values) =>
        values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.Ordinal).ToList();

    private static List<string>? NullIfEmpty(List<string> values) => values.Count == 0 ? null : values;
}
