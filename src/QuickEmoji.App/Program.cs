using System.Reflection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using QuickEmoji.App.Logging;
using QuickEmoji.App.Reliability;
using QuickEmoji.Core;
using Serilog;

namespace QuickEmoji.App;

public static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var options = LaunchOptions.Parse(args);
        Log.Logger = LogSetup.CreateLogger(AppPaths.ForCurrentUser());
        // SPEC §8: as early as possible, before anything (including Application.Start) can throw.
        GlobalExceptionHandlers.InstallProcessWide();
        try
        {
            Log.Information(
                "QuickEmoji {Version} starting on {OsVersion} ({Architecture}), options {@Options}",
                Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                Environment.OSVersion.VersionString,
                System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture,
                options);

            var singleInstance = new SingleInstanceGuard();
            if (!singleInstance.IsFirstInstance)
            {
                // SPEC §8: a second launch signals the first instance, then exits.
                Log.Information("Another instance is already running; signalling it and exiting");
                singleInstance.SignalExistingInstanceAndExit();
                singleInstance.Dispose();
                return 0;
            }

            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(callbackParams =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
                _ = new QuickEmojiApplication(options, singleInstance);
            });

            Log.Information("QuickEmoji exited normally");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "QuickEmoji terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
