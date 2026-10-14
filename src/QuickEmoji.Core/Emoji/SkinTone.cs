namespace QuickEmoji.Core.Emoji;

/// <summary>Fitzpatrick skin-tone modifiers U+1F3FB..U+1F3FF. <see cref="None"/> means the default yellow.</summary>
public enum SkinTone
{
    None = 0,
    Light = 1,
    MediumLight = 2,
    Medium = 3,
    MediumDark = 4,
    Dark = 5,
}

public static class SkinTones
{
    private const int FirstModifier = 0x1F3FB;
    private const int LastModifier = 0x1F3FF;

    public static bool IsModifier(int codePoint) => codePoint is >= FirstModifier and <= LastModifier;

    public static SkinTone FromModifier(int codePoint) =>
        IsModifier(codePoint)
            ? (SkinTone)(codePoint - FirstModifier + 1)
            : throw new ArgumentOutOfRangeException(nameof(codePoint), $"U+{codePoint:X} is not a skin-tone modifier.");

    /// <summary>The skin-tone modifiers in <paramref name="emoji"/>, in order of appearance.</summary>
    public static IReadOnlyList<SkinTone> In(string emoji)
    {
        ArgumentNullException.ThrowIfNull(emoji);
        var tones = new List<SkinTone>();
        foreach (var rune in emoji.EnumerateRunes())
        {
            if (IsModifier(rune.Value))
            {
                tones.Add(FromModifier(rune.Value));
            }
        }

        return tones;
    }
}
