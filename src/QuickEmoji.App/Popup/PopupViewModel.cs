using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using QuickEmoji.Core.Emoji;
using QuickEmoji.Core.Search;
using QuickEmoji.Core.Settings;

namespace QuickEmoji.App.Popup;

/// <summary>
/// Search state and the keymap (SPEC §2) for the popup. UI-agnostic beyond WinUI's <c>ObservableObject</c>, so
/// the window's code-behind only wires keyboard/mouse events to these methods.
/// </summary>
public sealed partial class PopupViewModel : ObservableObject
{
    /// <summary>
    /// Not readonly: Settings → Search → "max emoji version (advanced)" rebuilds the filtered catalog (and so a
    /// new engine) on save; the popup itself is never recreated (CLAUDE.md), only this reference swapped.
    /// </summary>
    private EmojiSearchEngine _engine;

    /// <summary>Enter / Alt+1-3 / row click: real insertion (Stage 4) or, with no target, copy (SPEC §6.3 tray mode).</summary>
    private readonly Func<string, Task> _onCommit;

    /// <summary>Ctrl+Enter — always copies, never inserts (SPEC §2: "do not insert").</summary>
    private readonly Func<string, Task> _onCopy;

    public PopupViewModel(EmojiSearchEngine engine, Func<string, Task> onCommit, Func<string, Task> onCopy)
    {
        _engine = engine;
        _onCommit = onCommit;
        _onCopy = onCopy;
    }

    public ObservableCollection<ResultRowViewModel> Results { get; } = [];

    /// <summary>
    /// Live settings (SPEC §6.2's "Search" languages and preferred skin tone): the composition root updates this
    /// in place when the user saves changes in the Settings window, so the next search or reopen picks them up
    /// without recreating the popup (CLAUDE.md: never re-create it).
    /// </summary>
    public AppSettings Settings { get; set; } = AppSettings.Default;

    /// <summary>Swaps the search engine in place after Settings rebuilds the filtered catalog.</summary>
    public void UpdateEngine(EmojiSearchEngine engine) => _engine = engine;

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    public partial int SelectedIndex { get; set; }

    /// <summary>True only when the query is non-empty and nothing cleared the minimum score (SPEC §2 "No results").</summary>
    [ObservableProperty]
    public partial bool HasNoResults { get; set; }

    public Visibility ResultsVisibility => HasNoResults ? Visibility.Collapsed : Visibility.Visible;

    public Visibility NoResultsVisibility => HasNoResults ? Visibility.Visible : Visibility.Collapsed;

    partial void OnQueryChanged(string value) => RunSearch();

    partial void OnHasNoResultsChanged(bool value)
    {
        OnPropertyChanged(nameof(ResultsVisibility));
        OnPropertyChanged(nameof(NoResultsVisibility));
    }

    /// <summary>Call once, right after the popup becomes visible, to load the empty-query state (SPEC §2).</summary>
    public void Reset()
    {
        if (Query.Length == 0)
        {
            // Setting Query to its own value would not raise OnQueryChanged, so search directly.
            RunSearch();
        }
        else
        {
            Query = "";
        }
    }

    /// <summary>Up/Down, Shift+Tab/Tab: moves selection, wraps (SPEC §2).</summary>
    public void MoveSelection(int delta)
    {
        if (Results.Count == 0)
        {
            return;
        }

        SetSelectedIndex(((SelectedIndex + delta) % Results.Count + Results.Count) % Results.Count);
    }

    /// <summary>Enter: commits the selected row, or row 1 if nothing was moved.</summary>
    public Task CommitSelectedAsync() => CommitAsync(SelectedIndex, _onCommit);

    /// <summary>Alt+1 / Alt+2 / Alt+3 or a row click: commits that row directly, regardless of the current selection.</summary>
    public Task CommitRowAsync(int index) => CommitAsync(index, _onCommit);

    /// <summary>Ctrl+Enter: copies the selected row without inserting, regardless of the current selection.</summary>
    public Task CopySelectedAsync() => CommitAsync(SelectedIndex, _onCopy);

    private Task CommitAsync(int index, Func<string, Task> action)
    {
        if (index < 0 || index >= Results.Count)
        {
            return Task.CompletedTask;
        }

        var emoji = Results[index].Emoji;
        _engine.RecordUse(Query, emoji);
        return action(emoji);
    }

    private void RunSearch()
    {
        var results = _engine.Search(Query, new SearchOptions(Settings.Languages));
        Results.Clear();
        for (var i = 0; i < results.Count; i++)
        {
            var displayEmoji = SkinToneApplier.Apply(results[i].Entry, Settings.PreferredSkinTone);
            Results.Add(new ResultRowViewModel(results[i], position: i + 1, total: results.Count, displayEmoji));
        }

        HasNoResults = Query.Trim().Length > 0 && Results.Count == 0;
        SetSelectedIndex(0); // "Row 1 is always selected when results change" (SPEC §2).
    }

    private void SetSelectedIndex(int index)
    {
        if (SelectedIndex >= 0 && SelectedIndex < Results.Count)
        {
            Results[SelectedIndex].IsSelected = false;
        }

        SelectedIndex = index;
        if (index >= 0 && index < Results.Count)
        {
            Results[index].IsSelected = true;
        }
    }
}
