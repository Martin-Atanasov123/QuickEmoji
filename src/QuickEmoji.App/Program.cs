using System.Reflection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using QuickEmoji.App.Logging;
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
        try
        {
            Log.Information(
                "QuickEmoji {Version} starting on {OsVersion} ({Architecture}), options {@Options}",
                Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                Environment.OSVersion.VersionString,
                System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture,
                options);

            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start(callbackParams =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
                _ = new QuickEmojiApplication(options);
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
