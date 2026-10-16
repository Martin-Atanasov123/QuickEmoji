using QuickEmoji.Core.IO;

namespace QuickEmoji.Core.Settings;

/// <summary>
/// Writes a file via a temp file plus <see cref="IFileSystem.Replace"/> (SPEC §7), so a crash or power loss
/// mid-write never leaves a half-written <c>settings.json</c> or <c>usage.json</c> behind. Falls back to a plain
/// move when the destination does not exist yet (<c>File.Replace</c> requires an existing destination).
/// </summary>
public static class AtomicFileWriter
{
    public static void Write(IFileSystem fileSystem, string path, string contents)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(contents);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            fileSystem.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";
        fileSystem.WriteAllText(tempPath, contents);

        if (fileSystem.FileExists(path))
        {
            fileSystem.Replace(tempPath, path);
        }
        else
        {
            fileSystem.Move(tempPath, path, overwrite: true);
        }
    }
}
