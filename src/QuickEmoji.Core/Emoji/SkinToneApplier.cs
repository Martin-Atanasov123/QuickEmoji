namespace QuickEmoji.Core.Emoji;

/// <summary>
/// Substitutes a base emoji's preferred-skin-tone variant for display/insertion (SPEC §6.2 "preferred skin
/// tone"). Never splits or re-normalizes a sequence (CLAUDE.md) — it only ever picks one of the catalog's
/// already-composed variant strings, or falls back to the base's own string unchanged.
/// </summary>
public static class SkinToneApplier
{
    /// <summary>
    /// <paramref name="preferred"/> applied to <paramref name="entry"/>: its uniform variant (every person in
    /// the sequence has that tone) when one exists, otherwise the base emoji itself — including when
    /// <paramref name="preferred"/> is <see cref="SkinTone.None"/>, the entry has no variants, or it is a
    /// multi-person emoji with no uniformly-toned variant for that tone.
    /// </summary>
    public static string Apply(EmojiEntry entry, SkinTone preferred)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (preferred == SkinTone.None)
        {
            return entry.Emoji;
        }

        foreach (var variant in entry.Variants)
        {
            if (variant.IsUniform(preferred))
            {
                return variant.Emoji;
            }
        }

        return entry.Emoji;
    }
}
