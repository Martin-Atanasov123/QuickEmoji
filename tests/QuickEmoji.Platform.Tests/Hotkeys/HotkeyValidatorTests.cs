using QuickEmoji.Platform.Hotkeys;

namespace QuickEmoji.Platform.Tests.Hotkeys;

public sealed class HotkeyValidatorTests
{
    [Fact]
    public void A_combination_with_no_modifier_is_rejected()
    {
        var gesture = new HotkeyGesture(HotkeyModifiers.None, VirtualKeyCodes.E);

        Assert.Equal(HotkeyValidation.MissingModifier, HotkeyValidator.Validate(gesture));
    }

    [Theory]
    [InlineData(HotkeyModifiers.Win)]
    [InlineData(HotkeyModifiers.Ctrl)]
    [InlineData(HotkeyModifiers.Alt)]
    [InlineData(HotkeyModifiers.Win | HotkeyModifiers.Shift)]
    [InlineData(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift)]
    [InlineData(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Win)]
    public void A_combination_with_Ctrl_Alt_or_Win_is_valid_without_a_warning(HotkeyModifiers modifiers)
    {
        var gesture = new HotkeyGesture(modifiers, VirtualKeyCodes.E);

        Assert.Equal(HotkeyValidation.Valid, HotkeyValidator.Validate(gesture));
    }

    [Theory]
    [InlineData(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt)]
    [InlineData(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift)]
    public void Ctrl_Alt_without_Win_warns_about_AltGr(HotkeyModifiers modifiers)
    {
        var gesture = new HotkeyGesture(modifiers, VirtualKeyCodes.E);

        Assert.Equal(HotkeyValidation.ValidWithAltGrWarning, HotkeyValidator.Validate(gesture));
    }

    [Fact]
    public void Ctrl_Alt_with_Win_does_not_warn()
    {
        var gesture = new HotkeyGesture(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Win, VirtualKeyCodes.E);

        Assert.Equal(HotkeyValidation.Valid, HotkeyValidator.Validate(gesture));
    }

    [Fact]
    public void The_default_gesture_is_Win_Shift_E_and_is_valid()
    {
        Assert.Equal(HotkeyModifiers.Win | HotkeyModifiers.Shift, HotkeyGesture.Default.Modifiers);
        Assert.Equal(VirtualKeyCodes.E, HotkeyGesture.Default.VirtualKey);
        Assert.Equal(HotkeyValidation.Valid, HotkeyValidator.Validate(HotkeyGesture.Default));
    }
}
