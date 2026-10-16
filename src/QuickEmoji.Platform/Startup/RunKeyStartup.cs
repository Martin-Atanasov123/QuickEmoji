using Microsoft.Win32;

namespace QuickEmoji.Platform.Startup;

/// <summary>
/// "Launch at startup" (SPEC §7): a value under HKCU's <c>...\CurrentVersion\Run</c> pointing at the exe with
/// <c>--background</c>. Uses <see cref="Registry"/> directly rather than CsWin32 — it is already a managed BCL
/// wrapper over the registry APIs, not raw Win32 interop, so CLAUDE.md's CsWin32 rule does not apply to it.
/// </summary>
public static class RunKeyStartup
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string DefaultValueName = "QuickEmoji";

    /// <summary>True when a startup value for <paramref name="valueName"/> currently exists.</summary>
    public static bool IsEnabled(string valueName = DefaultValueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(valueName) is string;
    }

    /// <summary>Creates or overwrites the startup value, pointing at <paramref name="executablePath"/> with <c>--background</c>.</summary>
    public static void Enable(string executablePath, string valueName = DefaultValueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(valueName, $"\"{executablePath}\" --background", RegistryValueKind.String);
    }

    /// <summary>Removes the startup value, if it exists. Safe to call when it does not (SPEC §7: "remove on uninstall").</summary>
    public static void Disable(string valueName = DefaultValueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
