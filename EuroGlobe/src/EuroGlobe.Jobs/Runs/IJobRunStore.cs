namespace EuroGlobe.Jobs.Runs;

/// <summary>Run history. In memory for now; a database implementation replaces it in Phase 3.</summary>
public interface IJobRunStore
{
    /// <summary>Inserts the run, or replaces the stored run with the same <see cref="JobRunRecord.RunId"/>.</summary>
    Task SaveAsync(JobRunRecord run, CancellationToken cancellationToken);

    /// <summary>The most recent runs first, optionally for one job only.</summary>
    Task<IReadOnlyList<JobRunRecord>> GetRecentAsync(string? jobName, int limit, CancellationToken cancellationToken);

    /// <summary>The latest occurrence the scheduler ran (or skipped) for the job; manual runs don't count.</summary>
    Task<DateTimeOffset?> GetLastScheduledForAsync(string jobName, CancellationToken cancellationToken);
}
