using QuickEmoji.Platform.Security;

namespace QuickEmoji.Platform.Tests.Security;

[Trait("Category", "Win32")]
public sealed class IntegrityLevelReaderTests
{
    [Fact]
    public void Reads_the_current_processs_own_integrity_level()
    {
        var level = IntegrityLevelReader.TryRead(Environment.ProcessId);

        // CI and interactive dev machines run the test host unelevated; this fails loudly if that ever changes
        // so a silently-elevated test run doesn't mask a real detection bug.
        Assert.Equal(IntegrityLevel.Medium, level);
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
