using System.Globalization;
using System.Text.Json;

namespace QuickEmoji.DataBuilder.Parsing;

/// <summary>Parses the hand-maintained synonyms.json and popularity.json.</summary>
internal static class CuratedDataParser
{
    private static readonly JsonDocumentOptions Options = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>
    /// synonyms.json: <c>{ "word": ["😂", "🤣"], ... }</c>. Words must be lowercase and trimmed; each needs at least one emoji.
    /// </summary>
    public static IReadOnlyList<(string Word, IReadOnlyList<string> Emoji)> ParseSynonyms(Stream json)
    {
        using var document = JsonDocument.Parse(json, Options);
        var result = new List<(string, IReadOnlyList<string>)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var word = property.Name;
            if (word.Length == 0 || word != word.Trim() || word != word.ToLower(CultureInfo.InvariantCulture))
            {
                throw new InvalidDataException($"synonyms.json: '{word}' must be trimmed and lowercase.");
            }

            if (!seen.Add(word))
            {
                throw new InvalidDataException($"synonyms.json: '{word}' is listed twice.");
            }

            var emoji = property.Value.EnumerateArray().Select(e => e.GetString()!).ToArray();
            if (emoji.Length == 0)
            {
                throw new InvalidDataException($"synonyms.json: '{word}' has no emoji.");
            }

            result.Add((word, emoji));
        }

        return result;
    }

    /// <summary>shortcode-exclusions.json: <c>{ "🌔": ["moon"], ... }</c>, gemoji aliases not to index as shortcodes.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ParseShortcodeExclusions(Stream json)
    {
        using var document = JsonDocument.Parse(json, Options);
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var aliases = property.Value.EnumerateArray().Select(e => e.GetString()!).ToArray();
            if (aliases.Length == 0 || !result.TryAdd(property.Name, aliases))
            {
                throw new InvalidDataException($"shortcode-exclusions.json: '{property.Name}' is empty or listed twice.");
            }
        }

        return result;
    }

    /// <summary>popularity.json: <c>{ "order": ["😂", "❤️", ...] }</c>, most used first.</summary>
    public static IReadOnlyList<string> ParsePopularity(Stream json)
    {
        using var document = JsonDocument.Parse(json, Options);
        return document.RootElement.GetProperty("order").EnumerateArray().Select(e => e.GetString()!).ToArray();
    }
}
