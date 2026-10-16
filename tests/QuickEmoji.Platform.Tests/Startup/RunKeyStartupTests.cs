using QuickEmoji.Platform.Startup;

namespace QuickEmoji.Platform.Tests.Startup;

/// <summary>
/// Real HKCU registry access. Every test uses its own unique value name and disables it afterward, so a failed
/// run never leaves a stray startup entry behind (and never touches QuickEmoji's own real
/// <see cref="RunKeyStartup.DefaultValueName"/> value).
/// </summary>
[Trait("Category", "Win32")]
public sealed class RunKeyStartupTests : IDisposable
{
    private readonly string _valueName = $"QuickEmoji_Test_{Guid.NewGuid():N}";

    [Fact]
    public void A_value_that_was_never_enabled_reports_disabled()
    {
        Assert.False(RunKeyStartup.IsEnabled(_valueName));
    }

    [Fact]
    public void Enable_then_IsEnabled_reports_enabled()
    {
        RunKeyStartup.Enable(@"C:\fake\QuickEmoji.exe", _valueName);

        Assert.True(RunKeyStartup.IsEnabled(_valueName));
    }

    [Fact]
    public void Disable_after_enable_reports_disabled_again()
    {
        RunKeyStartup.Enable(@"C:\fake\QuickEmoji.exe", _valueName);
        RunKeyStartup.Disable(_valueName);

        Assert.False(RunKeyStartup.IsEnabled(_valueName));
    }

    [Fact]
    public void Disable_on_a_value_that_was_never_enabled_does_not_throw()
    {
        RunKeyStartup.Disable(_valueName);
    }

    [Fact]
    public void Enable_twice_overwrites_rather_than_duplicating_or_throwing()
    {
        RunKeyStartup.Enable(@"C:\fake\QuickEmoji.exe", _valueName);
        RunKeyStartup.Enable(@"C:\fake\QuickEmojiNew.exe", _valueName);

        Assert.True(RunKeyStartup.IsEnabled(_valueName));
    }

    [Fact]
    public void An_empty_executable_path_throws()
    {
        Assert.Throws<ArgumentException>(() => RunKeyStartup.Enable("", _valueName));
    }

    public void Dispose() => RunKeyStartup.Disable(_valueName);
}
