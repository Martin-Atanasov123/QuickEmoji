using QuickEmoji.Core.Emoji;
using QuickEmoji.Core.Emoji.Serialization;
using QuickEmoji.DataBuilder.Building;
using QuickEmoji.DataBuilder.Output;
using QuickEmoji.DataBuilder.Parsing;
using QuickEmoji.DataBuilder.Sources;

namespace QuickEmoji.DataBuilder;

/// <summary>The whole pipeline: fetch pinned sources, merge, write emoji.json, then verify it through Core's loader.</summary>
internal sealed class EmojiDataBuild(RepositoryLayout layout, SourceCache cache)
{
    public async Task RunAsync(bool offline, CancellationToken cancellationToken)
    {
        var paths = new Dictionary<SourceFile, string>();
        foreach (var source in SourceManifest.All)
        {
            paths[source] = await cache.GetAsync(source, offline, cancellationToken);
        }

        var emojiTest = EmojiTestParser.ParseFullyQualified(new StringReader(await File.ReadAllTextAsync(paths[SourceManifest.EmojiTest], cancellationToken)));
        var sources = new SourceData(
            emojiTest,
            CldrAnnotationParser.Merge(Cldr(paths[SourceManifest.CldrEnglish]), Cldr(paths[SourceManifest.CldrEnglishDerived])),
            CldrAnnotationParser.Merge(Cldr(paths[SourceManifest.CldrBulgarian]), Cldr(paths[SourceManifest.CldrBulgarianDerived])),
            Parse(paths[SourceManifest.Gemoji], GemojiParser.Parse),
            Parse(layout.SynonymsPath, CuratedDataParser.ParseSynonyms),
            Parse(layout.PopularityPath, CuratedDataParser.ParsePopularity),
            Parse(layout.ShortcodeExclusionsPath, CuratedDataParser.ParseShortcodeExclusions));

        var versions = new EmojiDataSources
        {
            EmojiTest = SourceManifest.EmojiVersion,
            Cldr = SourceManifest.CldrVersion,
            Gemoji = SourceManifest.GemojiVersion,
        };
        var data = EmojiDataAssembler.Assemble(sources, versions);
        var bytes = EmojiJsonWriter.ToUtf8(data);
        Verify(bytes, emojiTest);

        await File.WriteAllBytesAsync(layout.EmojiJsonPath, bytes, cancellationToken);
        PrintSummary(data, sources, bytes.Length);
    }

    /// <summary>Loads the bytes with the runtime loader and checks every fully-qualified sequence survived exactly once.</summary>
    private static void Verify(byte[] bytes, IReadOnlyList<EmojiTestRecord> emojiTest)
    {
        using var stream = new MemoryStream(bytes);
        var catalog = EmojiCatalogLoader.Load(stream);
        var loaded = catalog.AllSequences().Select(s => s.Emoji).ToList();
        var expected = emojiTest.Select(r => r.Emoji).ToList();
        if (loaded.Count != expected.Count || !new HashSet<string>(loaded, StringComparer.Ordinal).SetEquals(expected))
        {
            throw new InvalidDataException(
                $"Round-trip check failed: wrote {loaded.Count} sequences, emoji-test.txt has {expected.Count} fully-qualified ones.");
        }
    }

    private static void PrintSummary(EmojiDataFile data, SourceData sources, int size)
    {
        var variants = data.Emoji.Sum(e => e.Variants?.Count ?? 0);
        Console.WriteLine($"Wrote {data.Emoji.Count} base emoji with {variants} skin-tone variants ({size / 1024} KiB).");
        Console.WriteLine($"  without CLDR English name: {data.Emoji.Count(e => !sources.CldrEnglish.ContainsKey(EmojiKey.Of(e.Emoji)))}");
        Console.WriteLine($"  without CLDR Bulgarian name: {data.Emoji.Count(e => e.NameBg is null)}");
        Console.WriteLine($"  with gemoji shortcodes: {data.Emoji.Count(e => e.Shortcodes is not null)}");
        Console.WriteLine($"  with synonyms: {data.Emoji.Count(e => e.Synonyms is not null)} ({sources.Synonyms.Count} synonym words)");
        Console.WriteLine($"  with popularity rank: {data.Emoji.Count(e => e.Popularity is not null)}");
    }

    private static Dictionary<string, CldrAnnotation> Cldr(string path) => Parse(path, CldrAnnotationParser.Parse);

    private static T Parse<T>(string path, Func<Stream, T> parser)
    {
        using var stream = File.OpenRead(path);
        return parser(stream);
    }
}
