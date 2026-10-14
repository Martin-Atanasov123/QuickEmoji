using System.Globalization;
using System.Text;

namespace QuickEmoji.Core.Search;

/// <summary>
/// Query and index normalisation (SPEC §3.3): trim, collapse whitespace, lowercase (invariant), strip Latin
/// diacritics via NFKD. Cyrillic is left exactly as typed (й stays й). Apostrophes are dropped so "woman's"
/// matches "woman’s"; other punctuation separates tokens. '+', '#' and '*' stay inside tokens for "+1" and keycaps.
/// </summary>
public static class QueryNormalizer
{
    public static string Normalize(string? text) => string.Join(' ', Tokenize(text));

    public static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var folded = Fold(text.Normalize(NormalizationForm.FormC));
        var tokens = new List<string>();
        var start = -1;
        for (var i = 0; i <= folded.Length; i++)
        {
            var isTokenChar = i < folded.Length && IsTokenChar(folded[i]);
            if (isTokenChar && start < 0)
            {
                start = i;
            }
            else if (!isTokenChar && start >= 0)
            {
                tokens.Add(folded[start..i]);
                start = -1;
            }
        }

        return tokens;
    }

    private static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsApostrophe(rune.Value))
            {
                continue;
            }

            if (IsCyrillic(rune.Value))
            {
                builder.Append(char.ToLowerInvariant((char)rune.Value));
                continue;
            }

            foreach (var c in rune.ToString().Normalize(NormalizationForm.FormKD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(char.ToLowerInvariant(c));
                }
            }
        }

        return builder.ToString();
    }

    private static bool IsTokenChar(char c) => char.IsLetterOrDigit(c) || c is '+' or '#' or '*';

    private static bool IsCyrillic(int codePoint) => codePoint is >= 0x0400 and <= 0x052F;

    private static bool IsApostrophe(int codePoint) => codePoint is '\'' or '`' or 0x2018 or 0x2019 or 0x02BC or 0x00B4;
}
