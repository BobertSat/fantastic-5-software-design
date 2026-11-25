using System.Collections.Concurrent;
using EuroGlobe.Contracts.Jobs;

namespace EuroGlobe.Jobs.Runs;

internal sealed class InMemoryJobRunStore : IJobRunStore
{
    internal const int Capacity = 1_000;

    private readonly ConcurrentDictionary<Guid, JobRunRecord> _runs = new();

    public Task SaveAsync(JobRunRecord run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        _runs[run.RunId] = run;

        if (_runs.Count > Capacity)
        {
            foreach (var oldest in _runs.Values.OrderBy(r => r.StartedAt).Take(_runs.Count - Capacity).ToList())
            {
                _runs.TryRemove(oldest.RunId, out _);
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<JobRunRecord>> GetRecentAsync(string? jobName, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        IReadOnlyList<JobRunRecord> runs = _runs.Values
            .Where(r => jobName is null || r.JobName == jobName)
            .OrderByDescending(r => r.StartedAt)
            .Take(limit)
            .ToList();

        return Task.FromResult(runs);
    }

    public Task<DateTimeOffset?> GetLastScheduledForAsync(string jobName, CancellationToken cancellationToken)
    {
        var last = _runs.Values
            .Where(r => r.JobName == jobName && r.Trigger == JobTrigger.Scheduled)
            .Select(r => (DateTimeOffset?)r.ScheduledFor)
            .Max();

        return Task.FromResult(last);
    }
}
