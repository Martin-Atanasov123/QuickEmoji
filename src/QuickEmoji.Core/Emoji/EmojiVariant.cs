namespace QuickEmoji.Core.Emoji;

/// <summary>
/// A skin-tone form of a base emoji. <see cref="Tones"/> lists one tone per person in the sequence,
/// so 🧑🏻‍🤝‍🧑🏿 has [Light, Dark] and 👍🏽 has [Medium].
/// </summary>
public sealed record EmojiVariant(string Emoji, IReadOnlyList<SkinTone> Tones, EmojiVersion Version)
{
    /// <summary>True when every person in the sequence has <paramref name="tone"/>.</summary>
    public bool IsUniform(SkinTone tone) => Tones.Count > 0 && Tones.All(t => t == tone);
}
