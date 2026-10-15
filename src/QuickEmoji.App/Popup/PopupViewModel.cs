using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using QuickEmoji.Core.Search;

namespace QuickEmoji.App.Popup;

/// <summary>
/// Search state and the keymap (SPEC §2) for the popup. UI-agnostic beyond WinUI's <c>ObservableObject</c>, so
/// the window's code-behind only wires keyboard/mouse events to these methods.
/// </summary>
public sealed partial class PopupViewModel : ObservableObject
{
    private readonly EmojiSearchEngine _engine;

    /// <summary>Enter / Alt+1-3 / row click — Stage 3: copy to clipboard and show a toast (SPEC §4.6); Stage 4 replaces this with real insertion.</summary>
    private readonly Action<string> _onCommit;

    /// <summary>Ctrl+Enter — always copies, even after Stage 4 adds real insertion (SPEC §2: "do not insert").</summary>
    private readonly Action<string> _onCopy;

    public PopupViewModel(EmojiSearchEngine engine, Action<string> onCommit, Action<string> onCopy)
    {
        _engine = engine;
        _onCommit = onCommit;
        _onCopy = onCopy;
    }

    public ObservableCollection<ResultRowViewModel> Results { get; } = [];

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
    public void CommitSelected() => Commit(SelectedIndex, _onCommit);

    /// <summary>Alt+1 / Alt+2 / Alt+3 or a row click: commits that row directly, regardless of the current selection.</summary>
    public void CommitRow(int index) => Commit(index, _onCommit);

    /// <summary>Ctrl+Enter: copies the selected row without inserting, regardless of the current selection.</summary>
    public void CopySelected() => Commit(SelectedIndex, _onCopy);

    private void Commit(int index, Action<string> action)
    {
        if (index < 0 || index >= Results.Count)
        {
            return;
        }

        var emoji = Results[index].Emoji;
        _engine.RecordUse(Query, emoji);
        action(emoji);
    }

    private void RunSearch()
    {
        var results = _engine.Search(Query);
        Results.Clear();
        for (var i = 0; i < results.Count; i++)
        {
            Results.Add(new ResultRowViewModel(results[i], position: i + 1, total: results.Count));
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
