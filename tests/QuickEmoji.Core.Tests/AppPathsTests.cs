namespace QuickEmoji.Core.Tests;

public sealed class AppPathsTests
{
    [Fact]
    public void Files_live_under_the_QuickEmoji_folder()
    {
        var paths = new AppPaths(@"C:\Users\test\AppData\Local");

        Assert.Equal(@"C:\Users\test\AppData\Local\QuickEmoji", paths.Root);
        Assert.Equal(@"C:\Users\test\AppData\Local\QuickEmoji\settings.json", paths.SettingsFile);
        Assert.Equal(@"C:\Users\test\AppData\Local\QuickEmoji\usage.json", paths.UsageFile);
        Assert.Equal(@"C:\Users\test\AppData\Local\QuickEmoji\logs", paths.LogDirectory);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"relative\path")]
    public void Rejects_a_root_that_is_not_an_absolute_path(string root)
    {
        Assert.ThrowsAny<ArgumentException>(() => new AppPaths(root));
    }

    [Fact]
    public void ForCurrentUser_uses_local_app_data()
    {
        var expectedRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);

        Assert.Equal(Path.Combine(expectedRoot, AppPaths.FolderName), AppPaths.ForCurrentUser().Root);
    }
}
