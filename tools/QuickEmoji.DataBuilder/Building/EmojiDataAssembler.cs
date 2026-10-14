using QuickEmoji.Core.Emoji;
using QuickEmoji.Core.Emoji.Serialization;
using QuickEmoji.DataBuilder.Parsing;

namespace QuickEmoji.DataBuilder.Building;

internal sealed record SourceData(
    IReadOnlyList<EmojiTestRecord> EmojiTest,
    Dictionary<string, CldrAnnotation> CldrEnglish,
    Dictionary<string, CldrAnnotation> CldrBulgarian,
    Dictionary<string, GemojiRecord> Gemoji,
    IReadOnlyList<(string Word, IReadOnlyList<string> Emoji)> Synonyms,
    IReadOnlyList<string> Popularity);

/// <summary>Merges every source into the emoji.json model and fails on any reference it cannot resolve.</summary>
internal static class EmojiDataAssembler
{
    public static EmojiDataFile Assemble(SourceData sources, EmojiDataSources versions)
    {
        var grouped = SkinToneGrouper.Group(sources.EmojiTest);
        var baseByKey = grouped.ToDictionary(g => EmojiKey.Of(g.Base.Emoji), g => g.Base.Emoji, StringComparer.Ordinal);
        var synonyms = InvertSynonyms(sources.Synonyms, baseByKey);
        var popularity = RankPopularity(sources.Popularity, baseByKey);

        var records = grouped.Select(g => ToRecord(g, sources, synonyms, popularity)).ToList();
        return new EmojiDataFile { SchemaVersion = EmojiDataFile.CurrentSchemaVersion, Sources = versions, Emoji = records };
    }

    private static EmojiRecord ToRecord(
        GroupedEmoji group,
        SourceData sources,
        Dictionary<string, List<string>> synonyms,
        Dictionary<string, int> popularity)
    {
        var emoji = group.Base.Emoji;
        var key = EmojiKey.Of(emoji);
        sources.CldrEnglish.TryGetValue(key, out var english);
        sources.CldrBulgarian.TryGetValue(key, out var bulgarian);
        sources.Gemoji.TryGetValue(key, out var gemoji);

        var keywords = Distinct((english?.Keywords ?? []).Concat(gemoji?.Tags ?? []));
        return new EmojiRecord
        {
            Emoji = emoji,
            Name = english?.Name ?? group.Base.Name,
            NameBg = bulgarian?.Name,
            Version = group.Base.Version.ToString(),
            Group = group.Base.Group,
            Subgroup = group.Base.Subgroup,
            Shortcodes = NullIfEmpty(Distinct(gemoji?.Aliases ?? [])),
            Keywords = NullIfEmpty(keywords),
            KeywordsBg = NullIfEmpty(Distinct(bulgarian?.Keywords ?? [])),
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

    private static Dictionary<string, List<string>> InvertSynonyms(
        IReadOnlyList<(string Word, IReadOnlyList<string> Emoji)> synonyms,
        Dictionary<string, string> baseByKey)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (word, targets) in synonyms)
        {
            foreach (var target in targets)
            {
                var emoji = ResolveBase(target, baseByKey, $"synonyms.json '{word}'");
                var words = result.TryGetValue(emoji, out var list) ? list : result[emoji] = [];
                if (!words.Contains(word, StringComparer.Ordinal))
                {
                    words.Add(word);
                }
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
