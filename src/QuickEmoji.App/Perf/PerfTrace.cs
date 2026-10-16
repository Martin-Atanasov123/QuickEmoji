using System.Diagnostics;
using Serilog;

namespace QuickEmoji.App.Perf;

/// <summary>
/// Measures and logs the SPEC §9 budgets this process can observe about itself — cold start and idle working
/// set — gated behind <c>--perf</c> so measuring costs nothing in normal use. The remaining budgets (hotkey →
/// popup, keystroke → results, search latency, insertion latency) are either measured by dedicated benchmark
/// tests (<c>SearchLatencyBenchmarkTests</c>) or need a human with a stopwatch (SPEC §11's manual matrix), not
/// something a single process can self-report.
/// </summary>
public static class PerfTrace
{
    /// <summary>
    /// Wall-clock time from the OS actually creating this process to now — includes CLR/runtime startup, which
    /// a <see cref="Stopwatch"/> started inside <c>Main</c> would miss, and is what SPEC §9's "cold start → tray
    /// ready" budget means by "cold start".
    /// </summary>
    public static TimeSpan ElapsedSinceProcessStart()
    {
        using var process = Process.GetCurrentProcess();
        return DateTime.Now - process.StartTime;
    }

    public static void LogColdStart(TimeSpan elapsed) =>
        Log.Information("[perf] Cold start to tray ready: {ElapsedMs:F1} ms", elapsed.TotalMilliseconds);

    public static void LogWorkingSet()
    {
        using var process = Process.GetCurrentProcess();
        Log.Information("[perf] Working set: {WorkingSetMb:F1} MB", process.WorkingSet64 / 1024.0 / 1024.0);
    }
}
