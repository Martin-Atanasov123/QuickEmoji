namespace QuickEmoji.App;

/// <summary>
/// Single-instance enforcement with a named mutex (SPEC §8). A second launch signals the first instance, then
/// the caller exits. <see cref="AnotherInstanceStarted"/> fires on a background thread; subscribers must marshal
/// to the UI thread themselves.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = "Local\\QuickEmoji.SingleInstance.Mutex";
    private const string SignalEventName = "Local\\QuickEmoji.SingleInstance.Signal";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _signalEvent;
    private readonly Thread? _waitThread;
    private volatile bool _disposed;

    public SingleInstanceGuard()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsFirstInstance = createdNew;
        _signalEvent = new EventWaitHandle(false, EventResetMode.AutoReset, SignalEventName);

        if (IsFirstInstance)
        {
            _waitThread = new Thread(WaitForSignal) { IsBackground = true, Name = "QuickEmoji.SingleInstanceWatcher" };
            _waitThread.Start();
        }
    }

    /// <summary>
    /// True when this process was the first to run and should act normally. False means another instance is
    /// already running: the caller should call <see cref="SignalExistingInstanceAndExit"/> and exit immediately.
    /// </summary>
    public bool IsFirstInstance { get; }

    /// <summary>
    /// Raised on the first instance, on a background thread, when a second launch signals it.
    /// SPEC §8 asks for Settings to open; until Stage 5 adds a Settings window, the subscriber shows the popup
    /// instead (see docs/DECISIONS.md).
    /// </summary>
    public event EventHandler? AnotherInstanceStarted;

    /// <summary>Call only when <see cref="IsFirstInstance"/> is false.</summary>
    public void SignalExistingInstanceAndExit()
    {
        _signalEvent.Set();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (IsFirstInstance)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _signalEvent.Dispose();
    }

    private void WaitForSignal()
    {
        while (!_disposed)
        {
            if (_signalEvent.WaitOne(TimeSpan.FromSeconds(1)))
            {
                AnotherInstanceStarted?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
