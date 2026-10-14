using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace QuickEmoji.Core.Emoji;

/// <summary>An Emoji version such as 15.1, as listed in Unicode's emoji-test.txt.</summary>
public readonly record struct EmojiVersion(int Major, int Minor) : IComparable<EmojiVersion>
{
    public int CompareTo(EmojiVersion other) =>
        Major != other.Major ? Major.CompareTo(other.Major) : Minor.CompareTo(other.Minor);

    public static bool operator <(EmojiVersion left, EmojiVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(EmojiVersion left, EmojiVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(EmojiVersion left, EmojiVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(EmojiVersion left, EmojiVersion right) => left.CompareTo(right) >= 0;

    public static EmojiVersion Parse(string text) =>
        TryParse(text, out var version)
            ? version
            : throw new FormatException($"'{text}' is not an emoji version (expected major.minor).");

    public static bool TryParse([NotNullWhen(true)] string? text, out EmojiVersion version)
    {
        version = default;
        var dot = text?.IndexOf('.', StringComparison.Ordinal) ?? -1;
        if (text is null || dot <= 0
            || !int.TryParse(text.AsSpan(0, dot), NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(text.AsSpan(dot + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var minor))
        {
            return false;
        }

        version = new EmojiVersion(major, minor);
        return true;
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}");
}
