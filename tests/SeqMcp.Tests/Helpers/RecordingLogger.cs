using Microsoft.Extensions.Logging;

namespace SeqMcp.Tests.Helpers;

public sealed record LogRecord(LogLevel Level, string Message, Exception? Exception);

public sealed class RecordingLogger<T> : ILogger<T>
{
    public List<LogRecord> Records { get; } = new();

    IDisposable? ILogger.BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Records.Add(new LogRecord(logLevel, formatter(state, exception), exception));
    }
}
