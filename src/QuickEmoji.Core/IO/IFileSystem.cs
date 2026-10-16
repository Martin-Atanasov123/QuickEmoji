namespace QuickEmoji.Core.IO;

/// <summary>
/// The narrow slice of file-system access Core's persistence needs (CLAUDE.md: "file system access goes through
/// an abstraction"), so <c>SettingsStore</c>/<c>UsageStore</c> can be unit-tested without touching real disk.
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);

    string ReadAllText(string path);

    void WriteAllText(string path, string contents);

    void CreateDirectory(string path);

    /// <summary>Renames/moves a file, overwriting the destination if it already exists.</summary>
    void Move(string sourcePath, string destinationPath, bool overwrite);

    /// <summary>
    /// Atomically replaces <paramref name="destinationPath"/>'s content with <paramref name="sourcePath"/>'s
    /// (SPEC §7: "written atomically — temp file, then File.Replace"). The destination must already exist.
    /// </summary>
    void Replace(string sourcePath, string destinationPath);
}
