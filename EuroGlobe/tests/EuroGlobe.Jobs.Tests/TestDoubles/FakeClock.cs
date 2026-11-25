using EuroGlobe.Contracts.Time;

namespace EuroGlobe.Jobs.Tests.TestDoubles;

internal sealed class FakeClock(DateTimeOffset utcNow) : IClock
{
    private readonly Lock _lock = new();
    private DateTimeOffset _utcNow = utcNow;

    // Locked because scheduler tests set it while the scheduler's background loop reads it.
    public DateTimeOffset UtcNow
    {
        get { lock (_lock) { return _utcNow; } }
        set { lock (_lock) { _utcNow = value; } }
    }
}
