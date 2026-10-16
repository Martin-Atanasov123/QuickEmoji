using QuickEmoji.Core.IO;

namespace QuickEmoji.Core.Tests.IO;

/// <summary>An in-memory <see cref="IFileSystem"/>, so settings/usage persistence tests never touch real disk.</summary>
public sealed class FakeFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Throws this on the next call that matches <see cref="FailOn"/>, once, then clears itself.</summary>
    public Exception? FailNextWriteWith { get; set; }

    public bool FileExists(string path) => _files.ContainsKey(path);

    public string ReadAllText(string path) =>
        _files.TryGetValue(path, out var contents) ? contents : throw new FileNotFoundException(path);

    public void WriteAllText(string path, string contents)
    {
        if (FailNextWriteWith is { } ex)
        {
            FailNextWriteWith = null;
            throw ex;
        }

        _files[path] = contents;
    }

    public void CreateDirectory(string path) => _directories.Add(path);

    public void Move(string sourcePath, string destinationPath, bool overwrite)
    {
        if (!overwrite && _files.ContainsKey(destinationPath))
        {
            throw new IOException("Destination already exists.");
        }

        _files[destinationPath] = _files[sourcePath];
        _files.Remove(sourcePath);
    }

    public void Replace(string sourcePath, string destinationPath)
    {
        if (!_files.ContainsKey(destinationPath))
        {
            throw new FileNotFoundException("Replace requires an existing destination.", destinationPath);
        }

        _files[destinationPath] = _files[sourcePath];
        _files.Remove(sourcePath);
    }

    public string? Get(string path) => _files.GetValueOrDefault(path);

    public void Set(string path, string contents) => _files[path] = contents;
}
