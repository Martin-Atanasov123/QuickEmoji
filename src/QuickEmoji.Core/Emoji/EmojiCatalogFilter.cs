namespace QuickEmoji.Core.Emoji;

/// <summary>
/// Removes emoji the system font cannot draw (SPEC §3.2): everything above <see cref="MaxVersion"/>,
/// plus individual sequences in <see cref="Unrenderable"/>.
/// </summary>
public sealed record EmojiCatalogFilter(EmojiVersion? MaxVersion = null, IReadOnlySet<string>? Unrenderable = null)
{
    public static EmojiCatalogFilter None { get; } = new();

    public bool Allows(string emoji, EmojiVersion version) =>
        (MaxVersion is not { } max || version <= max)
        && (Unrenderable is null || !Unrenderable.Contains(emoji));
}
