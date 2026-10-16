using System.Diagnostics;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using QuickEmoji.Core;
using QuickEmoji.Core.Emoji;
using QuickEmoji.Core.Insertion;
using QuickEmoji.Core.Settings;
using QuickEmoji.Platform.Display;
using Windows.System;
using WinRT.Interop;

namespace QuickEmoji.App.Settings;

/// <summary>
/// The Settings window (SPEC §6.2): Mica backdrop, ~720×560, standard WinUI controls. Unlike the popup/toast,
/// this is a normal titled window — created on demand (it is not latency-critical, so Stage 3's "create once"
/// gotcha does not apply here) and closed, not hidden, when the user is done with it.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private const int EffectiveWidth = 720;
    private const int EffectiveHeight = 560;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        var hwnd = WindowNative.GetWindowHandle(this);
        AppWindow.Title = "QuickEmoji Settings";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(EffectiveWidth, EffectiveHeight));
        if (DesktopAcrylicController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop();
        }

        WindowChrome.ApplyRoundedCorners(hwnd);

        SelectByTag(PopupPositionButtons, ViewModel.PopupPosition.ToString());
        SelectByTag(ThemeButtons, ViewModel.Theme.ToString());
        SelectByTag(SkinToneButtons, ViewModel.PreferredSkinTone.ToString());
        SelectByTag(DefaultMethodButtons, ViewModel.DefaultInsertionMethod.ToString());
    }

    public SettingsViewModel ViewModel { get; }

    private void RecordHotkeyButton_Click(object sender, RoutedEventArgs e) => ViewModel.BeginRecordingHotkey();

    private void RecordHotkeyButton_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!ViewModel.IsRecordingHotkey)
        {
            return;
        }

        e.Handled = true;
        var ctrl = IsDown(VirtualKey.Control);
        var alt = IsDown(VirtualKey.Menu);
        var shift = IsDown(VirtualKey.Shift);
        var win = IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows);
        ViewModel.OnRecorderKeyDown(e.Key, ctrl, alt, shift, win);
    }

    private void PopupPositionButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedTag(PopupPositionButtons) is { } tag && Enum.TryParse<PopupPosition>(tag, out var value))
        {
            ViewModel.PopupPosition = value;
        }
    }

    private void ThemeButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedTag(ThemeButtons) is { } tag && Enum.TryParse<AppTheme>(tag, out var value))
        {
            ViewModel.Theme = value;
        }
    }

    private void SkinToneButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedTag(SkinToneButtons) is { } tag && Enum.TryParse<SkinTone>(tag, out var value))
        {
            ViewModel.PreferredSkinTone = value;
        }
    }

    private void DefaultMethodButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedTag(DefaultMethodButtons) is { } tag && Enum.TryParse<InsertionMethod>(tag, out var value))
        {
            ViewModel.DefaultInsertionMethod = value;
        }
    }

    private void AddOverrideRow_Click(object sender, RoutedEventArgs e) => ViewModel.AddOverrideRow();

    private void RemoveOverrideRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AppOverrideRowViewModel row })
        {
            ViewModel.RemoveOverrideRow(row);
        }
    }

    private void OpenLogFolder_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppPaths.ForCurrentUser().LogDirectory}\""));

    private void CopyDiagnostics_Click(object sender, RoutedEventArgs e) => ViewModel.CopyDiagnostics();

    private void ClearUsageHistory_Click(object sender, RoutedEventArgs e) => ViewModel.ClearUsageHistory();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Save();
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private static void SelectByTag(RadioButtons control, string tag)
    {
        foreach (var item in control.Items)
        {
            if (item is RadioButton { Tag: string itemTag } button && itemTag == tag)
            {
                control.SelectedItem = button;
                return;
            }
        }
    }

    private static string? SelectedTag(RadioButtons control) =>
        control.SelectedItem is RadioButton { Tag: string tag } ? tag : null;

    private static bool IsDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
}
