using System.Security.Cryptography;

namespace QuickEmoji.DataBuilder.Sources;

/// <summary>Downloads pinned sources into a local cache and refuses any file whose SHA-256 does not match.</summary>
internal sealed class SourceCache(string directory, HttpClient http)
{
    public async Task<string> GetAsync(SourceFile source, bool offline, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, source.FileName);
        if (File.Exists(path) && HashOf(await File.ReadAllBytesAsync(path, cancellationToken)) == source.Sha256)
        {
            return path;
        }

        if (offline)
        {
            throw new InvalidOperationException($"{source.FileName} is not in the cache and --offline was given.");
        }

        Console.WriteLine($"Downloading {source.Url}");
        var bytes = await http.GetByteArrayAsync(source.Url, cancellationToken);
        var actual = HashOf(bytes);
        if (actual != source.Sha256)
        {
            throw new InvalidDataException(
                $"{source.Url} has SHA-256 {actual}, expected {source.Sha256}. The upstream file changed; review it and update SourceManifest.");
        }

        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return path;
    }

    internal static string HashOf(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
