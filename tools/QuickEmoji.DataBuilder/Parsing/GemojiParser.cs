using System.Text.Json;
using QuickEmoji.DataBuilder.Building;

namespace QuickEmoji.DataBuilder.Parsing;

internal sealed record GemojiRecord(IReadOnlyList<string> Aliases, IReadOnlyList<string> Tags);

/// <summary>Parses GitHub gemoji's db/emoji.json into a map keyed by <see cref="EmojiKey.Of"/>.</summary>
internal static class GemojiParser
{
    public static Dictionary<string, GemojiRecord> Parse(Stream json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new Dictionary<string, GemojiRecord>(StringComparer.Ordinal);
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var emoji = item.GetProperty("emoji").GetString()
                ?? throw new InvalidDataException("gemoji entry without an 'emoji' value.");
            result[EmojiKey.Of(emoji)] = new GemojiRecord(Strings(item, "aliases"), Strings(item, "tags"));
        }

        return result;
    }

    private static string[] Strings(JsonElement item, string property) =>
        item.TryGetProperty(property, out var array) ? array.EnumerateArray().Select(e => e.GetString()!).ToArray() : [];
}
