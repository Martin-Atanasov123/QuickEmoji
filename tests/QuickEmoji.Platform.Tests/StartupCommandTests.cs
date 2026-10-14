using QuickEmoji.Platform.Startup;

namespace QuickEmoji.Platform.Tests;

public sealed class StartupCommandTests
{
    private const string Exe = @"C:\Users\test\AppData\Local\Programs\QuickEmoji\QuickEmoji.exe";

    [Fact]
    public void Build_quotes_the_path_and_adds_the_background_flag()
    {
        Assert.Equal($"\"{Exe}\" --background", StartupCommand.Build(Exe));
    }

    [Fact]
    public void Build_quotes_paths_that_contain_spaces()
    {
        const string exe = @"C:\Program Files\Quick Emoji\QuickEmoji.exe";

        Assert.Equal("\"C:\\Program Files\\Quick Emoji\\QuickEmoji.exe\" --background", StartupCommand.Build(exe));
    }

    [Theory]
    [InlineData("")]
    [InlineData(@"QuickEmoji.exe")]
    [InlineData("C:\\a\"b\\QuickEmoji.exe")]
    public void Build_rejects_relative_or_quoted_paths(string exe)
    {
        Assert.ThrowsAny<ArgumentException>(() => StartupCommand.Build(exe));
    }

    [Fact]
    public void PointsTo_recognises_the_value_it_built()
    {
        Assert.True(StartupCommand.PointsTo(StartupCommand.Build(Exe), Exe));
    }

    [Theory]
    [InlineData("\"C:\\USERS\\TEST\\AppData\\Local\\Programs\\QuickEmoji\\QUICKEMOJI.EXE\" --background")]
    [InlineData(@"C:\Users\test\AppData\Local\Programs\QuickEmoji\QuickEmoji.exe --background")]
    [InlineData("  \"C:\\Users\\test\\AppData\\Local\\Programs\\QuickEmoji\\QuickEmoji.exe\"  ")]
    public void PointsTo_ignores_case_quotes_and_arguments(string runValue)
    {
        Assert.True(StartupCommand.PointsTo(runValue, Exe));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\"C:\\Other\\QuickEmoji.exe\" --background")]
    [InlineData("\"unterminated")]
    [InlineData("no executable here")]
    public void PointsTo_rejects_other_or_malformed_values(string? runValue)
    {
        Assert.False(StartupCommand.PointsTo(runValue, Exe));
    }
}
