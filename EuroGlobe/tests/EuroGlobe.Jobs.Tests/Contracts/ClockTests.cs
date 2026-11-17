using EuroGlobe.Contracts.Time;
using EuroGlobe.Jobs.Tests.TestDoubles;

namespace EuroGlobe.Jobs.Tests.Contracts;

public sealed class ClockTests
{
    [Fact]
    public void Today_UtcInstant_IsItsDate()
    {
        IClock clock = new FakeClock(new DateTimeOffset(2026, 11, 17, 23, 59, 59, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 11, 17), clock.Today);
    }

    [Fact]
    public void Today_NonUtcInstant_IsTheUtcDate()
    {
        // 23:30 at -02:00 is 01:30Z on the next day.
        IClock clock = new FakeClock(new DateTimeOffset(2026, 11, 17, 23, 30, 0, TimeSpan.FromHours(-2)));

        Assert.Equal(new DateOnly(2026, 11, 18), clock.Today);
    }
}
