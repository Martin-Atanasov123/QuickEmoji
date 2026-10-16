using QuickEmoji.Core.Settings;
using QuickEmoji.Core.Tests.IO;

namespace QuickEmoji.Core.Tests.Settings;

public sealed class AtomicFileWriterTests
{
    private const string Path = @"C:\fake\QuickEmoji\settings.json";

    [Fact]
    public void Writing_for_the_first_time_creates_the_destination_directly()
    {
        var fs = new FakeFileSystem();

        AtomicFileWriter.Write(fs, Path, "hello");

        Assert.Equal("hello", fs.Get(Path));
        Assert.Null(fs.Get(Path + ".tmp"));
    }

    [Fact]
    public void Overwriting_an_existing_file_goes_through_a_temp_file_and_leaves_no_temp_file_behind()
    {
        var fs = new FakeFileSystem();
        fs.Set(Path, "old");

        AtomicFileWriter.Write(fs, Path, "new");

        Assert.Equal("new", fs.Get(Path));
        Assert.Null(fs.Get(Path + ".tmp"));
    }

    [Fact]
    public void A_write_failure_never_touches_the_existing_destination()
    {
        var fs = new FakeFileSystem();
        fs.Set(Path, "original");
        fs.FailNextWriteWith = new IOException("disk full");

        Assert.Throws<IOException>(() => AtomicFileWriter.Write(fs, Path, "new"));
        Assert.Equal("original", fs.Get(Path));
    }
}
