using EuroGlobe.Contracts.Time;

namespace EuroGlobe.Investments.Tests.TestDoubles;

internal sealed class FakeClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}