using System.Collections.Concurrent;
using EuroGlobe.Contracts.Jobs;
using EuroGlobe.Contracts.Time;
using EuroGlobe.Jobs.Registry;
using EuroGlobe.Jobs.Running;
using EuroGlobe.Jobs.Runs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EuroGlobe.Jobs.Scheduling;

/// <summary>
/// Every tick, starts the jobs that are due according to <see cref="IClock"/> (which may be simulated).
/// The tick timer itself uses real time.
/// </summary>
/// <remarks>
/// Missed occurrences (downtime, or the simulated clock jumping forward) are caught up with a
/// <b>single</b> run for the latest missed occurrence. Jobs are idempotent "as of now", so one run is
/// enough. If the clock jumps backwards, the job is rescheduled from the new "now".
/// </remarks>
internal sealed class JobSchedulerHostedService : BackgroundService
{
    private readonly IJobRegistry _registry;
    private readonly JobRunner _runner;
    private readonly IJobRunStore _store;
    private readonly IClock _clock;
    private readonly ILogger<JobSchedulerHostedService> _logger;
    private readonly TimeSpan _tick;
    private readonly Dictionary<string, DateTimeOffset> _nextDue = new();
    private readonly ConcurrentDictionary<Task, byte> _inFlight = new();

    public JobSchedulerHostedService(
        IJobRegistry registry,
        JobRunner runner,
        IJobRunStore store,
        IClock clock,
        JobsOptions options,
        ILogger<JobSchedulerHostedService> logger)
        : this(registry, runner, store, clock, logger, TimeSpan.FromSeconds(options.TickSeconds))
    {
    }

    internal JobSchedulerHostedService(
        IJobRegistry registry,
        JobRunner runner,
        IJobRunStore store,
        IClock clock,
        ILogger<JobSchedulerHostedService> logger,
        TimeSpan tick)
    {
        _registry = registry;
        _runner = runner;
        _store = store;
        _clock = clock;
        _logger = logger;
        _tick = tick;
    }

    internal IReadOnlyDictionary<string, DateTimeOffset> NextDue => _nextDue;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Done before the host finishes starting, so invalid job registrations fail startup.
        await InitializeAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    internal async Task InitializeAsync(CancellationToken cancellationToken)
    {
        foreach (var job in _registry.Jobs)
        {
            var last = await _store.GetLastScheduledForAsync(job.Name, cancellationToken);
            var next = job.Schedule.GetNextOccurrence(last ?? _clock.UtcNow);
            _nextDue[job.Name] = next;
            _logger.LogInformation("Job {JobName} scheduled {Schedule}; next run {NextRun:O}", job.Name, job.Schedule, next);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_tick);
        try
        {
            do
            {
                foreach (var run in StartDueJobs(stoppingToken))
                {
                    _inFlight.TryAdd(run, 0);
                    _ = run.ContinueWith(t => _inFlight.TryRemove(t, out _), TaskScheduler.Default);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            await Task.WhenAll(_inFlight.Keys);
        }
    }

    /// <summary>Starts every due job without waiting for it; returns the started runs.</summary>
    internal IReadOnlyList<Task> StartDueJobs(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var started = new List<Task>();

        foreach (var job in _registry.Jobs)
        {
            var period = Period(job.Schedule);
            var next = _nextDue[job.Name];

            if (next - period > now)
            {
                next = job.Schedule.GetNextOccurrence(now);
                _nextDue[job.Name] = next;
                _logger.LogInformation("Clock moved back; job {JobName} rescheduled to {NextRun:O}", job.Name, next);
            }

            if (next > now)
            {
                continue;
            }

            // The latest occurrence at or before now: one run covers every missed occurrence.
            var scheduledFor = job.Schedule.GetNextOccurrence(now - period);
            _nextDue[job.Name] = job.Schedule.GetNextOccurrence(scheduledFor);
            if (scheduledFor > next)
            {
                _logger.LogInformation("Job {JobName} catching up: missed occurrences since {Missed:O}", job.Name, next);
            }

            started.Add(RunSafelyAsync(job, scheduledFor, cancellationToken));
        }

        return started;
    }

    private async Task RunSafelyAsync(RegisteredJob job, DateTimeOffset scheduledFor, CancellationToken cancellationToken)
    {
        try
        {
            await _runner.RunAsync(job, scheduledFor, JobTrigger.Scheduled, cancellationToken);
        }
        catch (Exception ex)
        {
            // Only infrastructure failures (e.g. the run store) get here; the scheduler keeps going.
            _logger.LogError(ex, "Could not run job {JobName}", job.Name);
        }
    }

    private static TimeSpan Period(JobSchedule schedule) =>
        schedule.Kind == JobScheduleKind.Interval ? schedule.Interval : TimeSpan.FromDays(1);
}
