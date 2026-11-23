using EuroGlobe.Contracts.Time;

namespace EuroGlobe.Contracts.Jobs;

public sealed class JobContext
{
    private int _itemsProcessed;
    private int _itemsFailed;

    public JobContext(Guid runId, string jobName, DateTimeOffset scheduledFor, JobTrigger trigger, IClock clock)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(runId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        ArgumentNullException.ThrowIfNull(clock);

        RunId = runId;
        JobName = jobName;
        ScheduledFor = scheduledFor;
        Trigger = trigger;
        Clock = clock;
    }

    public Guid RunId { get; }

    public string JobName { get; }

    /// <summary>The due time this run is for; earlier than <c>Clock.UtcNow</c> when a missed run is caught up.</summary>
    public DateTimeOffset ScheduledFor { get; }

    public JobTrigger Trigger { get; }

    public IClock Clock { get; }

    public int ItemsProcessed => Volatile.Read(ref _itemsProcessed);

    public int ItemsFailed => Volatile.Read(ref _itemsFailed);

    public void ReportProcessed(int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        Interlocked.Add(ref _itemsProcessed, count);
    }

    public void ReportFailed(int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        Interlocked.Add(ref _itemsFailed, count);
    }
}
