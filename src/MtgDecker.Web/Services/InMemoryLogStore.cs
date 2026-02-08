using System.Collections.Concurrent;

namespace MtgDecker.Web.Services;

public class LogEntry
{
    public DateTime Timestamp { get; init; }
    public LogLevel Level { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? Exception { get; init; }
}

public class InMemoryLogStore
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private const int MaxEntries = 1000;

    public event Action? OnNewEntry;

    public void Add(LogEntry entry)
    {
        _entries.Enqueue(entry);
        while (_entries.Count > MaxEntries)
            _entries.TryDequeue(out _);

        var handler = OnNewEntry;
        if (handler != null)
        {
            try { handler.Invoke(); }
            catch (Exception) { /* Subscriber errors should not break logging */ }
        }
    }

    public IReadOnlyList<LogEntry> GetEntries() => _entries.ToArray();

    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }
}

public class InMemoryLogProvider : ILoggerProvider
{
    private readonly InMemoryLogStore _store;
    private readonly TimeProvider _timeProvider;

    public InMemoryLogProvider(InMemoryLogStore store, TimeProvider timeProvider)
    {
        _store = store;
        _timeProvider = timeProvider;
    }

    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(_store, _timeProvider, categoryName);

    public void Dispose() { }

    private class InMemoryLogger : ILogger
    {
        private readonly InMemoryLogStore _store;
        private readonly TimeProvider _timeProvider;
        private readonly string _category;

        public InMemoryLogger(InMemoryLogStore store, TimeProvider timeProvider, string category)
        {
            _store = store;
            _timeProvider = timeProvider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            _store.Add(new LogEntry
            {
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                Level = logLevel,
                Category = _category,
                Message = formatter(state, exception),
                Exception = exception?.ToString()
            });
        }
    }
}
