namespace QuickEmoji.DataBuilder;

/// <summary>Locates the repository root and the files DataBuilder reads and writes.</summary>
internal sealed class RepositoryLayout
{
    private const string SolutionFileName = "QuickEmoji.sln";

    private RepositoryLayout(string root)
    {
        Root = root;
    }

    public string Root { get; }

    public string CoreDataDirectory => Path.Combine(Root, "src", "QuickEmoji.Core", "Data");

    public string EmojiJsonPath => Path.Combine(CoreDataDirectory, "emoji.json");

    /// <summary>Downloaded source files; git-ignored.</summary>
    public string CacheDirectory => Path.Combine(Root, "tools", "QuickEmoji.DataBuilder", "cache");

    public static RepositoryLayout Find(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
            {
                return new RepositoryLayout(dir.FullName);
            }
        }

        throw new DirectoryNotFoundException($"Could not find {SolutionFileName} in '{startDirectory}' or any parent directory.");
    }
}
