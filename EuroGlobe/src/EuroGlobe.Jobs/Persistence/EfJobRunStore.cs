using EuroGlobe.Contracts.Jobs;
using EuroGlobe.Jobs.Runs;
using Microsoft.EntityFrameworkCore;

namespace EuroGlobe.Jobs.Persistence;

/// <summary>Run history in PostgreSQL, so it survives restarts and lets the scheduler catch up after downtime.</summary>
internal sealed class EfJobRunStore(IDbContextFactory<JobsDbContext> contextFactory) : IJobRunStore
{
    public async Task SaveAsync(JobRunRecord run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var row = await db.JobRuns.FindAsync([run.RunId], cancellationToken);
        if (row is null)
        {
            row = new JobRunRow { Id = run.RunId };
            db.JobRuns.Add(row);
        }

        row.JobName = run.JobName;
        row.Trigger = run.Trigger;
        row.ScheduledFor = run.ScheduledFor.ToUniversalTime();
        row.StartedAt = run.StartedAt.ToUniversalTime();
        row.FinishedAt = run.FinishedAt?.ToUniversalTime();
        row.Status = run.Status;
        row.Error = run.Error;
        row.ItemsProcessed = run.ItemsProcessed;
        row.ItemsFailed = run.ItemsFailed;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobRunRecord>> GetRecentAsync(string? jobName, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.JobRuns.AsNoTracking();
        if (jobName is not null)
        {
            query = query.Where(r => r.JobName == jobName);
        }

        var rows = await query.OrderByDescending(r => r.StartedAt).Take(limit).ToListAsync(cancellationToken);
        return rows.ConvertAll(ToRecord);
    }

    public async Task<DateTimeOffset?> GetLastScheduledForAsync(string jobName, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await db.JobRuns
            .Where(r => r.JobName == jobName && r.Trigger == JobTrigger.Scheduled)
            .MaxAsync(r => (DateTimeOffset?)r.ScheduledFor, cancellationToken);
    }

    private static JobRunRecord ToRecord(JobRunRow row) => new()
    {
        RunId = row.Id,
        JobName = row.JobName,
        Trigger = row.Trigger,
        ScheduledFor = row.ScheduledFor,
        StartedAt = row.StartedAt,
        FinishedAt = row.FinishedAt,
        Status = row.Status,
        Error = row.Error,
        ItemsProcessed = row.ItemsProcessed,
        ItemsFailed = row.ItemsFailed,
    };
}
