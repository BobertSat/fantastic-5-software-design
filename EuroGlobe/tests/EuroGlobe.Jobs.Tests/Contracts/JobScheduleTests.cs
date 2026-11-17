using EuroGlobe.Contracts.Jobs;

namespace EuroGlobe.Jobs.Tests.Contracts;

public sealed class JobScheduleTests
{
    private static DateTimeOffset Utc(string iso) => DateTimeOffset.Parse(iso + "Z", System.Globalization.CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(0)]
    [InlineData(-1_000)]
    [InlineData(999)]
    public void Every_IntervalBelowOneSecond_Throws(int milliseconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => JobSchedule.Every(TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Fact]
    public void Every_ExactlyOneSecond_IsAllowed()
    {
        var schedule = JobSchedule.Every(TimeSpan.FromSeconds(1));

        Assert.Equal(JobScheduleKind.Interval, schedule.Kind);
        Assert.Equal(TimeSpan.FromSeconds(1), schedule.Interval);
        Assert.Equal(default, schedule.TimeOfDayUtc);
    }

    [Theory]
    [InlineData(60, "2026-11-17T10:00:30", "2026-11-17T10:01:00")]
    [InlineData(60, "2026-11-17T10:00:00", "2026-11-17T10:01:00")]
    [InlineData(900, "2026-11-17T10:07:00", "2026-11-17T10:15:00")]
    [InlineData(900, "2026-11-17T10:59:59", "2026-11-17T11:00:00")]
    [InlineData(3600, "2026-11-17T23:30:00", "2026-11-18T00:00:00")]
    [InlineData(3600, "2026-12-31T23:00:01", "2027-01-01T00:00:00")]
    [InlineData(86400, "2026-11-17T00:00:00", "2026-11-18T00:00:00")]
    public void Every_GetNextOccurrence_ReturnsNextAlignedBoundary(int seconds, string after, string expected)
    {
        var schedule = JobSchedule.Every(TimeSpan.FromSeconds(seconds));

        var next = schedule.GetNextOccurrence(Utc(after));

        Assert.Equal(Utc(expected), next);
        Assert.Equal(TimeSpan.Zero, next.Offset);
    }

    [Fact]
    public void Every_GetNextOccurrence_IsAlwaysStrictlyLater()
    {
        var schedule = JobSchedule.Every(TimeSpan.FromMinutes(1));
        var at = Utc("2026-11-17T10:00:00");

        var first = schedule.GetNextOccurrence(at);
        var second = schedule.GetNextOccurrence(first);

        Assert.True(first > at);
        Assert.Equal(TimeSpan.FromMinutes(1), second - first);
    }

    [Fact]
    public void Every_NonUtcInput_UsesTheSameInstantAndReturnsUtc()
    {
        var schedule = JobSchedule.Every(TimeSpan.FromHours(1));
        var vilnius = new DateTimeOffset(2026, 11, 17, 12, 30, 0, TimeSpan.FromHours(2)); // = 10:30Z

        var next = schedule.GetNextOccurrence(vilnius);

        Assert.Equal(Utc("2026-11-17T11:00:00"), next);
        Assert.Equal(TimeSpan.Zero, next.Offset);
    }

    [Fact]
    public void DailyAt_SetsKindAndTime()
    {
        var schedule = JobSchedule.DailyAt(new TimeOnly(2, 0));

        Assert.Equal(JobScheduleKind.Daily, schedule.Kind);
        Assert.Equal(new TimeOnly(2, 0), schedule.TimeOfDayUtc);
        Assert.Equal(TimeSpan.Zero, schedule.Interval);
    }

    [Theory]
    [InlineData("2026-11-17T01:59:59", "2026-11-17T02:00:00")]
    [InlineData("2026-11-17T02:00:00", "2026-11-18T02:00:00")]
    [InlineData("2026-11-17T02:00:01", "2026-11-18T02:00:00")]
    [InlineData("2026-11-30T23:00:00", "2026-12-01T02:00:00")]
    [InlineData("2026-12-31T23:59:59", "2027-01-01T02:00:00")]
    [InlineData("2028-02-28T03:00:00", "2028-02-29T02:00:00")]
    public void DailyAt_GetNextOccurrence_ReturnsNextRunTime(string after, string expected)
    {
        var schedule = JobSchedule.DailyAt(new TimeOnly(2, 0));

        var next = schedule.GetNextOccurrence(Utc(after));

        Assert.Equal(Utc(expected), next);
        Assert.Equal(TimeSpan.Zero, next.Offset);
    }

    [Fact]
    public void DailyAt_Midnight_RunsAtStartOfNextDay()
    {
        var schedule = JobSchedule.DailyAt(TimeOnly.MinValue);

        Assert.Equal(Utc("2026-11-18T00:00:00"), schedule.GetNextOccurrence(Utc("2026-11-17T00:00:00")));
    }

    [Fact]
    public void DailyAt_NonUtcInput_UsesTheUtcDate()
    {
        var schedule = JobSchedule.DailyAt(new TimeOnly(2, 0));
        // 01:30 in Vilnius (+03:00 in summer) is 22:30Z on the previous day, so 02:00Z today is still ahead.
        var vilnius = new DateTimeOffset(2026, 7, 17, 1, 30, 0, TimeSpan.FromHours(3));

        var next = schedule.GetNextOccurrence(vilnius);

        Assert.Equal(Utc("2026-07-17T02:00:00"), next);
        Assert.Equal(TimeSpan.Zero, next.Offset);
    }

    [Fact]
    public void ToString_Interval_DescribesInterval()
    {
        Assert.Equal("every 00:15:00", JobSchedule.Every(TimeSpan.FromMinutes(15)).ToString());
    }

    [Fact]
    public void ToString_Daily_DescribesUtcTime()
    {
        Assert.Equal("daily at 02:05 UTC", JobSchedule.DailyAt(new TimeOnly(2, 5)).ToString());
    }
}
