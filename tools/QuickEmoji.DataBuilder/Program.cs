using QuickEmoji.DataBuilder;

var layout = RepositoryLayout.Find(Environment.CurrentDirectory);

if (args is ["--paths"])
{
    Console.WriteLine($"Repository root: {layout.Root}");
    Console.WriteLine($"Output:          {layout.EmojiJsonPath}");
    Console.WriteLine($"Download cache:  {layout.CacheDirectory}");
    return 0;
}

Console.Error.WriteLine("Usage: dotnet run --project tools/QuickEmoji.DataBuilder -- --paths");
return 2;
