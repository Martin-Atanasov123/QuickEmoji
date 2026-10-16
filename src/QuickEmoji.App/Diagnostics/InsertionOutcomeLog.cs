namespace QuickEmoji.App.Diagnostics;

/// <summary>
/// The last 20 insertion outcomes (SPEC §6.2 "About → Copy diagnostics"), in memory only — never persisted, and
/// never the emoji or any text the user typed elsewhere (CLAUDE.md: "never log what the user typed in other
/// apps"), only the method/process/outcome shape already written to the log file.
/// </summary>
public sealed class InsertionOutcomeLog
{
    private const int Capacity = 20;
    private readonly Queue<string> _entries = new(Capacity);
    private readonly Lock _lock = new();

    public void Record(string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        lock (_lock)
        {
            _entries.Enqueue(outcome);
            while (_entries.Count > Capacity)
            {
                _entries.Dequeue();
            }
        }
    }

    public IReadOnlyList<string> Recent()
    {
        lock (_lock)
        {
            return _entries.ToArray();
        }
    }
}
