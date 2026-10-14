using System.Globalization;
using System.Text;
using QuickEmoji.Core.Emoji;

namespace QuickEmoji.Core.Tests.Emoji;

/// <summary>
/// An independent reading of the pinned emoji-test.txt (tests/data/unicode), used as the oracle for catalog tests.
/// It deliberately does not reuse DataBuilder's parser.
/// </summary>
internal static class PinnedEmojiTest
{
    private static readonly Lazy<IReadOnlyList<(string Emoji, EmojiVersion Version, string Name)>> Lazy = new(Load);

    public static IReadOnlyList<(string Emoji, EmojiVersion Version, string Name)> FullyQualified => Lazy.Value;

    public static string Sequence(params int[] codePoints) =>
        string.Concat(codePoints.Select(cp => new Rune(cp).ToString()));

    private static IReadOnlyList<(string, EmojiVersion, string)> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "unicode", "emoji-test-18.0.txt");
        var result = new List<(string, EmojiVersion, string)>();
        foreach (var line in File.ReadLines(path))
        {
            var parts = line.Split(';', 2);
            if (line.StartsWith('#') || parts.Length < 2 || !parts[1].TrimStart().StartsWith("fully-qualified", StringComparison.Ordinal))
            {
                continue;
            }

            var emoji = string.Concat(parts[0].Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(h => new Rune(int.Parse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString()));
            var comment = parts[1][(parts[1].IndexOf('#', StringComparison.Ordinal) + 1)..].Trim();
            var fields = comment.Split(' ', 3);
            result.Add((emoji, EmojiVersion.Parse(fields[1][1..]), fields[2]));
        }

        return result;
    }
}
