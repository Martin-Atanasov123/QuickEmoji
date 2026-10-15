using QuickEmoji.Platform.Hotkeys;

namespace QuickEmoji.Platform.Tests.Hotkeys;

public sealed class HotkeyGestureTests
{
    [Fact]
    public void ToString_lists_modifiers_in_a_fixed_order_then_the_key()
    {
        var gesture = new HotkeyGesture(HotkeyModifiers.Shift | HotkeyModifiers.Win | HotkeyModifiers.Alt | HotkeyModifiers.Ctrl, VirtualKeyCodes.E);

        Assert.Equal("Win+Ctrl+Alt+Shift+E", gesture.ToString());
    }

    [Fact]
    public void ToString_omits_modifiers_that_are_not_set()
    {
        var gesture = new HotkeyGesture(HotkeyModifiers.Win | HotkeyModifiers.Shift, VirtualKeyCodes.E);

        Assert.Equal("Win+Shift+E", gesture.ToString());
    }

    [Theory]
    [InlineData(0x31, "1")]
    [InlineData(0x39, "9")]
    [InlineData(0x41, "A")]
    [InlineData(0x5A, "Z")]
    [InlineData(0x70, "F1")]
    [InlineData(0x87, "F24")]
    public void VirtualKeyCodes_names_letters_digits_and_function_keys(uint vk, string expected)
    {
        Assert.Equal(expected, VirtualKeyCodes.Name(vk));
    }

    [Fact]
    public void Two_gestures_with_the_same_values_are_equal()
    {
        var a = new HotkeyGesture(HotkeyModifiers.Win | HotkeyModifiers.Shift, VirtualKeyCodes.E);
        var b = new HotkeyGesture(HotkeyModifiers.Win | HotkeyModifiers.Shift, VirtualKeyCodes.E);

        Assert.Equal(a, b);
    }
}
