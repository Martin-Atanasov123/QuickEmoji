namespace QuickEmoji.Core.Insertion;

/// <summary>The chosen method, with a human-readable reason for logging (CLAUDE.md: "log insertion method... and outcome").</summary>
public sealed record MethodSelection(InsertionMethod Method, PasteKeys PasteKeys, string Reason);

/// <summary>
/// Picks the insertion method (SPEC §4.2 step 4), in priority order: an elevated or unknown-elevation target
/// always gets Copy (CLAUDE.md gotcha: SendInput into an elevated process fails silently — this must be decided
/// before attempting injection, never inferred from a failed attempt); otherwise a matching per-app override
/// wins; otherwise Unicode injection. Pure and Win32-free, so every branch is unit-testable.
/// </summary>
public static class MethodSelector
{
    public static MethodSelection Select(string? processName, bool isElevatedOrUnknown, IReadOnlyList<AppOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        if (isElevatedOrUnknown)
        {
            return new MethodSelection(InsertionMethod.Copy, PasteKeys.CtrlV, "target runs elevated");
        }

        var match = processName is null
            ? null
            : overrides.FirstOrDefault(o => string.Equals(o.ProcessName, processName, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return new MethodSelection(match.Method, match.PasteKeys, $"per-app override for {processName}");
        }

        return new MethodSelection(InsertionMethod.UnicodeInjection, PasteKeys.CtrlV, "default");
    }
}
