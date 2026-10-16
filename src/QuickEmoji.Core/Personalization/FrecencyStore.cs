using QuickEmoji.Core.Search;

namespace QuickEmoji.Core.Personalization;

/// <summary>One per-query raw count, as persisted to <c>usage.json</c> (SPEC §7).</summary>
public sealed record FrecencyQueryEntry(string Query, string Emoji, double Value, DateTimeOffset Updated);

/// <summary>One global raw count, as persisted to <c>usage.json</c> (SPEC §7).</summary>
public sealed record FrecencyGlobalEntry(string Emoji, double Value, DateTimeOffset Updated);

/// <summary>A full dump of a <see cref="FrecencyStore"/>, for <see cref="UsageStore"/> to serialize.</summary>
public sealed record FrecencySnapshot(IReadOnlyList<FrecencyQueryEntry> ByQuery, IReadOnlyList<FrecencyGlobalEntry> Global);

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

    /// <summary>Rebuilds a store from a previously-saved <see cref="Snapshot"/> (SPEC §7: <c>usage.json</c>).</summary>
    public static FrecencyStore FromSnapshot(FrecencySnapshot snapshot, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var store = new FrecencyStore(time);
        foreach (var entry in snapshot.ByQuery)
        {
            store._byQuery[(entry.Query, entry.Emoji)] = new DecayingCount(entry.Value, entry.Updated);
        }

        foreach (var entry in snapshot.Global)
        {
            store._global[entry.Emoji] = new DecayingCount(entry.Value, entry.Updated);
        }

        return store;
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

    /// <summary>Settings → Search → "clear usage history" (SPEC §6.2): discards every count, per-query and global alike.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _byQuery.Clear();
            _global.Clear();
        }
    }

    /// <summary>A point-in-time dump of every count, raw (un-decayed) value plus its last-updated timestamp, so decay resumes correctly after a restart.</summary>
    public FrecencySnapshot Snapshot()
    {
        lock (_lock)
        {
            var byQuery = _byQuery.Select(kv => new FrecencyQueryEntry(kv.Key.Query, kv.Key.Emoji, kv.Value.Value, kv.Value.Updated)).ToList();
            var global = _global.Select(kv => new FrecencyGlobalEntry(kv.Key, kv.Value.Value, kv.Value.Updated)).ToList();
            return new FrecencySnapshot(byQuery, global);
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
