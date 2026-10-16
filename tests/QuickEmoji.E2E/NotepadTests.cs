using System.Diagnostics;
using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Application = FlaUI.Core.Application;

namespace QuickEmoji.E2E;

/// <summary>
/// Starts one <c>QuickEmoji.exe</c> and one Notepad per test (xUnit creates a fresh <see cref="NotepadTests"/>
/// instance per [Fact] and runs <see cref="IAsyncLifetime"/> around each one — not once per class — and xUnit
/// runs this class's tests sequentially, so each test's QuickEmoji fully exits in <see cref="DisposeAsync"/>
/// before the next one starts; there is never two running at once to trigger SPEC §8's single-instance signal).
/// Every FlaUI wait below also takes an explicit, bounded timeout: FlaUI's own defaults are unbounded, and an
/// unbounded wait on a window that never appears hangs the whole run rather than failing it (see DECISIONS.md).
/// </summary>
[Trait("Category", "E2E")]
public sealed class NotepadTests : IAsyncLifetime
{
    private static readonly TimeSpan NotepadWindowTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PopupSettleTime = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan InsertionSettleTime = TimeSpan.FromMilliseconds(600);

    private Process? _quickEmoji;
    private Application? _notepad;
    private readonly UIA3Automation _automation = new();

    public async ValueTask InitializeAsync()
    {
        // Stage 5 added a first-run welcome window that pops up (and steals focus) whenever settings.json does
        // not exist yet. Without this, whether these tests see that window depends on whatever state the host
        // machine happens to be in (e.g. it appeared and corrupted every assertion below after a manual
        // --startup-check run on this machine had deleted its own settings.json) — ensure it is never first-run.
        EnsureSettingsFileExists();

        _quickEmoji = Process.Start(new ProcessStartInfo(AppUnderTest.LocateExecutable()) { UseShellExecute = false })
            ?? throw new InvalidOperationException("Failed to start QuickEmoji.exe.");
        // The hotkey is registered during OnLaunched, which the process needs a moment to reach.
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        // Windows 11's modern, tab-based Notepad is effectively single-instance: launching "notepad.exe" while
        // one is already running does not open a new window, it just adds a tab to the existing one (and the
        // Process object Process.Start hands back for that second launch is a stub that never owns a window).
        // Close any pre-existing Notepad first so the launch below is unambiguous.
        foreach (var stray in Process.GetProcessesByName("notepad"))
        {
            try
            {
                stray.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited.
            }
        }

        Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true });

        Process? windowOwner = null;
        var deadline = DateTime.UtcNow + NotepadWindowTimeout;
        while (DateTime.UtcNow < deadline)
        {
            foreach (var candidate in Process.GetProcessesByName("notepad"))
            {
                if (candidate.MainWindowHandle != IntPtr.Zero)
                {
                    windowOwner = candidate;
                    break;
                }
            }

            if (windowOwner is not null)
            {
                break;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        if (windowOwner is null)
        {
            throw new InvalidOperationException($"Notepad did not present a main window within {NotepadWindowTimeout.TotalSeconds} s.");
        }

        _notepad = Application.Attach(windowOwner);

        // This Notepad's UI (WinUI/XAML-based) populates its automation tree asynchronously after the window
        // handle first appears; GetNotepadEditor()'s own retry loop covers the rest, but giving it a head start
        // here avoids every test in the class eating that same settle time on its first automation call.
        await Task.Delay(500, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Hotkey_then_Enter_inserts_the_emoji_after_the_typed_text()
    {
        var editor = await ClearAndFocusEditorAsync();
        await TypeSlowlyAsync("I love");
        await Task.Delay(200, TestContext.Current.CancellationToken);
        var original = GetText(editor);

        await PressHotkeyAsync();
        await Task.Delay(PopupSettleTime, TestContext.Current.CancellationToken);
        await TypeSlowlyAsync("heart");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Keyboard.Press(VirtualKeyShort.ENTER);
        Keyboard.Release(VirtualKeyShort.ENTER);
        await Task.Delay(InsertionSettleTime, TestContext.Current.CancellationToken);

        var afterInsert = GetText(editor);
        Assert.Equal(original + "❤️", afterInsert);
    }

    [Fact]
    public async Task Esc_closes_the_popup_and_changes_nothing()
    {
        var editor = await ClearAndFocusEditorAsync();
        await TypeSlowlyAsync("unchanged text");
        await Task.Delay(200, TestContext.Current.CancellationToken);
        var original = GetText(editor);

        await PressHotkeyAsync();
        await Task.Delay(PopupSettleTime, TestContext.Current.CancellationToken);
        await TypeSlowlyAsync("fire");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Keyboard.Press(VirtualKeyShort.ESCAPE);
        Keyboard.Release(VirtualKeyShort.ESCAPE);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.Equal(original, GetText(editor));
    }

    [Fact]
    public async Task Inserting_an_emoji_does_not_change_the_clipboard()
    {
        const string marker = "QuickEmoji-E2E-clipboard-marker";
        ClipboardOracle.WriteText(marker);

        var editor = await ClearAndFocusEditorAsync();
        await TypeSlowlyAsync("clipboard test");
        await Task.Delay(200, TestContext.Current.CancellationToken);

        await PressHotkeyAsync();
        await Task.Delay(PopupSettleTime, TestContext.Current.CancellationToken);
        await TypeSlowlyAsync("star");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Keyboard.Press(VirtualKeyShort.ENTER);
        Keyboard.Release(VirtualKeyShort.ENTER);
        await Task.Delay(InsertionSettleTime, TestContext.Current.CancellationToken);

        // Unicode injection never touches the clipboard at all (SPEC §4.2 step 5); this is the oracle that proves it.
        Assert.Equal(marker, ClipboardOracle.ReadText());
    }

    /// <summary>
    /// Presses Win+Shift+E with each key held down in turn and a short gap before the final key, rather than
    /// FlaUI's <c>Keyboard.TypeSimultaneously</c> (which did not reliably trigger QuickEmoji's RegisterHotKey-based
    /// WM_HOTKEY — the modifiers likely weren't registered as down yet when E went down).
    /// </summary>
    private static async Task PressHotkeyAsync()
    {
        Keyboard.Press(VirtualKeyShort.LWIN);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Keyboard.Press(VirtualKeyShort.SHIFT);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Keyboard.Press(VirtualKeyShort.KEY_E);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Keyboard.Release(VirtualKeyShort.KEY_E);
        Keyboard.Release(VirtualKeyShort.SHIFT);
        Keyboard.Release(VirtualKeyShort.LWIN);
    }

    /// <summary>
    /// Types one character at a time with a short gap between each. FlaUI's own <c>Keyboard.Type(string)</c>
    /// sends keystrokes back-to-back, which this WinUI/XAML-based Notepad's input pipeline cannot keep up with:
    /// under load it drops and miscombines keystrokes (observed turning "love" into "eeee" — the last letter
    /// repeated to the original length), rather than simply lagging.
    /// </summary>
    private static async Task TypeSlowlyAsync(string text)
    {
        foreach (var c in text)
        {
            Keyboard.Type(c);
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>One shared Notepad window is reused across all three tests in this class; this clears it first so each test starts clean.</summary>
    private async Task<AutomationElement> ClearAndFocusEditorAsync()
    {
        var editor = await GetNotepadEditorAsync();
        editor.Focus();
        Keyboard.Press(VirtualKeyShort.CONTROL);
        Keyboard.Press(VirtualKeyShort.KEY_A);
        Keyboard.Release(VirtualKeyShort.KEY_A);
        Keyboard.Release(VirtualKeyShort.CONTROL);
        Keyboard.Press(VirtualKeyShort.DELETE);
        Keyboard.Release(VirtualKeyShort.DELETE);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        return editor;
    }

    /// <summary>
    /// Notepad's WinUI/XAML automation tree populates asynchronously after the main window handle appears
    /// (confirmed via a tree dump: the editor is a <c>ControlType.Document</c> named "Text editor", class
    /// <c>RichEditD2DPT</c>), so a single lookup right after attach can legitimately miss it — retry briefly.
    /// </summary>
    private async Task<AutomationElement> GetNotepadEditorAsync()
    {
        var window = _notepad!.GetMainWindow(_automation, TimeSpan.FromSeconds(5))
            ?? throw new InvalidOperationException("Notepad's main window did not appear within 5 s.");

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        AutomationElement? editor;
        do
        {
            editor = window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Document));
            if (editor is not null)
            {
                return editor;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        while (DateTime.UtcNow < deadline);

        throw new InvalidOperationException("Could not find Notepad's text editor in the automation tree within 5 s.");
    }

    private static string GetText(AutomationElement editor) => editor.Patterns.Value.Pattern.Value.ValueOrDefault ?? "";

    private static void EnsureSettingsFileExists()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickEmoji");
        var settingsFile = Path.Combine(directory, "settings.json");
        if (!File.Exists(settingsFile))
        {
            Directory.CreateDirectory(directory);
            // An empty object is enough: QuickEmojiApplication only checks the file's existence to decide
            // whether this is a first run, and SettingsStore falls back to defaults for every field anyway.
            File.WriteAllText(settingsFile, "{}");
        }
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            _notepad?.Close(killIfCloseFails: true);
        }
        catch
        {
            // Best effort; the process may already be gone.
        }

        _automation.Dispose();

        if (_quickEmoji is { HasExited: false })
        {
            _quickEmoji.Kill(entireProcessTree: true);
        }

        _quickEmoji?.Dispose();
        return ValueTask.CompletedTask;
    }
}
