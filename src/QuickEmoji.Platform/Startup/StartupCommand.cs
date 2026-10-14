namespace QuickEmoji.Platform.Startup;

/// <summary>
/// Builds and recognises the HKCU Run value that launches QuickEmoji at sign-in (SPEC §7).
/// </summary>
public static class StartupCommand
{
    public const string BackgroundArgument = "--background";

    public static string Build(string executablePath)
    {
        ValidateExecutablePath(executablePath);
        return $"\"{executablePath}\" {BackgroundArgument}";
    }

    /// <summary>
    /// True when <paramref name="runValue"/> launches <paramref name="executablePath"/>,
    /// whether or not the path is quoted. Paths compare case-insensitively.
    /// </summary>
    public static bool PointsTo(string? runValue, string executablePath)
    {
        ValidateExecutablePath(executablePath);
        var target = ExtractExecutable(runValue);
        return target is not null
            && string.Equals(Path.GetFullPath(target), Path.GetFullPath(executablePath), StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractExecutable(string? runValue)
    {
        if (string.IsNullOrWhiteSpace(runValue))
        {
            return null;
        }

        var value = runValue.Trim();
        if (value[0] == '"')
        {
            var closing = value.IndexOf('"', 1);
            return closing > 1 ? value[1..closing] : null;
        }

        var exeEnd = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exeEnd > 0 ? value[..(exeEnd + ".exe".Length)] : null;
    }

    private static void ValidateExecutablePath(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!Path.IsPathFullyQualified(executablePath) || executablePath.Contains('"', StringComparison.Ordinal))
        {
            throw new ArgumentException("The executable path must be absolute and must not contain quotes.", nameof(executablePath));
        }
    }
}
