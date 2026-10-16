using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Serilog;
using WinUIUnhandledExceptionEventArgs = Microsoft.UI.Xaml.UnhandledExceptionEventArgs;

namespace QuickEmoji.App.Reliability;

/// <summary>
/// SPEC §8: "global handlers log the exception and keep the tray process alive. A failure inside the popup must
/// never take down the app." Split into two install points because the three sources have different lifetimes:
/// <see cref="InstallProcessWide"/> (AppDomain, TaskScheduler) has no dependency on WinUI and should run as early
/// as possible in <c>Program.Main</c>, before <c>Application.Start</c>; <see cref="InstallFor"/> needs the
/// running <see cref="Application"/> instance, so it runs from <c>QuickEmojiApplication</c>'s constructor.
/// </summary>
public static class GlobalExceptionHandlers
{
    public static void InstallProcessWide()
    {
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public static void InstallFor(Application app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UnhandledException += OnApplicationUnhandledException;
    }

    /// <summary>
    /// A true AppDomain-level unhandled exception (one that escaped every other handler, including WinUI's own)
    /// cannot actually be kept alive — .NET terminates the process after this event regardless of what the
    /// handler does (<see cref="System.UnhandledExceptionEventArgs"/> has no way to cancel that). This exists so
    /// the crash is logged with its real stack trace before the process goes down, rather than disappearing.
    /// </summary>
    private static void OnAppDomainUnhandledException(object? sender, System.UnhandledExceptionEventArgs e) =>
        Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception escaped every other handler; the process will exit (IsTerminating={IsTerminating})", e.IsTerminating);

    /// <summary>
    /// An unobserved faulted <see cref="Task"/>'s exception is rethrown on the finalizer thread when nothing
    /// ever awaited or inspected it, which crashes the process in the same unrecoverable way as the AppDomain
    /// case above. <see cref="UnobservedTaskExceptionEventArgs.SetObserved"/> marks it handled, preventing that.
    /// </summary>
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }

    /// <summary>
    /// Everything that runs on the UI thread's dispatch loop — a keymap handler, a button click, a timer tick —
    /// funnels an otherwise-unhandled exception through here first. Marking it handled is what actually keeps
    /// the tray process alive for this, the most common real-world source (SPEC §8's "failure inside the popup").
    /// </summary>
    private static void OnApplicationUnhandledException(object sender, WinUIUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI exception; keeping the app running");
        e.Handled = true;
    }
}
