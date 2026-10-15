using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using QuickEmoji.Core.Search;

namespace QuickEmoji.App.Popup;

/// <summary>
/// One result row (SPEC §6.1). <see cref="IsBestMatch"/> is fixed per search (row 0); <see cref="IsSelected"/>
/// moves with the keymap. Visibility/brush are computed here rather than via XAML converters: a WinUI3 `Window`
/// root is not a <c>FrameworkElement</c>, and its <c>x:Bind</c> codegen cannot resolve <c>StaticResource</c>
/// converters from one (see docs/DECISIONS.md).
/// </summary>
public sealed partial class ResultRowViewModel : ObservableObject
{
    private static readonly SolidColorBrush Transparent = new(Colors.Transparent);

    public ResultRowViewModel(SearchResult result, int position, int total)
    {
        Emoji = result.Entry.Emoji;
        Name = ToTitleCase(result.Entry.Name);
        MatchExplanation = result.ExplainsMatch ? $"matched: {result.MatchedTerm}" : null;
        MatchExplanationVisibility = MatchExplanation is null ? Visibility.Collapsed : Visibility.Visible;
        IsBestMatch = position == 1;
        BestMatchVisibility = IsBestMatch ? Visibility.Visible : Visibility.Collapsed;

        // SPEC §6.5: Narrator reads e.g. "Red heart, best match, 1 of 3".
        AccessibleName = IsBestMatch
            ? $"{Name}, best match, {position} of {total}"
            : $"{Name}, {position} of {total}";
    }

    public string Emoji { get; }

    public string Name { get; }

    /// <summary>Null when the match came from the name itself; otherwise "matched: <c>term</c>" (SPEC §6.1).</summary>
    public string? MatchExplanation { get; }

    public Visibility MatchExplanationVisibility { get; }

    public bool IsBestMatch { get; }

    public Visibility BestMatchVisibility { get; }

    public string AccessibleName { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>The ↵ glyph next to the selected row (SPEC §6.1).</summary>
    public Visibility SelectedGlyphVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>The selected row's background tint, in the system accent (SPEC §6.1).</summary>
    public Brush RowBackground => IsSelected ? AccentTint() : Transparent;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(SelectedGlyphVisibility));
        OnPropertyChanged(nameof(RowBackground));
    }

    private static Brush AccentTint()
    {
        var accent = (SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        return new SolidColorBrush(accent.Color) { Opacity = 0.16 };
    }

    private static string ToTitleCase(string name) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);
}
