using EuroGlobe.Contracts.Time;

namespace EuroGlobe.Jobs.Time;

/// <summary>
/// Real time plus an offset that the demo can move forward (ADR-J002). Development only;
/// the offset is kept in memory, so it resets when the process restarts.
/// </summary>
public sealed class SimulatedClock : IClock
{
    private readonly IClock _realClock;
    private long _offsetTicks;

    public SimulatedClock(IClock realClock)
    {
        ArgumentNullException.ThrowIfNull(realClock);
        _realClock = realClock;
    }

    public DateTimeOffset UtcNow => _realClock.UtcNow + Offset;

    public TimeSpan Offset => TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

    public void Advance(TimeSpan by)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(by, TimeSpan.Zero);
        Interlocked.Add(ref _offsetTicks, by.Ticks);
    }

    public void Reset() => Interlocked.Exchange(ref _offsetTicks, 0);
}
