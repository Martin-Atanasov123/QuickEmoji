namespace QuickEmoji.Platform.Security;

/// <summary>Windows mandatory integrity levels, ordered low to high.</summary>
public enum IntegrityLevel
{
    Untrusted,
    Low,
    Medium,
    High,
    System,
}
