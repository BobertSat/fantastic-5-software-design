using EuroGlobe.Contracts.Jobs;

namespace EuroGlobe.Jobs.Runs;

public enum JobRunStatus
{
    Running,
    Succeeded,
    Failed,
    Skipped,
}

/// <summary>One run of a job, as kept in the run history.</summary>
public sealed class JobRunRecord
{
    public required Guid RunId { get; init; }

    public required string JobName { get; init; }

    public required JobTrigger Trigger { get; init; }

    public required DateTimeOffset ScheduledFor { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public required JobRunStatus Status { get; init; }

    public string? Error { get; init; }

    public int ItemsProcessed { get; init; }

    public int ItemsFailed { get; init; }
}
