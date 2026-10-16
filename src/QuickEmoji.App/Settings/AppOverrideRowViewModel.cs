using CommunityToolkit.Mvvm.ComponentModel;
using QuickEmoji.Core.Insertion;

namespace QuickEmoji.App.Settings;

/// <summary>One editable row of Settings → Insertion → "per-app overrides table" (SPEC §6.2).</summary>
public sealed partial class AppOverrideRowViewModel : ObservableObject
{
    public AppOverrideRowViewModel(AppOverride source)
    {
        ProcessName = source.ProcessName;
        Method = source.Method;
        PasteKeys = source.PasteKeys;
    }

    public AppOverrideRowViewModel()
        : this(new AppOverride("", InsertionMethod.ClipboardPaste))
    {
    }

    [ObservableProperty]
    public partial string ProcessName { get; set; }

    public InsertionMethod Method { get; private set; }

    public PasteKeys PasteKeys { get; private set; }

    /// <summary>
    /// String-typed proxy for the method ComboBox (SPEC §6.2's table): this app avoids XAML value converters
    /// (a WinUI 3 <c>Window</c> is not a <c>FrameworkElement</c> and its <c>x:Bind</c> codegen cannot resolve
    /// them — see docs/DECISIONS.md), so binding an enum to a ComboBox of plain strings goes through here instead.
    /// </summary>
    public string MethodText
    {
        get => Method.ToString();
        set
        {
            if (Enum.TryParse<InsertionMethod>(value, out var parsed))
            {
                Method = parsed;
            }
        }
    }

    public string PasteKeysText
    {
        get => PasteKeys.ToString();
        set
        {
            if (Enum.TryParse<PasteKeys>(value, out var parsed))
            {
                PasteKeys = parsed;
            }
        }
    }

    /// <summary>SPEC §6.2's table has no meaningful row for a blank process name.</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(ProcessName);

    public AppOverride ToOverride() => new(ProcessName.Trim(), Method, PasteKeys);
}
