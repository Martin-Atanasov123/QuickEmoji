using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace QuickEmoji.App.Diagnostics;

/// <summary>
/// Builds the plain-text report behind Settings → About → "Copy diagnostics" (SPEC §6.2): OS build, app
/// version, hotkey state, and the last 20 insertion outcomes — explicitly never any text the user typed.
/// </summary>
public static class DiagnosticsReport
{
    public static string Build(string appVersion, string hotkeyDescription, bool hotkeyPaused, IReadOnlyList<string> recentInsertionOutcomes)
    {
        ArgumentNullException.ThrowIfNull(appVersion);
        ArgumentNullException.ThrowIfNull(hotkeyDescription);
        ArgumentNullException.ThrowIfNull(recentInsertionOutcomes);

        var report = new StringBuilder();
        report.AppendLine("QuickEmoji diagnostics");
        report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"OS: {Environment.OSVersion.VersionString} ({RuntimeInformation.ProcessArchitecture})"));
        report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"App version: {appVersion}"));
        report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Hotkey: {hotkeyDescription} ({(hotkeyPaused ? "paused" : "active")})"));
        report.AppendLine("Last insertion outcomes:");
        if (recentInsertionOutcomes.Count == 0)
        {
            report.AppendLine("  (none yet)");
        }
        else
        {
            foreach (var outcome in recentInsertionOutcomes)
            {
                report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  {outcome}"));
            }
        }

        return report.ToString();
    }
}
