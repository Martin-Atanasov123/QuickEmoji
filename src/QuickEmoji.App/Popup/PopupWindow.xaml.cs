using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using QuickEmoji.App.Insertion;
using QuickEmoji.Core.Search;
using QuickEmoji.Platform.Clipboard;
using QuickEmoji.Platform.Display;
using QuickEmoji.Platform.Targeting;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using WinRT.Interop;

namespace QuickEmoji.App.Popup;

/// <summary>
/// The popup (SPEC §6.1, §2). Created once at startup (CLAUDE.md gotcha: re-creating it breaks the 50 ms show
/// budget) and shown or hidden thereafter. Enter/Alt+1-3/click insert via <see cref="InsertionCoordinator"/> when
/// a target was captured (hotkey mode), or copy when there is none (tray mode, SPEC §6.3). Ctrl+Enter always copies.
/// </summary>
public sealed partial class PopupWindow : Window
{
    private const double EffectiveWidth = 400;
    private const int ShowAnimationMs = 120;
    private const double ShowTranslateY = 6;

    private readonly InsertionCoordinator _insertion;
    private readonly ClipboardGuard _clipboard;
    private readonly Action<string> _showToast;
    private readonly UISettings _uiSettings = new();
    private readonly nint _hwnd;
    private bool _activatedByShow;
    private TargetContext? _target;

    public PopupWindow(EmojiSearchEngine engine, InsertionCoordinator insertion, ClipboardGuard clipboard, Action<string> showToast)
    {
        _insertion = insertion;
        _clipboard = clipboard;
        _showToast = showToast;
        ViewModel = new PopupViewModel(engine, OnCommitInsertAsync, OnCommitCopyAsync);

        InitializeComponent();
        _hwnd = WindowNative.GetWindowHandle(this);

        ConfigureWindow();
        Activated += OnActivated;
    }

    public PopupViewModel ViewModel { get; }

    /// <summary>
    /// Shows the popup positioned per <paramref name="mode"/> (SPEC §6.1). Pass the <see cref="TargetContext"/>
    /// captured inside the hotkey handler so the caret rectangle reflects the app the user was typing into; pass
    /// null from the tray (no target — SPEC §6.3, "left-click opens the popup centered").
    /// </summary>
    public void Show(PopupPositionMode mode, TargetContext? target)
    {
        _target = target;
        ViewModel.Reset();
        Position(mode, target);

        _activatedByShow = true;
        AppWindow.Show();
        // AppWindow.Show() alone only makes the window visible; WinUI 3's own Activate() is not reliable enough
        // here to take keyboard focus away from whatever the user was typing into, so this calls
        // SetForegroundWindow directly (CLAUDE.md gotcha: "Activate the popup inside the handler").
        Activate();
        WindowChrome.BringToForeground(_hwnd);
        SearchBox.Focus(FocusState.Programmatic);
        PlayShowAnimation();
    }

    /// <summary>Hides with no exit animation (SPEC §6.1) and no other side effect — used for Esc, the hotkey toggling off, and losing activation.</summary>
    public void Hide()
    {
        AppWindow.Hide();
    }

    private void ConfigureWindow()
    {
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
        ApplyBackdrop();
    }

