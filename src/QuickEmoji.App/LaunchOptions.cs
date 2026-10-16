namespace QuickEmoji.App;

/// <summary>Command-line switches understood by the app.</summary>
internal sealed record LaunchOptions(bool StartupCheck, bool Background, bool PerfTrace)
{
    /// <summary>Initialise the app fully, then exit with code 0. Used by CI-style checks and the E2E smoke test.</summary>
    public const string StartupCheckArgument = "--startup-check";

    /// <summary>
    /// What <see cref="QuickEmoji.Platform.Startup.RunKeyStartup"/> puts on the Run-key command line (SPEC §7).
    /// The app's own startup behavior is identical either way — it always starts as a tray-only process with no
    /// visible window — so this is currently just recorded for diagnostics/logging, not branched on.
    /// </summary>
    public const string BackgroundArgument = "--background";

    /// <summary>Logs cold-start time and idle working set (SPEC §9), gated so measuring costs nothing normally.</summary>
    public const string PerfTraceArgument = "--perf";

    public static LaunchOptions Parse(IEnumerable<string> args)
    {
        var list = args as ICollection<string> ?? args.ToList();
        return new(
            StartupCheck: list.Contains(StartupCheckArgument, StringComparer.OrdinalIgnoreCase),
            Background: list.Contains(BackgroundArgument, StringComparer.OrdinalIgnoreCase),
            PerfTrace: list.Contains(PerfTraceArgument, StringComparer.OrdinalIgnoreCase));
    }
}
