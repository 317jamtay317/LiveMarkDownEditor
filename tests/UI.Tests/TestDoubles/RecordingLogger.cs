using Microsoft.Extensions.Logging;

namespace UI.Tests.TestDoubles;

/// <summary>
/// A test double for <see cref="ILogger{TCategoryName}"/> that records every entry it is asked
/// to log so tests can assert on logging behaviour without a real logging pipeline.
/// </summary>
/// <typeparam name="T">The category the logger is created for.</typeparam>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<RecordedLogEntry> _entries = new();

    /// <summary>The entries this logger has recorded, in the order they were logged.</summary>
    public IReadOnlyList<RecordedLogEntry> Entries => _entries;

    /// <inheritdoc />
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _entries.Add(new RecordedLogEntry(logLevel, exception, formatter(state, exception)));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

/// <summary>A single entry captured by a <see cref="RecordingLogger{T}"/>.</summary>
/// <param name="Level">The level the entry was logged at.</param>
/// <param name="Exception">The exception attached to the entry, if any.</param>
/// <param name="Message">The formatted message text.</param>
public sealed record RecordedLogEntry(LogLevel Level, Exception? Exception, string Message);