    private void ApplyBackdrop()
    {
        var useAcrylic = DesktopAcrylicController.IsSupported()
            && _uiSettings.AdvancedEffectsEnabled
            && Windows.System.Power.PowerManager.EnergySaverStatus != Windows.System.Power.EnergySaverStatus.On;

        if (useAcrylic)
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
            RootGrid.Background = null;
        }
        else
        {
            // SPEC §6.1 fallback: Windows 10, transparency off, or battery saver — a solid theme color.
            RootGrid.Background = (Brush)Application.Current.Resources["SolidBackgroundFillColorBaseBrush"];
        }
    }

    private void Position(PopupPositionMode mode, TargetContext? target)
    {
        var caret = target is { CaretRectangle.IsEmpty: false } t
            ? new Rect(t.CaretRectangle.Left, t.CaretRectangle.Top, t.CaretRectangle.Right, t.CaretRectangle.Bottom)
            : (Rect?)null;
        var cursor = MonitorQuery.GetCursorPosition();
        var monitor = MonitorQuery.FromPoint(caret?.ToPoint() ?? cursor);

        var contentHeight = RootGrid.ActualHeight > 0 ? RootGrid.ActualHeight : 212; // measured fallback before first layout
        var placed = PopupPlacement.Compute(mode, new PopupSize(EffectiveWidth, contentHeight), monitor, caret, cursor);

        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(placed.Position.X, placed.Position.Y, placed.Width, placed.Height));
    }

    private void PlayShowAnimation()
    {
        if (!_uiSettings.AnimationsEnabled)
        {
            RootGrid.Opacity = 1;
            ShowTransform.Y = 0;
            return;
        }

        RootGrid.Opacity = 0;
        ShowTransform.Y = ShowTranslateY;

        var fade = new DoubleAnimation { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(ShowAnimationMs)) };
        Storyboard.SetTarget(fade, RootGrid);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var translate = new DoubleAnimation { From = ShowTranslateY, To = 0, Duration = new Duration(TimeSpan.FromMilliseconds(ShowAnimationMs)) };
        Storyboard.SetTarget(translate, ShowTransform);
        Storyboard.SetTargetProperty(translate, "Y");

        var storyboard = new Storyboard();
        storyboard.Children.Add(fade);
        storyboard.Children.Add(translate);
        storyboard.Begin();
    }

    private void OnActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState == WindowActivationState.Deactivated && !_activatedByShow)
        {
            Hide(); // "Click outside / popup loses activation → close as Esc" (SPEC §2).
        }

        _activatedByShow = false;
    }

    // async void is the accepted shape for a top-level WinUI input event handler (there is no caller to await it).
    private async void SearchBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = IsDown(VirtualKey.Control);
        var alt = IsDown(VirtualKey.Menu);
        var shift = IsDown(VirtualKey.Shift);

        switch (e.Key)
        {
            case VirtualKey.Escape:
                Hide();
                e.Handled = true;
                return;
            case VirtualKey.Enter when ctrl:
                e.Handled = true;
                await ViewModel.CopySelectedAsync();
                return;
            case VirtualKey.Enter:
                e.Handled = true;
                await ViewModel.CommitSelectedAsync();
                return;
            case VirtualKey.Up:
                ViewModel.MoveSelection(-1);
                e.Handled = true;
                return;
            case VirtualKey.Down:
                ViewModel.MoveSelection(1);
                e.Handled = true;
                return;
            case VirtualKey.Tab:
                ViewModel.MoveSelection(shift ? -1 : 1);
                e.Handled = true;
                return;
            case VirtualKey.Number1 when alt:
                e.Handled = true;
                await ViewModel.CommitRowAsync(0);
                return;
            case VirtualKey.Number2 when alt:
                e.Handled = true;
                await ViewModel.CommitRowAsync(1);
                return;
            case VirtualKey.Number3 when alt:
                e.Handled = true;
                await ViewModel.CommitRowAsync(2);
                return;
        }
    }

    private async void ResultRow_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ResultRowViewModel row })
        {
            var index = ViewModel.Results.IndexOf(row);
            if (index >= 0)
            {
                await ViewModel.CommitRowAsync(index);
            }
        }
    }

    /// <summary>
    /// Enter / Alt+1-3 / row click: real insertion when a target was captured (hotkey mode); with no target
    /// (tray mode — SPEC §6.3, "there is no target; Enter copies instead"), falls back to copy.
    /// </summary>
    private async Task OnCommitInsertAsync(string emoji)
    {
        Hide();
        if (_target is { } target)
        {
            await _insertion.InsertAsync(target, emoji);
        }
        else
        {
            _clipboard.Copy(emoji);
            _showToast($"Copied {emoji} — press Ctrl+V");
        }
    }

    /// <summary>Ctrl+Enter: always copies, never inserts (SPEC §2).</summary>
    private Task OnCommitCopyAsync(string emoji)
    {
        Hide();
        _clipboard.Copy(emoji);
        _showToast($"Copied {emoji} — press Ctrl+V");
        return Task.CompletedTask;
    }

    private static bool IsDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
}

file static class RectExtensions
{
    public static Point ToPoint(this Rect rect) => new(rect.Left, rect.Top);
}
