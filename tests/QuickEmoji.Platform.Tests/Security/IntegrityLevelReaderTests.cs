using System.Security.Principal;
using QuickEmoji.Platform.Security;

namespace QuickEmoji.Platform.Tests.Security;

[Trait("Category", "Win32")]
public sealed class IntegrityLevelReaderTests
{
    [Fact]
    public void Reads_the_current_processs_own_integrity_level_without_failing()
    {
        var level = IntegrityLevelReader.TryRead(Environment.ProcessId);

        // Not a fixed expected level: whether the test host itself runs elevated varies by machine (a normal
        // dev desktop is unelevated; a CI runner's default account commonly is). What must always hold is that
        // reading our own process succeeds at all.
        Assert.NotNull(level);
    }

    [Fact]
    public void Agrees_with_WindowsPrincipal_on_whether_the_current_process_is_elevated()
    {
        var level = IntegrityLevelReader.TryRead(Environment.ProcessId);
        var isAdministrator = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        Assert.NotNull(level);
        Assert.Equal(isAdministrator, level is IntegrityLevel.High or IntegrityLevel.System);
    }

    [Fact]
    public void Reading_the_same_process_twice_gives_the_same_level()
    {
        var first = IntegrityLevelReader.TryRead(Environment.ProcessId);
        var second = IntegrityLevelReader.TryRead(Environment.ProcessId);

        Assert.Equal(first, second);
    }

    [Fact]
    public void An_invalid_process_id_returns_null_rather_than_throwing()
    {
        var level = IntegrityLevelReader.TryRead(-1);

        Assert.Null(level);
    }

    [Fact]
    public void A_process_id_that_does_not_exist_returns_null()
    {
        // Pick a PID unlikely to be in use; if it happens to exist, the assertion still only requires "no throw".
        var level = IntegrityLevelReader.TryRead(999_999);

        Assert.Null(level);
    }
}
