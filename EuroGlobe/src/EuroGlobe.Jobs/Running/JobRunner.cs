using System.Collections.Concurrent;
using EuroGlobe.Contracts.Jobs;
using EuroGlobe.Contracts.Time;
using EuroGlobe.Jobs.Registry;
using EuroGlobe.Jobs.Runs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EuroGlobe.Jobs.Running;

public interface IJobRunner
{
    /// <summary>Runs the job now, outside its schedule. Returns a Skipped run if it is already running.</summary>
    /// <exception cref="JobNotFoundException">No job has that name.</exception>
    Task<JobRunRecord> RunNowAsync(string jobName, CancellationToken cancellationToken);
}

public sealed class JobNotFoundException(string jobName) : Exception($"No job named '{jobName}' is registered.")
{
    public string JobName { get; } = jobName;
}

internal sealed class JobRunner(
    IJobRegistry registry,
    IJobRunStore store,
    IClock clock,
    IServiceScopeFactory scopeFactory,
    ILogger<JobRunner> logger) : IJobRunner
{
    internal const string SkippedMessage = "A previous run of this job is still in progress.";
    internal const string CancelledMessage = "Cancelled.";

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public Task<JobRunRecord> RunNowAsync(string jobName, CancellationToken cancellationToken)
    {
        var job = registry.Find(jobName) ?? throw new JobNotFoundException(jobName);
        return RunAsync(job, clock.UtcNow, JobTrigger.Manual, cancellationToken);
    }

    /// <summary>
    /// Runs the job in a new DI scope and records the result. Job exceptions never escape;
    /// run-store exceptions do.
    /// </summary>
    internal async Task<JobRunRecord> RunAsync(
        RegisteredJob job, DateTimeOffset scheduledFor, JobTrigger trigger, CancellationToken cancellationToken)
    {
        var gate = _locks.GetOrAdd(job.Name, _ => new SemaphoreSlim(1, 1));
        if (!gate.Wait(0))
        {
            var skipped = new JobRunRecord
            {
                RunId = Guid.NewGuid(),
                JobName = job.Name,
                Trigger = trigger,
                ScheduledFor = scheduledFor,
                StartedAt = clock.UtcNow,
                FinishedAt = clock.UtcNow,
                Status = JobRunStatus.Skipped,
                Error = SkippedMessage,
            };
            await store.SaveAsync(skipped, CancellationToken.None);
            logger.LogWarning("Job {JobName} skipped: {Reason}", job.Name, SkippedMessage);
            return skipped;
        }

        try
        {
            return await RunExclusiveAsync(job, scheduledFor, trigger, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<JobRunRecord> RunExclusiveAsync(
        RegisteredJob job, DateTimeOffset scheduledFor, JobTrigger trigger, CancellationToken cancellationToken)
    {
        var running = new JobRunRecord
        {
            RunId = Guid.NewGuid(),
            JobName = job.Name,
            Trigger = trigger,
            ScheduledFor = scheduledFor,
            StartedAt = clock.UtcNow,
            Status = JobRunStatus.Running,
        };
        await store.SaveAsync(running, CancellationToken.None);
        logger.LogInformation("Job {JobName} started ({Trigger}, run {RunId})", job.Name, trigger, running.RunId);

        var context = new JobContext(running.RunId, job.Name, scheduledFor, trigger, clock);
        var status = JobRunStatus.Succeeded;
        string? error = null;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var instance = (IRecurringJob)scope.ServiceProvider.GetRequiredService(job.JobType);
            await instance.ExecuteAsync(context, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            status = JobRunStatus.Failed;
            error = CancelledMessage;
            logger.LogWarning("Job {JobName} was cancelled (run {RunId})", job.Name, running.RunId);
        }
        catch (Exception ex)
        {
            status = JobRunStatus.Failed;
            error = $"{ex.GetType().Name}: {ex.Message}";
            logger.LogError(ex, "Job {JobName} failed (run {RunId})", job.Name, running.RunId);
        }

        var finished = new JobRunRecord
        {
            RunId = running.RunId,
            JobName = job.Name,
            Trigger = trigger,
            ScheduledFor = scheduledFor,
            StartedAt = running.StartedAt,
            FinishedAt = clock.UtcNow,
            Status = status,
            Error = error,
            ItemsProcessed = context.ItemsProcessed,
            ItemsFailed = context.ItemsFailed,
        };
        await store.SaveAsync(finished, CancellationToken.None);
        logger.LogInformation(
            "Job {JobName} finished: {Status}, {Processed} processed, {Failed} failed (run {RunId})",
            job.Name, status, finished.ItemsProcessed, finished.ItemsFailed, running.RunId);

        return finished;
    }
}
