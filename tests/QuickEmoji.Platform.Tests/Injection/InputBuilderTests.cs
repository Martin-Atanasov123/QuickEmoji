using QuickEmoji.Platform.Injection;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace QuickEmoji.Platform.Tests.Injection;

/// <summary>
/// SPEC §4.2 step 5 names five sequences the INPUT[] builder must be tested against: 👨‍👩‍👧‍👦 (ZWJ family),
/// 👍🏽 (skin tone), 🏳️‍🌈 (VS16 + ZWJ), 🏴󠁧󠁢󠁳󠁣󠁴󠁿 (tag sequence, all supplementary-plane), 1️⃣ (keycap).
/// </summary>
public sealed class InputBuilderTests
{
    public static TheoryData<string, string> NamedSequences => new()
    {
        { "👨‍👩‍👧‍👦", "family: man, woman, girl, boy (ZWJ)" },
        { "👍🏽", "thumbs up: medium skin tone" },
        { "🏳️‍🌈", "rainbow flag (VS16 + ZWJ)" },
        { "🏴󠁧󠁢󠁳󠁣󠁴󠁿", "flag: Scotland (tag sequence)" },
        { "1️⃣", "keycap: 1" },
    };

    [Theory]
    [MemberData(nameof(NamedSequences))]
    public void Every_UTF16_code_unit_becomes_a_down_and_an_up_event_in_order(string emoji, string name)
    {
        var events = InputBuilder.BuildUnicodeText(emoji);
        Assert.NotEmpty(name);

        Assert.Equal(emoji.Length * 2, events.Length);
        for (var i = 0; i < emoji.Length; i++)
        {
            var down = events[i * 2];
            var up = events[(i * 2) + 1];

            Assert.Equal(emoji[i], (char)down.Anonymous.ki.wScan);
            Assert.Equal(emoji[i], (char)up.Anonymous.ki.wScan);
            Assert.Equal(KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE, down.Anonymous.ki.dwFlags);
            Assert.Equal(KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP, up.Anonymous.ki.dwFlags);
        }
    }

    [Theory]
    [MemberData(nameof(NamedSequences))]
    public void Every_event_is_type_keyboard_with_a_zero_virtual_key_code(string emoji, string name)
    {
        var events = InputBuilder.BuildUnicodeText(emoji);
        Assert.NotEmpty(name);

        Assert.All(events, e =>
        {
            Assert.Equal(INPUT_TYPE.INPUT_KEYBOARD, e.type);
            Assert.Equal((VIRTUAL_KEY)0, e.Anonymous.ki.wVk); // wVk must be 0 when KEYEVENTF_UNICODE is set
        });
    }

    [Fact]
    public void A_surrogate_pair_becomes_two_separate_events_each_carrying_one_half()
    {
        // 👍🏽 = U+1F44D U+1F3FD, each a surrogate pair: 4 UTF-16 code units, 8 events.
        var events = InputBuilder.BuildUnicodeText("👍🏽");

        Assert.Equal(8, events.Length);
        Assert.Equal("👍🏽".ToCharArray(), events.Where((_, i) => i % 2 == 0).Select(e => (char)e.Anonymous.ki.wScan).ToArray());
    }

    [Fact]
    public void An_empty_string_builds_no_events()
    {
        Assert.Empty(InputBuilder.BuildUnicodeText(""));
    }

    [Fact]
    public void CtrlV_is_control_down_v_down_v_up_control_up()
    {
        var events = InputBuilder.BuildPasteShortcut(VIRTUAL_KEY.VK_CONTROL, VIRTUAL_KEY.VK_V);

        Assert.Equal(4, events.Length);
        AssertKeyEvent(events[0], VIRTUAL_KEY.VK_CONTROL, down: true);
        AssertKeyEvent(events[1], VIRTUAL_KEY.VK_V, down: true);
        AssertKeyEvent(events[2], VIRTUAL_KEY.VK_V, down: false);
        AssertKeyEvent(events[3], VIRTUAL_KEY.VK_CONTROL, down: false);
    }

    [Fact]
    public void ShiftInsert_is_shift_down_insert_down_insert_up_shift_up()
    {
        var events = InputBuilder.BuildPasteShortcut(VIRTUAL_KEY.VK_SHIFT, VIRTUAL_KEY.VK_INSERT);

        Assert.Equal(4, events.Length);
        AssertKeyEvent(events[0], VIRTUAL_KEY.VK_SHIFT, down: true);
        AssertKeyEvent(events[1], VIRTUAL_KEY.VK_INSERT, down: true);
        AssertKeyEvent(events[2], VIRTUAL_KEY.VK_INSERT, down: false);
        AssertKeyEvent(events[3], VIRTUAL_KEY.VK_SHIFT, down: false);
    }

    private static void AssertKeyEvent(Windows.Win32.UI.Input.KeyboardAndMouse.INPUT e, VIRTUAL_KEY key, bool down)
    {
        Assert.Equal(INPUT_TYPE.INPUT_KEYBOARD, e.type);
        Assert.Equal(key, e.Anonymous.ki.wVk);
        Assert.Equal(down ? default : KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP, e.Anonymous.ki.dwFlags);
    }
}
