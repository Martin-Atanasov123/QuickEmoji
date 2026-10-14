using QuickEmoji.Core.Emoji;
using QuickEmoji.DataBuilder.Parsing;

namespace QuickEmoji.DataBuilder.Building;

internal sealed record ToneVariant(EmojiTestRecord Record, IReadOnlyList<SkinTone> Tones);

internal sealed record GroupedEmoji(EmojiTestRecord Base, IReadOnlyList<ToneVariant> Variants);

/// <summary>
/// Groups skin-tone sequences under their base emoji (SPEC §3.2).
/// A toned sequence belongs to the base with the same sequence once skin tones and VS16 are removed
/// (👍🏽 → 👍, 👩🏻‍❤️‍👨🏼 → 👩‍❤️‍👨). Multi-person forms that have no untoned twin
/// (🧑🏻‍❤️‍💋‍🧑🏼, 🫱🏻‍🫲🏿) fall back to the base whose name precedes the colon ("kiss", "handshake").
/// </summary>
internal static class SkinToneGrouper
{
    public static IReadOnlyList<GroupedEmoji> Group(IReadOnlyList<EmojiTestRecord> records)
    {
        var bases = records.Where(r => !EmojiKey.HasSkinTone(r.Emoji)).ToList();
        var byKey = new Dictionary<string, EmojiTestRecord>(StringComparer.Ordinal);
        var byName = new Dictionary<string, EmojiTestRecord>(StringComparer.Ordinal);
        foreach (var record in bases)
        {
            if (!byKey.TryAdd(EmojiKey.WithoutSkinTones(record.Emoji), record))
            {
                throw new InvalidDataException($"Two base emoji share the key of {record.Emoji} ({record.Name}).");
            }

            byName.TryAdd(record.Name, record);
        }

        var variants = bases.ToDictionary(b => b, _ => new List<ToneVariant>());
        foreach (var record in records.Where(r => EmojiKey.HasSkinTone(r.Emoji)))
        {
            var owner = FindBase(record, byKey, byName);
            variants[owner].Add(new ToneVariant(record, SkinTones.In(record.Emoji)));
        }

        return bases.Select(b => new GroupedEmoji(b, variants[b])).ToList();
    }

    private static EmojiTestRecord FindBase(
        EmojiTestRecord toned,
        Dictionary<string, EmojiTestRecord> byKey,
        Dictionary<string, EmojiTestRecord> byName)
    {
        if (byKey.TryGetValue(EmojiKey.WithoutSkinTones(toned.Emoji), out var exact))
        {
            return exact;
        }

        var colon = toned.Name.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0 && byName.TryGetValue(toned.Name[..colon], out var named))
        {
            return named;
        }

        throw new InvalidDataException($"No base emoji found for skin-tone sequence {toned.Emoji} ({toned.Name}).");
    }
}
