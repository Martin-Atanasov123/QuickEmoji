using QuickEmoji.Core.Search;

namespace QuickEmoji.Core.Personalization;

/// <summary>
/// Counts emoji picks per (normalised query, emoji) and globally, with exponential decay
/// (half-life <see cref="RankingWeights.FrecencyHalfLife"/>, SPEC §3.4).
/// </summary>
public sealed class FrecencyStore
{
    private readonly TimeProvider _time;
    private readonly Dictionary<(string Query, string Emoji), DecayingCount> _byQuery = [];
    private readonly Dictionary<string, DecayingCount> _global = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    public FrecencyStore(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Records one pick. <paramref name="normalizedQuery"/> is empty for the empty-query state.</summary>
    public void RecordUse(string normalizedQuery, string emoji)
    {
        ArgumentNullException.ThrowIfNull(normalizedQuery);
        ArgumentException.ThrowIfNullOrEmpty(emoji);
        var now = _time.GetUtcNow();
        lock (_lock)
        {
            if (normalizedQuery.Length > 0)
            {
                _byQuery[(normalizedQuery, emoji)] = _byQuery.GetValueOrDefault((normalizedQuery, emoji)).Add(now);
            }

            _global[emoji] = _global.GetValueOrDefault(emoji).Add(now);
        }
    }

    /// <summary>Decayed number of times <paramref name="emoji"/> was picked for exactly this query.</summary>
    public double QueryUses(string normalizedQuery, string emoji)
    {
        var now = _time.GetUtcNow();
        lock (_lock)
        {
            return _byQuery.TryGetValue((normalizedQuery, emoji), out var count) ? count.ValueAt(now) : 0;
        }
    }

    /// <summary>Decayed number of times <paramref name="emoji"/> was picked for any query.</summary>
    public double GlobalUses(string emoji)
    {
        var now = _time.GetUtcNow();
        lock (_lock)
        {
            return _global.TryGetValue(emoji, out var count) ? count.ValueAt(now) : 0;
        }
    }

    /// <summary>The most used emoji by decayed global count, most used first.</summary>
    public IReadOnlyList<string> MostUsed(int count)
    {
        var now = _time.GetUtcNow();
        lock (_lock)
        {
            return _global
                .Select(p => (Emoji: p.Key, Uses: p.Value.ValueAt(now)))
                .OrderByDescending(p => p.Uses)
                .ThenBy(p => p.Emoji, StringComparer.Ordinal)
                .Take(count)
                .Select(p => p.Emoji)
                .ToList();
        }
    }

    private readonly record struct DecayingCount(double Value, DateTimeOffset Updated)
    {
        public double ValueAt(DateTimeOffset now)
        {
            if (Value == 0)
            {
                return 0;
            }

            var elapsed = Math.Max(0, (now - Updated).TotalSeconds);
            return Value * Math.Pow(0.5, elapsed / RankingWeights.FrecencyHalfLife.TotalSeconds);
        }

        public DecayingCount Add(DateTimeOffset now) => new(ValueAt(now) + 1, now);
    }
}
