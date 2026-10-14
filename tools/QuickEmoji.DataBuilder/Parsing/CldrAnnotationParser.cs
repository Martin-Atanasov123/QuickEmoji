using System.Text.Json;
using QuickEmoji.DataBuilder.Building;

namespace QuickEmoji.DataBuilder.Parsing;

internal sealed record CldrAnnotation(string? Name, IReadOnlyList<string> Keywords);

/// <summary>
/// Parses cldr-json annotation files (<c>annotations</c> or <c>annotationsDerived</c>) into a map keyed by <see cref="EmojiKey.Of"/>.
/// The name comes from <c>tts</c>, the keywords from <c>default</c>.
/// </summary>
internal static class CldrAnnotationParser
{
    public static Dictionary<string, CldrAnnotation> Parse(Stream json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("annotations", out var container) && !root.TryGetProperty("annotationsDerived", out container))
        {
            throw new InvalidDataException("CLDR file has neither 'annotations' nor 'annotationsDerived' at the root.");
        }

        var result = new Dictionary<string, CldrAnnotation>(StringComparer.Ordinal);
        foreach (var item in container.GetProperty("annotations").EnumerateObject())
        {
            var name = item.Value.TryGetProperty("tts", out var tts) && tts.GetArrayLength() > 0 ? tts[0].GetString() : null;
            var keywords = item.Value.TryGetProperty("default", out var defaults)
                ? defaults.EnumerateArray().Select(k => k.GetString()!).ToArray()
                : [];
            result[EmojiKey.Of(item.Name)] = new CldrAnnotation(name, keywords);
        }

        return result;
    }

    /// <summary>Merges base annotations with derived ones; base entries win.</summary>
    public static Dictionary<string, CldrAnnotation> Merge(Dictionary<string, CldrAnnotation> annotations, Dictionary<string, CldrAnnotation> derived)
    {
        var merged = new Dictionary<string, CldrAnnotation>(derived, StringComparer.Ordinal);
        foreach (var (key, value) in annotations)
        {
            merged[key] = value;
        }

        return merged;
    }
}
