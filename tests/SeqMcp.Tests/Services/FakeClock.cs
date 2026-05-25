using SeqMcp.Core.Services;

namespace SeqMcp.Tests.Services;

/// <summary>
/// Controllable clock for cache-eviction tests. <see cref="Advance"/> moves
/// time forward without sleeping; <see cref="UtcNow"/> returns the current
/// fake time.
/// </summary>
internal sealed class FakeClock : IClock
{
    private long _ticks;

    public FakeClock(DateTime startUtc)
    {
        _ticks = startUtc.Ticks;
    }

    public DateTime UtcNow => new(Interlocked.Read(ref _ticks), DateTimeKind.Utc);

    public void Advance(TimeSpan delta)
    {
        Interlocked.Add(ref _ticks, delta.Ticks);
    }
}
