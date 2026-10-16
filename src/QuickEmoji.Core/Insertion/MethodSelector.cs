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
    /// <summary>
    /// <paramref name="defaultMethod"/> is SPEC §6.2's Settings → Insertion → "default method (Auto or
    /// Clipboard)" — Auto is <see cref="InsertionMethod.UnicodeInjection"/>, Clipboard is
    /// <see cref="InsertionMethod.ClipboardPaste"/>. It never overrides the elevation safety check above it, and
    /// <see cref="InsertionMethod.Copy"/> is not a meaningful default (callers should not pass it; if they do, it
    /// is honored as "always copy" rather than rejected, since nothing about that choice is unsafe).
    /// </summary>
    public static MethodSelection Select(
        string? processName,
        bool isElevatedOrUnknown,
        IReadOnlyList<AppOverride> overrides,
        InsertionMethod defaultMethod = InsertionMethod.UnicodeInjection)
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

        return new MethodSelection(defaultMethod, PasteKeys.CtrlV, "default");
    }
}
