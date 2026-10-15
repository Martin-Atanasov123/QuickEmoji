using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;

namespace QuickEmoji.Platform.Security;

/// <summary>
/// Reads a process's mandatory integrity level, to detect an elevated (administrator) target before attempting
/// to inject input into it (SPEC §4.1, §4.2): "SendInput into an elevated process from a non-elevated one fails
/// silently" (CLAUDE.md gotcha), so the check must happen first, not be inferred from a failed injection.
/// </summary>
public static unsafe class IntegrityLevelReader
{
    /// <summary>
    /// The target's integrity level, or null when it could not be read. A non-elevated caller is normally denied
    /// access to an elevated process's token, so null is itself evidence the target may be elevated — callers
    /// should treat null the same as <see cref="IntegrityLevel.High"/> and fall back to copy (SPEC §4.6).
    /// </summary>
    public static IntegrityLevel? TryRead(int processId)
    {
        using var process = PInvoke.OpenProcess_SafeHandle(
            PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)processId);
        if (process.IsInvalid)
        {
            return null;
        }

        if (!PInvoke.OpenProcessToken(process, TOKEN_ACCESS_MASK.TOKEN_QUERY, out var token) || token.IsInvalid)
        {
            return null;
        }

        using (token)
        {
            return TryReadFromToken(token);
        }
    }

    private static IntegrityLevel? TryReadFromToken(Microsoft.Win32.SafeHandles.SafeFileHandle token)
    {
        if (!PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, null, 0, out var requiredSize)
            && requiredSize == 0)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal((int)requiredSize);
        try
        {
            if (!PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, buffer.ToPointer(), requiredSize, out _))
            {
                return null;
            }

            var label = Marshal.PtrToStructure<TOKEN_MANDATORY_LABEL>(buffer);
            var sid = label.Label.Sid;
            var subAuthorityCount = *PInvoke.GetSidSubAuthorityCount(sid);
            if (subAuthorityCount == 0)
            {
                return null;
            }

            var rid = *PInvoke.GetSidSubAuthority(sid, (uint)(subAuthorityCount - 1));
            return FromRid(rid);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static IntegrityLevel? FromRid(uint rid) => rid switch
    {
        PInvoke.SECURITY_MANDATORY_UNTRUSTED_RID => IntegrityLevel.Untrusted,
        PInvoke.SECURITY_MANDATORY_LOW_RID => IntegrityLevel.Low,
        >= PInvoke.SECURITY_MANDATORY_MEDIUM_RID and < PInvoke.SECURITY_MANDATORY_HIGH_RID => IntegrityLevel.Medium,
        >= PInvoke.SECURITY_MANDATORY_HIGH_RID and < PInvoke.SECURITY_MANDATORY_SYSTEM_RID => IntegrityLevel.High,
        >= PInvoke.SECURITY_MANDATORY_SYSTEM_RID => IntegrityLevel.System,
        _ => null,
    };
}
