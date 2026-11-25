using EuroGlobe.Api.Controllers.Demo;
using EuroGlobe.Contracts.Time;
using EuroGlobe.Jobs;
using EuroGlobe.Jobs.Registry;
using EuroGlobe.Jobs.Running;
using EuroGlobe.Jobs.Runs;
using Microsoft.AspNetCore.Mvc;

namespace EuroGlobe.Api.Controllers.Jobs;

/// <summary>Demo tools: list the background jobs with their recent runs, and run a job now.</summary>
[ApiController]
[DemoAdminEndpoint]
[Route("api/admin/jobs")]
public sealed class AdminJobsController(
    IJobRegistry registry,
    IJobRunner runner,
    IJobRunStore store,
    IClock clock,
    JobsRuntime runtime) : ControllerBase
{
    internal const int RecentRunsPerJob = 10;

    [HttpGet]
    [ProducesResponseType<JobsOverviewResponse>(StatusCodes.Status200OK)]
    public async Task<JobsOverviewResponse> GetJobs(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var jobs = new List<JobResponse>();
        foreach (var job in registry.Jobs)
        {
            var runs = await store.GetRecentAsync(job.Name, RecentRunsPerJob, cancellationToken);
            jobs.Add(new JobResponse
            {
                Name = job.Name,
                Schedule = job.Schedule.ToString(),
                NextOccurrence = job.Schedule.GetNextOccurrence(now),
                RecentRuns = runs.Select(JobRunResponse.From).ToList(),
            });
        }

        return new JobsOverviewResponse
        {
            UtcNow = now,
            SchedulerEnabled = runtime.SchedulerEnabled,
            PersistentHistory = runtime.PersistentHistory,
            SimulatedClock = runtime.SimulatedClock,
            Jobs = jobs,
        };
    }

    /// <summary>Runs the job immediately (trigger <c>Manual</c>) and returns the finished run.</summary>
    [HttpPost("{name}/run")]
    [ProducesResponseType<JobRunResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RunNow(string name, CancellationToken cancellationToken)
    {
        JobRunRecord run;
        try
        {
            run = await runner.RunNowAsync(name, cancellationToken);
        }
        catch (JobNotFoundException ex)
        {
            return this.ProblemWithCode(StatusCodes.Status404NotFound, "JOB_NOT_FOUND", "Job not found", ex.Message);
        }

        if (run.Status == JobRunStatus.Skipped)
        {
            return this.ProblemWithCode(StatusCodes.Status409Conflict, "JOB_ALREADY_RUNNING", "Job is already running", run.Error);
        }

        return Ok(JobRunResponse.From(run));
    }
}

public sealed class JobsOverviewResponse
{
    public required DateTimeOffset UtcNow { get; init; }

    public required bool SchedulerEnabled { get; init; }

    public required bool PersistentHistory { get; init; }

    public required bool SimulatedClock { get; init; }

    public required IReadOnlyList<JobResponse> Jobs { get; init; }
}

public sealed class JobResponse
{
    public required string Name { get; init; }

    public required string Schedule { get; init; }

    /// <summary>The next occurrence after the current (possibly simulated) time.</summary>
    public required DateTimeOffset NextOccurrence { get; init; }

    public required IReadOnlyList<JobRunResponse> RecentRuns { get; init; }
}

public sealed class JobRunResponse
{
    public required Guid RunId { get; init; }

    public required string JobName { get; init; }

    public required string Trigger { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset ScheduledFor { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public string? Error { get; init; }

    public int ItemsProcessed { get; init; }

    public int ItemsFailed { get; init; }

    internal static JobRunResponse From(JobRunRecord run) => new()
    {
        RunId = run.RunId,
        JobName = run.JobName,
        Trigger = run.Trigger.ToString(),
        Status = run.Status.ToString(),
        ScheduledFor = run.ScheduledFor,
        StartedAt = run.StartedAt,
        FinishedAt = run.FinishedAt,
        Error = run.Error,
        ItemsProcessed = run.ItemsProcessed,
        ItemsFailed = run.ItemsFailed,
    };
}
