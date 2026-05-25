namespace SeqMcp.Core.Services;

/// <summary>
/// Minimal abstraction over the system clock so cache-eviction tests can
/// fast-forward time without sleeping. <see cref="SystemClock"/> is the
/// real one; tests provide a <c>FakeClock</c> stand-in.
/// </summary>
internal interface IClock
{
    DateTime UtcNow { get; }
}

internal sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();
    private SystemClock() { }
    public DateTime UtcNow => DateTime.UtcNow;
}
