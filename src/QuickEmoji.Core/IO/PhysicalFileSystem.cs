namespace QuickEmoji.Core.IO;

/// <summary>The real file system, via <see cref="System.IO"/> (not Windows-specific, so it stays in Core).</summary>
public sealed class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void Move(string sourcePath, string destinationPath, bool overwrite) =>
        File.Move(sourcePath, destinationPath, overwrite);

    public void Replace(string sourcePath, string destinationPath) =>
        File.Replace(sourcePath, destinationPath, destinationBackupFileName: null);
}
