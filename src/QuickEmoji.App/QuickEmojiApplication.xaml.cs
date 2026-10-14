using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using Windows.Graphics;

namespace QuickEmoji.App;

public partial class QuickEmojiApplication : Application
{
    private readonly LaunchOptions _options;
    private Window? _window;

    internal QuickEmojiApplication(LaunchOptions options)
    {
        _options = options;
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = CreateStatusWindow();

        if (_options.StartupCheck)
        {
            // Proves XAML, the resource system and window creation work, then exits without showing UI.
            Log.Information("Startup check passed; exiting");
            DispatcherQueue.GetForCurrentThread().TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                _window.Close();
                Exit();
            });
            return;
        }

        _window.Closed += (_, _) => Exit();
        _window.Activate();
        Log.Information("Status window shown");
    }

    private static Window CreateStatusWindow()
    {
        var window = new Window
        {
            Title = "QuickEmoji",
            Content = new TextBlock
            {
                Text = "QuickEmoji is running. Close this window to exit.",
                Margin = new Thickness(24),
                TextWrapping = TextWrapping.Wrap,
            },
        };
        window.AppWindow.Resize(new SizeInt32(480, 160));
        return window;
    }
}
