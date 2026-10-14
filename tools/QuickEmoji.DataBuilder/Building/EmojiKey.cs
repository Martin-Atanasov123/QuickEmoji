using System.Text;
using QuickEmoji.Core.Emoji;

namespace QuickEmoji.DataBuilder.Building;

/// <summary>
/// Lookup keys for joining sources that spell the same emoji differently (CLDR omits VS16; gemoji sometimes keeps it).
/// Keys are only ever used to find records. Stored emoji strings always stay exactly as emoji-test.txt spells them.
/// </summary>
internal static class EmojiKey
{
    private const char VariationSelector16 = '️';

    /// <summary>The sequence without VS16.</summary>
    public static string Of(string emoji) => emoji.Replace(VariationSelector16.ToString(), "", StringComparison.Ordinal);

    /// <summary>The sequence without VS16 and without skin-tone modifiers.</summary>
    public static string WithoutSkinTones(string emoji)
    {
        var builder = new StringBuilder(emoji.Length);
        foreach (var rune in emoji.EnumerateRunes())
        {
            if (rune.Value != VariationSelector16 && !SkinTones.IsModifier(rune.Value))
            {
                builder.Append(rune.ToString());
            }
        }

        return builder.ToString();
    }

    public static bool HasSkinTone(string emoji) => emoji.EnumerateRunes().Any(r => SkinTones.IsModifier(r.Value));
}
