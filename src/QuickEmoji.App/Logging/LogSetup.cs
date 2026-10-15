using QuickEmoji.Core;
using Serilog;
using Serilog.Events;

namespace QuickEmoji.App.Logging;

/// <summary>Serilog configuration: rolling text logs, 7 files of up to 1 MB (SPEC §7).</summary>
internal static class LogSetup
{
    private const long MaxFileBytes = 1024 * 1024;
    private const int RetainedFiles = 7;
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    public static ILogger CreateLogger(AppPaths paths) =>
        new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .WriteTo.File(
                Path.Combine(paths.LogDirectory, "quickemoji.log"),
                restrictedToMinimumLevel: LogEventLevel.Verbose,
                outputTemplate: OutputTemplate,
                fileSizeLimitBytes: MaxFileBytes,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: RetainedFiles,
                // SPEC §8 allows a second process to run briefly (it logs a line or two before signalling the
                // first instance and exiting); without shared access its writes to this file are silently lost.
                shared: true)
            .CreateLogger();
}
