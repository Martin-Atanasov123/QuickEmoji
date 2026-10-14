namespace QuickEmoji.Core;

/// <summary>
/// Per-user file locations under <c>%LOCALAPPDATA%\QuickEmoji\</c> (SPEC §7).
/// </summary>
public sealed class AppPaths
{
    public const string FolderName = "QuickEmoji";

    public AppPaths(string localAppDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localAppDataRoot);
        if (!Path.IsPathFullyQualified(localAppDataRoot))
        {
            throw new ArgumentException("The local app data root must be an absolute path.", nameof(localAppDataRoot));
        }

        Root = Path.Combine(localAppDataRoot, FolderName);
    }

    public string Root { get; }

    public string SettingsFile => Path.Combine(Root, "settings.json");

    public string UsageFile => Path.Combine(Root, "usage.json");

    public string LogDirectory => Path.Combine(Root, "logs");

    public static AppPaths ForCurrentUser() =>
        new(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify));
}
