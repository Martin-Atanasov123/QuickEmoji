using Microsoft.Extensions.Time.Testing;
using QuickEmoji.Core.Personalization;
using QuickEmoji.Core.Settings;
using QuickEmoji.Core.Tests.IO;

namespace QuickEmoji.Core.Tests.Personalization;

public sealed class UsageStoreTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private static (UsageStore Store, FakeFileSystem FileSystem, AppPaths Paths) Make()
    {
        var fs = new FakeFileSystem();
        var paths = new AppPaths(@"C:\fake");
        return (new UsageStore(fs, paths, new FakeTimeProvider(FixedNow)), fs, paths);
    }

    [Fact]
    public void No_file_yet_loads_an_empty_store_and_is_not_reported_as_corrupt()
    {
        var (store, _, _) = Make();

        var wasCorrupt = store.Load(out var frecency);

        Assert.False(wasCorrupt);
        Assert.Empty(frecency.MostUsed(10));
    }

    [Fact]
    public void Saving_then_loading_round_trips_use_counts_byte_for_byte()
    {
        var (store, _, _) = Make();
        var time = new FakeTimeProvider(FixedNow);
        var original = new FrecencyStore(time);
        original.RecordUse("heart", "❤️");
        original.RecordUse("heart", "❤️");
        original.RecordUse("", "😂");

        store.Save(original);
        store.Load(out var loaded);

        Assert.Equal(original.GlobalUses("❤️"), loaded.GlobalUses("❤️"));
        Assert.Equal(original.GlobalUses("😂"), loaded.GlobalUses("😂"));
        Assert.Equal(original.QueryUses("heart", "❤️"), loaded.QueryUses("heart", "❤️"));
    }

    [Fact]
    public void Unparseable_json_is_quarantined_and_an_empty_store_is_returned()
    {
        var (store, fs, paths) = Make();
        fs.Set(paths.UsageFile, "{ not valid json");

        var wasCorrupt = store.Load(out var frecency);

        Assert.True(wasCorrupt);
        Assert.Empty(frecency.MostUsed(10));
        Assert.Null(fs.Get(paths.UsageFile));
        Assert.Equal("{ not valid json", fs.Get($"{paths.UsageFile}.bad-{FixedNow:yyyyMMddHHmmss}"));
    }

    [Fact]
    public void A_malformed_entry_is_skipped_but_valid_ones_still_load()
    {
        var (store, fs, paths) = Make();
        fs.Set(paths.UsageFile, """
            { "global": [ { "emoji": "❤️" }, { "emoji": "😂", "value": 3, "updated": "2026-10-16T12:00:00+00:00" } ] }
            """);

        store.Load(out var frecency);

        Assert.Equal(0, frecency.GlobalUses("❤️"));
        Assert.True(frecency.GlobalUses("😂") > 0);
    }
}
