using System.Globalization;

namespace EuroGlobe.Contracts.Jobs;

/// <summary>When a recurring job runs. All times are UTC.</summary>
public sealed class JobSchedule
{
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(1);

    private JobSchedule(JobScheduleKind kind, TimeSpan interval, TimeOnly timeOfDayUtc)
    {
        Kind = kind;
        Interval = interval;
        TimeOfDayUtc = timeOfDayUtc;
    }

    public JobScheduleKind Kind { get; }

    public TimeSpan Interval { get; }

    public TimeOnly TimeOfDayUtc { get; }

    /// <summary>
    /// Runs aligned to fixed UTC boundaries, independent of when the app started:
    /// every 15 minutes means :00/:15/:30/:45.
    /// </summary>
    public static JobSchedule Every(TimeSpan interval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(interval, MinimumInterval);
        return new JobSchedule(JobScheduleKind.Interval, interval, default);
    }

    public static JobSchedule DailyAt(TimeOnly timeOfDayUtc) =>
        new(JobScheduleKind.Daily, TimeSpan.Zero, timeOfDayUtc);

    /// <summary>The first occurrence strictly after <paramref name="after"/>; passing an exact run time returns the next one.</summary>
    public DateTimeOffset GetNextOccurrence(DateTimeOffset after)
    {
        var afterUtc = after.UtcDateTime;

        if (Kind == JobScheduleKind.Interval)
        {
            var intervalTicks = Interval.Ticks;
            var nextTicks = (afterUtc.Ticks / intervalTicks + 1) * intervalTicks;
            return new DateTimeOffset(nextTicks, TimeSpan.Zero);
        }

        var candidate = afterUtc.Date + TimeOfDayUtc.ToTimeSpan();
        if (candidate <= afterUtc)
        {
            candidate = candidate.AddDays(1);
        }

        return new DateTimeOffset(candidate, TimeSpan.Zero);
    }

    public override string ToString() => Kind == JobScheduleKind.Interval
        ? $"every {Interval.ToString("c", CultureInfo.InvariantCulture)}"
        : $"daily at {TimeOfDayUtc.ToString("HH:mm", CultureInfo.InvariantCulture)} UTC";
}

public enum JobScheduleKind
{
    Interval,
    Daily,
}
