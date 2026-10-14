namespace QuickEmoji.E2E;

/// <summary>Finds the QuickEmoji.exe produced by the App project's most recent build.</summary>
internal static class AppUnderTest
{
    public static string LocateExecutable()
    {
        var appBin = Path.Combine(FindRepositoryRoot(), "src", "QuickEmoji.App", "bin");
        var newest = Directory.Exists(appBin)
            ? Directory.EnumerateFiles(appBin, "QuickEmoji.exe", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}publish{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;

        return newest ?? throw new FileNotFoundException(
            $"QuickEmoji.exe was not found under '{appBin}'. Build the solution before running E2E tests.");
    }

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "QuickEmoji.sln")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find QuickEmoji.sln above the test output directory.");
    }
}
