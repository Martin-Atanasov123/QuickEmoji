using QuickEmoji.DataBuilder;
using QuickEmoji.DataBuilder.Sources;

var layout = RepositoryLayout.Find(Environment.CurrentDirectory);

switch (args)
{
    case []:
    case ["--offline"]:
        using (var http = new HttpClient())
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("QuickEmoji.DataBuilder");
            var build = new EmojiDataBuild(layout, new SourceCache(layout.CacheDirectory, http));
            await build.RunAsync(offline: args.Length == 1, CancellationToken.None);
        }

        return 0;

    case ["--paths"]:
        Console.WriteLine($"Repository root: {layout.Root}");
        Console.WriteLine($"Output:          {layout.EmojiJsonPath}");
        Console.WriteLine($"Download cache:  {layout.CacheDirectory}");
        return 0;

    default:
        Console.Error.WriteLine("Usage: dotnet run --project tools/QuickEmoji.DataBuilder [-- --offline | --paths]");
        Console.Error.WriteLine("  (no arguments)  download pinned sources if needed and regenerate src/QuickEmoji.Core/Data/emoji.json");
        Console.Error.WriteLine("  --offline       use only the local download cache");
        Console.Error.WriteLine("  --paths         print the files DataBuilder reads and writes");
        return 2;
}
