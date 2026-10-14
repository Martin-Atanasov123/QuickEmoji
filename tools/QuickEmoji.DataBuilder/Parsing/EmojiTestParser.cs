using System.Globalization;
using System.Text;
using QuickEmoji.Core.Emoji;

namespace QuickEmoji.DataBuilder.Parsing;

internal sealed record EmojiTestRecord(string Emoji, string Name, EmojiVersion Version, string Group, string Subgroup);

/// <summary>
/// Parses Unicode's emoji-test.txt and keeps fully-qualified entries only. The emoji string is built from the
/// code-point column, which is authoritative; the glyph in the comment is only cross-checked.
/// </summary>
internal static class EmojiTestParser
{
    private const string GroupPrefix = "# group:";
    private const string SubgroupPrefix = "# subgroup:";

    public static IReadOnlyList<EmojiTestRecord> ParseFullyQualified(TextReader reader)
    {
        var records = new List<EmojiTestRecord>();
        string? group = null;
        string? subgroup = null;
        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (line.StartsWith(GroupPrefix, StringComparison.Ordinal))
            {
                group = line[GroupPrefix.Length..].Trim();
                continue;
            }

            if (line.StartsWith(SubgroupPrefix, StringComparison.Ordinal))
            {
                subgroup = line[SubgroupPrefix.Length..].Trim();
                continue;
            }

            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var record = ParseDataLine(line, lineNumber, group, subgroup);
            if (record is not null)
            {
                records.Add(record);
            }
        }

        return records;
    }

    private static EmojiTestRecord? ParseDataLine(string line, int lineNumber, string? group, string? subgroup)
    {
        // 1F44D 1F3FD ; fully-qualified # 👍🏽 E1.0 thumbs up: medium skin tone
        var semicolon = line.IndexOf(';', StringComparison.Ordinal);
        var hash = line.IndexOf('#', StringComparison.Ordinal);
        if (semicolon < 0 || hash < semicolon)
        {
            throw Error(lineNumber, "expected 'code points ; status # comment'");
        }

        if (line[(semicolon + 1)..hash].Trim() != "fully-qualified")
        {
            return null;
        }

        if (group is null || subgroup is null)
        {
            throw Error(lineNumber, "data line appears before its group or subgroup header");
        }

        var emoji = BuildString(line[..semicolon], lineNumber);

        // Comment: "<glyph> E<version> <name>"
        var comment = line[(hash + 1)..].Trim();
        var versionStart = comment.IndexOf(" E", StringComparison.Ordinal);
        var nameStart = versionStart < 0 ? -1 : comment.IndexOf(' ', versionStart + 2);
        if (versionStart < 0 || nameStart < 0 || !EmojiVersion.TryParse(comment[(versionStart + 2)..nameStart], out var version))
        {
            throw Error(lineNumber, "comment is not '<emoji> E<version> <name>'");
        }

        if (!string.Equals(comment[..versionStart], emoji, StringComparison.Ordinal))
        {
            throw Error(lineNumber, "the glyph in the comment does not match the code points");
        }

        return new EmojiTestRecord(emoji, comment[(nameStart + 1)..], version, group, subgroup);
    }

    private static string BuildString(string codePoints, int lineNumber)
    {
        var builder = new StringBuilder();
        foreach (var hex in codePoints.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value) || !Rune.IsValid(value))
            {
                throw Error(lineNumber, $"'{hex}' is not a valid code point");
            }

            builder.Append(new Rune(value).ToString());
        }

        return builder.Length > 0 ? builder.ToString() : throw Error(lineNumber, "no code points");
    }

    private static FormatException Error(int lineNumber, string message) =>
        new($"emoji-test.txt line {lineNumber}: {message}.");
}
