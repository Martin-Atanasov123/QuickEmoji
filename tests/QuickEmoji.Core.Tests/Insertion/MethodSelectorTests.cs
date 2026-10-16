using QuickEmoji.Core.Insertion;

namespace QuickEmoji.Core.Tests.Insertion;

public sealed class MethodSelectorTests
{
    [Fact]
    public void An_elevated_target_always_gets_Copy_even_with_a_matching_override()
    {
        var overrides = new[] { new AppOverride("notepad", InsertionMethod.ClipboardPaste) };

        var selection = MethodSelector.Select("notepad", isElevatedOrUnknown: true, overrides);

        Assert.Equal(InsertionMethod.Copy, selection.Method);
    }

    [Fact]
    public void An_unknown_elevation_state_is_treated_the_same_as_elevated()
    {
        // isElevatedOrUnknown is already the merged signal (TargetContext.IsElevatedOrUnknown); this just
        // confirms the selector itself does not special-case "unknown" differently from "known elevated".
        var selection = MethodSelector.Select("notepad", isElevatedOrUnknown: true, []);

        Assert.Equal(InsertionMethod.Copy, selection.Method);
        Assert.Contains("elevated", selection.Reason);
    }

    [Fact]
    public void No_override_and_not_elevated_defaults_to_unicode_injection()
    {
        var selection = MethodSelector.Select("notepad", isElevatedOrUnknown: false, []);

        Assert.Equal(InsertionMethod.UnicodeInjection, selection.Method);
        Assert.Equal(PasteKeys.CtrlV, selection.PasteKeys);
    }

    [Fact]
    public void A_null_process_name_with_no_overrides_still_defaults_to_unicode_injection()
    {
        var selection = MethodSelector.Select(null, isElevatedOrUnknown: false, []);

        Assert.Equal(InsertionMethod.UnicodeInjection, selection.Method);
    }

    [Fact]
    public void A_matching_override_wins_over_the_default()
    {
        var overrides = new[] { new AppOverride("mstsc", InsertionMethod.ClipboardPaste, PasteKeys.ShiftInsert) };

        var selection = MethodSelector.Select("mstsc", isElevatedOrUnknown: false, overrides);

        Assert.Equal(InsertionMethod.ClipboardPaste, selection.Method);
        Assert.Equal(PasteKeys.ShiftInsert, selection.PasteKeys);
        Assert.Contains("mstsc", selection.Reason);
    }

    [Fact]
    public void An_override_can_also_ask_for_Copy()
    {
        var overrides = new[] { new AppOverride("securedesktop", InsertionMethod.Copy) };

        var selection = MethodSelector.Select("securedesktop", isElevatedOrUnknown: false, overrides);

        Assert.Equal(InsertionMethod.Copy, selection.Method);
    }

    [Fact]
    public void Override_matching_is_case_insensitive()
    {
        var overrides = new[] { new AppOverride("Notepad", InsertionMethod.ClipboardPaste) };

        var selection = MethodSelector.Select("NOTEPAD", isElevatedOrUnknown: false, overrides);

        Assert.Equal(InsertionMethod.ClipboardPaste, selection.Method);
    }

    [Fact]
    public void A_process_name_not_in_the_overrides_falls_back_to_unicode_injection()
    {
        var overrides = new[] { new AppOverride("notepad", InsertionMethod.ClipboardPaste) };

        var selection = MethodSelector.Select("wordpad", isElevatedOrUnknown: false, overrides);

        Assert.Equal(InsertionMethod.UnicodeInjection, selection.Method);
    }

    [Fact]
    public void A_null_overrides_list_throws()
    {
        Assert.Throws<ArgumentNullException>(() => MethodSelector.Select("notepad", false, null!));
    }

    [Fact]
    public void The_default_method_setting_replaces_unicode_injection_when_nothing_else_applies()
    {
        var selection = MethodSelector.Select("wordpad", isElevatedOrUnknown: false, [], InsertionMethod.ClipboardPaste);

        Assert.Equal(InsertionMethod.ClipboardPaste, selection.Method);
        Assert.Equal("default", selection.Reason);
    }

    [Fact]
    public void The_default_method_setting_never_overrides_the_elevation_safety_check()
    {
        var selection = MethodSelector.Select("notepad", isElevatedOrUnknown: true, [], InsertionMethod.ClipboardPaste);

        Assert.Equal(InsertionMethod.Copy, selection.Method);
    }

    [Fact]
    public void A_matching_override_still_wins_over_the_default_method_setting()
    {
        var overrides = new[] { new AppOverride("mstsc", InsertionMethod.UnicodeInjection) };

        var selection = MethodSelector.Select("mstsc", isElevatedOrUnknown: false, overrides, InsertionMethod.ClipboardPaste);

        Assert.Equal(InsertionMethod.UnicodeInjection, selection.Method);
    }
}
