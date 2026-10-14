namespace QuickEmoji.App;

/// <summary>Command-line switches understood by the app.</summary>
internal sealed record LaunchOptions(bool StartupCheck)
{
    /// <summary>Initialise the app fully, then exit with code 0. Used by CI-style checks and the E2E smoke test.</summary>
    public const string StartupCheckArgument = "--startup-check";

    public static LaunchOptions Parse(IEnumerable<string> args) =>
        new(StartupCheck: args.Contains(StartupCheckArgument, StringComparer.OrdinalIgnoreCase));
}
