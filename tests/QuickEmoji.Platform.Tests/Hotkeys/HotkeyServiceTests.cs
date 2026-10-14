using Windows.Win32;
using Windows.Win32.Foundation;
using QuickEmoji.Platform.Hotkeys;

namespace QuickEmoji.Platform.Tests.Hotkeys;

/// <summary>
/// Exercises the real message-only window and RegisterHotKey. Uses high F-keys with every modifier held, which
/// no real application is realistically bound to, to avoid colliding with the CI agent or the developer's own
/// shortcuts.
/// </summary>
[Trait("Category", "Win32")]
public sealed class HotkeyServiceTests
{
    private static readonly HotkeyModifiers AllModifiers = HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Win;

    [Fact]
    public void Registering_a_free_combination_succeeds()
    {
        using var service = new HotkeyService();

        var result = service.Register(new HotkeyGesture(AllModifiers, 0x78 /* F9 */));

        Assert.Equal(HotkeyRegistrationResult.Registered, result);
        Assert.Equal(new HotkeyGesture(AllModifiers, 0x78), service.Current);
    }

    [Fact]
    public void A_second_service_cannot_register_the_same_combination()
    {
        var gesture = new HotkeyGesture(AllModifiers, 0x79 /* F10 */);
        using var first = new HotkeyService();
        first.Register(gesture);

        using var second = new HotkeyService();
        var result = second.Register(gesture);

        Assert.Equal(HotkeyRegistrationResult.Conflict, result);
        Assert.Null(second.Current);
    }

    [Fact]
    public void TestRegister_reports_a_conflict_without_taking_over_the_combination()
    {
        var gesture = new HotkeyGesture(AllModifiers, 0x7A /* F11 */);
        using var owner = new HotkeyService();
        owner.Register(gesture);

        using var prober = new HotkeyService();
        var probed = prober.TestRegister(gesture);

        Assert.Equal(HotkeyRegistrationResult.Conflict, probed);
        // The owner must still hold it: a second TestRegister from elsewhere still conflicts.
        using var secondProber = new HotkeyService();
        Assert.Equal(HotkeyRegistrationResult.Conflict, secondProber.TestRegister(gesture));
    }

    [Fact]
    public void TestRegister_of_a_free_combination_succeeds_and_releases_it_again()
    {
        var gesture = new HotkeyGesture(AllModifiers, 0x7B /* F12 */);
        using var prober = new HotkeyService();

        Assert.Equal(HotkeyRegistrationResult.Registered, prober.TestRegister(gesture));

        // Released again: a real registration of the same combination now succeeds.
        using var owner = new HotkeyService();
        Assert.Equal(HotkeyRegistrationResult.Registered, owner.Register(gesture));
    }

    [Fact]
    public void Pausing_releases_the_combination_and_resuming_re_registers_it()
    {
        var gesture = new HotkeyGesture(AllModifiers, 0x7C /* F13 */);
        using var service = new HotkeyService();
        service.Register(gesture);

        service.Paused = true;
        using (var other = new HotkeyService())
        {
            Assert.Equal(HotkeyRegistrationResult.Registered, other.TestRegister(gesture));
        }

        service.Paused = false;
        using var prober = new HotkeyService();
        Assert.Equal(HotkeyRegistrationResult.Conflict, prober.TestRegister(gesture));
    }

    [Fact]
    public void Registering_again_replaces_the_previous_combination()
    {
        using var service = new HotkeyService();
        service.Register(new HotkeyGesture(AllModifiers, 0x7D /* F14 */));

        service.Register(new HotkeyGesture(AllModifiers, 0x7E /* F15 */));

        using var prober = new HotkeyService();
        Assert.Equal(HotkeyRegistrationResult.Registered, prober.TestRegister(new HotkeyGesture(AllModifiers, 0x7D)));
    }

    [Fact]
    public void Disposing_releases_the_combination()
    {
        var gesture = new HotkeyGesture(AllModifiers, 0x7F /* F16 */);
        var service = new HotkeyService();
        service.Register(gesture);

        service.Dispose();

        using var prober = new HotkeyService();
        Assert.Equal(HotkeyRegistrationResult.Registered, prober.TestRegister(gesture));
    }

    [Fact]
    public async Task WM_HOTKEY_raises_HotkeyPressed()
    {
        using var service = new HotkeyService();
        service.Register(new HotkeyGesture(AllModifiers, 0x80 /* F17 */));
        var fired = new TaskCompletionSource();
        service.HotkeyPressed += (_, _) => fired.TrySetResult();

        unsafe
        {
            PInvoke.PostMessage(new HWND((void*)service.WindowHandle), PInvoke.WM_HOTKEY, new WPARAM(1), default);
        }

        var completed = await Task.WhenAny(fired.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(fired.Task, completed);
    }

    [Fact]
    public void HotkeyPressed_does_not_fire_for_an_unrelated_hotkey_id()
    {
        using var service = new HotkeyService();
        service.Register(new HotkeyGesture(AllModifiers, 0x81 /* F18 */));
        var fired = false;
        service.HotkeyPressed += (_, _) => fired = true;

        unsafe
        {
            PInvoke.PostMessage(new HWND((void*)service.WindowHandle), PInvoke.WM_HOTKEY, new WPARAM(99), default);
        }

        Thread.Sleep(200);
        Assert.False(fired);
    }
}
