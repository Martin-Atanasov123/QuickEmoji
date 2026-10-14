namespace QuickEmoji.Core.Search;

/// <summary>Optimal-string-alignment Damerau-Levenshtein distance with an early exit above a bound.</summary>
internal static class DamerauLevenshtein
{
    /// <summary>True when <paramref name="a"/> and <paramref name="b"/> are at most <paramref name="max"/> edits apart.</summary>
    public static bool IsWithin(string a, string b, int max)
    {
        if (Math.Abs(a.Length - b.Length) > max)
        {
            return false;
        }

        Span<int> previous2 = stackalloc int[b.Length + 1];
        Span<int> previous = stackalloc int[b.Length + 1];
        Span<int> current = stackalloc int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowMin = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                var value = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    value = Math.Min(value, previous2[j - 2] + 1);
                }

                current[j] = value;
                rowMin = Math.Min(rowMin, value);
            }

            if (rowMin > max)
            {
                return false;
            }

            var recycled = previous2;
            previous2 = previous;
            previous = current;
            current = recycled;
        }

        return previous[b.Length] <= max;
    }
}
