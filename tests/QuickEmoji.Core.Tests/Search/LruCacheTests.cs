using QuickEmoji.Core.Search;

namespace QuickEmoji.Core.Tests.Search;

public sealed class LruCacheTests
{
    [Fact]
    public void Set_then_get_returns_the_value()
    {
        var cache = new LruCache<string, int>(4);

        cache.Set("a", 1);

        Assert.True(cache.TryGet("a", out var value));
        Assert.Equal(1, value);
    }

    [Fact]
    public void Missing_key_returns_false()
    {
        var cache = new LruCache<string, int>(4);

        Assert.False(cache.TryGet("missing", out _));
    }

    [Fact]
    public void Over_capacity_evicts_the_least_recently_used_entry()
    {
        var cache = new LruCache<string, int>(2);

        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.Set("c", 3); // evicts "a"

        Assert.False(cache.TryGet("a", out _));
        Assert.True(cache.TryGet("b", out _));
        Assert.True(cache.TryGet("c", out _));
    }

    [Fact]
    public void Getting_an_entry_marks_it_recently_used_so_it_survives_eviction()
    {
        var cache = new LruCache<string, int>(2);
        cache.Set("a", 1);
        cache.Set("b", 2);

        cache.TryGet("a", out _); // "a" is now more recent than "b"
        cache.Set("c", 3); // evicts "b" instead of "a"

        Assert.True(cache.TryGet("a", out _));
        Assert.False(cache.TryGet("b", out _));
        Assert.True(cache.TryGet("c", out _));
    }

    [Fact]
    public void Setting_an_existing_key_updates_the_value_without_growing()
    {
        var cache = new LruCache<string, int>(2);
        cache.Set("a", 1);
        cache.Set("a", 2);

        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet("a", out var value));
        Assert.Equal(2, value);
    }

    [Fact]
    public void Count_never_exceeds_capacity()
    {
        var cache = new LruCache<string, int>(3);

        for (var i = 0; i < 100; i++)
        {
            cache.Set(i.ToString(), i);
        }

        Assert.Equal(3, cache.Count);
    }
}
