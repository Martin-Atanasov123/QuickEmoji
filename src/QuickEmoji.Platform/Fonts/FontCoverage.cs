using QuickEmoji.Core.Emoji;

namespace QuickEmoji.Platform.Fonts;

/// <summary>What the system emoji font can draw: a version cap plus individual sequences it cannot.</summary>
public sealed record FontCoverage(EmojiVersion? MaxEmojiVersion, IReadOnlySet<string> Unrenderable)
{
    public EmojiCatalogFilter ToFilter() => new(MaxEmojiVersion, Unrenderable);
}

/// <summary>
/// Turns per-sequence probe results into <see cref="FontCoverage"/>. The cap is the newest emoji version whose
/// sequences render at least <see cref="RenderableShareForVersion"/> of the time; anything else that fails to
/// render is excluded individually, so tofu never reaches results (SPEC §3.2).
/// </summary>
public static class FontCoverageCalculator
{
    public const double RenderableShareForVersion = 0.9;

    public static FontCoverage Compute(IEnumerable<(string Emoji, EmojiVersion Version, bool Renderable)> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var list = results.ToList();

        var unrenderable = list.Where(r => !r.Renderable).Select(r => r.Emoji).ToHashSet(StringComparer.Ordinal);
        EmojiVersion? max = null;
        foreach (var version in list.GroupBy(r => r.Version))
        {
            var share = (double)version.Count(r => r.Renderable) / version.Count();
            if (share >= RenderableShareForVersion && (max is null || version.Key > max.Value))
            {
                max = version.Key;
            }
        }

        return new FontCoverage(max, unrenderable);
    }
}
