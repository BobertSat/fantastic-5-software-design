using EuroGlobe.Jobs.Tests.TestDoubles;
using EuroGlobe.Jobs.Time;

namespace EuroGlobe.Jobs.Tests.Time;

public sealed class ClockImplementationTests
{
    private static readonly DateTimeOffset RealNow = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SystemClock_UtcNow_IsCurrentUtcTime()
    {
        var before = DateTimeOffset.UtcNow;

        var now = new SystemClock().UtcNow;

        Assert.InRange(now, before, DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, now.Offset);
    }

    [Fact]
    public void SimulatedClock_NotAdvanced_FollowsRealClock()
    {
        var real = new FakeClock(RealNow);
        var clock = new SimulatedClock(real);

        Assert.Equal(RealNow, clock.UtcNow);
        real.UtcNow = RealNow.AddMinutes(5);
        Assert.Equal(RealNow.AddMinutes(5), clock.UtcNow);
        Assert.Equal(TimeSpan.Zero, clock.Offset);
    }

    [Fact]
    public void SimulatedClock_Advance_AccumulatesOffset()
    {
        var clock = new SimulatedClock(new FakeClock(RealNow));

        clock.Advance(TimeSpan.FromDays(30));
        clock.Advance(TimeSpan.FromHours(2));
        clock.Advance(TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromDays(30) + TimeSpan.FromHours(2), clock.Offset);
        Assert.Equal(RealNow.AddDays(30).AddHours(2), clock.UtcNow);
    }

    [Fact]
    public void SimulatedClock_AdvanceNegative_Throws()
    {
        var clock = new SimulatedClock(new FakeClock(RealNow));

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromSeconds(-1)));
        Assert.Equal(TimeSpan.Zero, clock.Offset);
    }

    [Fact]
    public void SimulatedClock_Reset_ReturnsToRealTime()
    {
        var clock = new SimulatedClock(new FakeClock(RealNow));
        clock.Advance(TimeSpan.FromDays(90));

        clock.Reset();

        Assert.Equal(TimeSpan.Zero, clock.Offset);
        Assert.Equal(RealNow, clock.UtcNow);
    }

    [Fact]
    public void SimulatedClock_NullRealClock_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SimulatedClock(null!));
    }
}
