using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using QuickEmoji.Platform.Display;
using WinRT.Interop;

namespace QuickEmoji.App.Toast;

/// <summary>
/// The transient toast (SPEC §4.6): a small window near the popup's last position. It does not take focus and
/// auto-hides after 2.5 s. Created once at startup, like the popup; shown and hidden thereafter.
/// </summary>
public sealed partial class ToastWindow : Window
{
    private static readonly TimeSpan AutoHideAfter = TimeSpan.FromSeconds(2.5);

    private readonly DispatcherQueueTimer _hideTimer;
    private readonly nint _hwnd;

    public ToastWindow()
    {
        InitializeComponent();
        _hwnd = WindowNative.GetWindowHandle(this);

        AppWindow.IsShownInSwitchers = false;
        AppWindow.Title = "QuickEmoji";
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsAlwaysOnTop = true;
        }

        WindowChrome.ApplyRoundedCorners(_hwnd);

        _hideTimer = DispatcherQueue.CreateTimer();
        _hideTimer.Interval = AutoHideAfter;
        _hideTimer.IsRepeating = false;
        _hideTimer.Tick += (_, _) => Hide();
    }

    /// <summary>Shows the message near <paramref name="anchor"/> (physical pixels — the popup's last on-screen position) and starts the auto-hide timer.</summary>
    public void Show(string message, Point anchor)
    {
        MessageText.Text = message;
        RootBorder.UpdateLayout();
        var width = (int)Math.Max(120, RootBorder.ActualWidth);
        var height = (int)Math.Max(40, RootBorder.ActualHeight);

        var monitor = MonitorQuery.FromPoint(anchor);
        var clampedX = Math.Clamp(anchor.X, monitor.WorkArea.Left, monitor.WorkArea.Right - width);
        var clampedY = Math.Clamp(anchor.Y, monitor.WorkArea.Top, monitor.WorkArea.Bottom - height);

        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(clampedX, clampedY, width, height));
        AppWindow.Show(activateWindow: false);

        _hideTimer.Stop();
        _hideTimer.Start();
    }

    public void Hide()
    {
        _hideTimer.Stop();
        AppWindow.Hide();
    }
}
