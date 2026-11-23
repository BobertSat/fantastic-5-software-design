using EuroGlobe.Contracts.Jobs;
using EuroGlobe.Jobs.Running;
using EuroGlobe.Jobs.Runs;
using EuroGlobe.Jobs.Scheduling;
using EuroGlobe.Jobs.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

namespace EuroGlobe.Jobs.Tests.Scheduling;

public sealed class JobSchedulerHostedServiceTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    // Start = 2026-11-23T10:00Z; DailyJob runs at 02:00Z; MinuteJob every minute.
    private static readonly DateTimeOffset NextDaily = new(2026, 11, 24, 2, 0, 0, TimeSpan.Zero);

    private static JobsTestContext Create(Action<IServiceCollection>? configure = null) =>
        JobsTestContext.Create(s =>
        {
            s.AddRecurringJob<DailyJob>();
            configure?.Invoke(s);
        });

    private static async Task<JobSchedulerHostedService> InitializedAsync(JobsTestContext ctx)
    {
        var scheduler = ctx.CreateScheduler();
        await scheduler.InitializeAsync(Ct);
        return scheduler;
    }

    private static async Task<int> TickAsync(JobSchedulerHostedService scheduler)
    {
        var started = scheduler.StartDueJobs(Ct);
        await Task.WhenAll(started);
        return started.Count;
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Condition not met within 10 s.");
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task InitializeAsync_NoHistory_SchedulesFirstOccurrenceAfterNow()
    {
        using var ctx = Create();
        var scheduler = ctx.CreateScheduler();

        await scheduler.InitializeAsync(Ct);

        Assert.Equal(NextDaily, scheduler.NextDue[DailyJob.JobName]);
    }

    [Fact]
    public async Task StartDueJobs_NotDue_StartsNothing()
    {
        using var ctx = Create();
        var scheduler = await InitializedAsync(ctx);
        ctx.Clock.UtcNow = NextDaily.AddSeconds(-1);

        Assert.Equal(0, await TickAsync(scheduler));
        Assert.Empty(ctx.Probe.Runs);
    }

    [Fact]
    public async Task StartDueJobs_Due_RunsOnceThenSchedulesNextOccurrence()
    {
        using var ctx = Create();
        var scheduler = await InitializedAsync(ctx);
        ctx.Clock.UtcNow = NextDaily.AddSeconds(5);

        Assert.Equal(1, await TickAsync(scheduler));
        Assert.Equal(0, await TickAsync(scheduler));

        var run = Assert.Single(await ctx.RunsAsync());
        Assert.Equal(JobRunStatus.Succeeded, run.Status);
        Assert.Equal(JobTrigger.Scheduled, run.Trigger);
        Assert.Equal(NextDaily, run.ScheduledFor);
        Assert.Equal(NextDaily.AddDays(1), scheduler.NextDue[DailyJob.JobName]);
    }

    [Fact]
    public async Task StartDueJobs_OnlyDueJobsStart()
    {
        using var ctx = Create(s => s.AddRecurringJob<MinuteJob>());
        var scheduler = await InitializedAsync(ctx);
        ctx.Clock.UtcNow = JobsTestContext.Start.AddMinutes(1);

        Assert.Equal(1, await TickAsync(scheduler));

        Assert.Equal(1, ctx.Probe.Count(MinuteJob.JobName));
        Assert.Equal(0, ctx.Probe.Count(DailyJob.JobName));
    }

    [Fact]
    public async Task StartDueJobs_ClockJumpedSeveralDays_CatchesUpWithOneRunForLatestOccurrence()
    {
        using var ctx = Create();
        var scheduler = await InitializedAsync(ctx);
        ctx.Clock.UtcNow = JobsTestContext.Start.AddDays(3);

        Assert.Equal(1, await TickAsync(scheduler));
        Assert.Equal(0, await TickAsync(scheduler));

        Assert.Equal(NextDaily.AddDays(2), Assert.Single(await ctx.RunsAsync()).ScheduledFor);
        Assert.Equal(NextDaily.AddDays(3), scheduler.NextDue[DailyJob.JobName]);
    }

    [Fact]
    public async Task InitializeAsync_HistoryShowsMissedOccurrences_CatchesUpOnFirstTick()
    {
        using var ctx = Create();
        await ctx.Store.SaveAsync(new JobRunRecord
        {
            RunId = Guid.NewGuid(),
            JobName = DailyJob.JobName,
            Trigger = JobTrigger.Scheduled,
            ScheduledFor = NextDaily.AddDays(-4),
            StartedAt = NextDaily.AddDays(-4),
            Status = JobRunStatus.Succeeded,
        }, Ct);
        var scheduler = await InitializedAsync(ctx);

        Assert.Equal(1, await TickAsync(scheduler));

        var latest = (await ctx.RunsAsync(DailyJob.JobName))[0];
        Assert.Equal(NextDaily.AddDays(-1), latest.ScheduledFor);
    }

    [Fact]
    public async Task StartDueJobs_ClockMovedBack_ReschedulesFromNewNow()
    {
        using var ctx = Create();
        var scheduler = await InitializedAsync(ctx);
        ctx.Clock.UtcNow = JobsTestContext.Start.AddDays(90);
        await TickAsync(scheduler);

        ctx.Clock.UtcNow = JobsTestContext.Start;
        Assert.Equal(0, await TickAsync(scheduler));
        Assert.Equal(NextDaily, scheduler.NextDue[DailyJob.JobName]);

        ctx.Clock.UtcNow = NextDaily;
        Assert.Equal(1, await TickAsync(scheduler));
        Assert.Equal(2, ctx.Probe.Count(DailyJob.JobName));
    }

    [Fact]
    public async Task StartDueJobs_JobFails_RecordedAsFailedAndNextOccurrenceStillRuns()
    {
        using var ctx = Create();
        var scheduler = await InitializedAsync(ctx);
        ctx.Probe.Behaviors[DailyJob.JobName] = (_, _) => throw new InvalidOperationException("db down");
        ctx.Clock.UtcNow = NextDaily;
        await TickAsync(scheduler);

        ctx.Probe.Behaviors.Clear();
        ctx.Clock.UtcNow = NextDaily.AddDays(1);
        await TickAsync(scheduler);

        var runs = await ctx.RunsAsync();
        Assert.Equal([JobRunStatus.Succeeded, JobRunStatus.Failed], runs.Select(r => r.Status));
        Assert.Equal("InvalidOperationException: db down", runs[1].Error);
    }

    [Fact]
    public async Task StartDueJobs_PreviousRunStillRunning_RecordsSkipped()
    {
        using var ctx = JobsTestContext.Create(s => s.AddRecurringJob<MinuteJob>());
        var scheduler = await InitializedAsync(ctx);
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        ctx.Probe.Behaviors[MinuteJob.JobName] = async (_, _) =>
        {
            started.TrySetResult();
            await release.Task;
        };

        ctx.Clock.UtcNow = JobsTestContext.Start.AddMinutes(1);
        var slowRun = scheduler.StartDueJobs(Ct);
        await started.Task;
        ctx.Clock.UtcNow = JobsTestContext.Start.AddMinutes(2);
        await TickAsync(scheduler);
        release.SetResult();
        await Task.WhenAll(slowRun);

        var statuses = (await ctx.RunsAsync()).Select(r => r.Status).ToList();
        Assert.Contains(JobRunStatus.Skipped, statuses);
        Assert.Contains(JobRunStatus.Succeeded, statuses);
        Assert.Equal(1, ctx.Probe.Count(MinuteJob.JobName));
    }

    [Fact]
    public async Task StartDueJobs_RunStoreThrows_SchedulerKeepsGoing()
    {
        using var ctx = Create(s => s.AddSingleton<IJobRunStore, ThrowingRunStore>());
        var scheduler = await InitializedAsync(ctx);
        ctx.Clock.UtcNow = NextDaily;

        var started = scheduler.StartDueJobs(Ct);
        await Task.WhenAll(started);

        _ = Assert.Single(started);
        Assert.Empty(ctx.Probe.Runs);
        Assert.Equal(NextDaily.AddDays(1), scheduler.NextDue[DailyJob.JobName]);
    }

    [Fact]
    public async Task StartAsync_InvalidJobRegistration_FailsStartup()
    {
        using var ctx = JobsTestContext.Create(s => s.AddRecurringJob<ConfigurableJob>());
        ctx.Provider.GetRequiredService<ConfigurableJobSettings>().Name = "Not Kebab";

        await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.CreateScheduler().StartAsync(Ct));
    }

    [Fact]
    public async Task HostedService_Running_RunsDueJobOnScheduleAndCancelsInFlightRunsOnStop()
    {
        using var ctx = JobsTestContext.Create(s => s.AddRecurringJob<MinuteJob>());
        var firstRunDone = new TaskCompletionSource();
        var secondRunStarted = new TaskCompletionSource();
        ctx.Probe.Behaviors[MinuteJob.JobName] = async (context, ct) =>
        {
            if (firstRunDone.Task.IsCompleted)
            {
                secondRunStarted.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
            }

            firstRunDone.TrySetResult();
        };
        var scheduler = ctx.CreateScheduler(TimeSpan.FromMilliseconds(20));

        await scheduler.StartAsync(Ct);
        ctx.Clock.UtcNow = JobsTestContext.Start.AddMinutes(1);
        await firstRunDone.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntilAsync(async () => (await ctx.RunsAsync()).Any(r => r.Status == JobRunStatus.Succeeded));
        ctx.Clock.UtcNow = JobsTestContext.Start.AddMinutes(2);
        await secondRunStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await scheduler.StopAsync(Ct);

        var runs = await ctx.RunsAsync();
        Assert.Equal(2, runs.Count);
        Assert.Equal(JobRunStatus.Failed, runs[0].Status);
        Assert.Equal(JobRunner.CancelledMessage, runs[0].Error);
        Assert.Equal(JobRunStatus.Succeeded, runs[1].Status);
    }

    private sealed class ThrowingRunStore : IJobRunStore
    {
        public Task SaveAsync(JobRunRecord run, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("store unavailable");

        public Task<IReadOnlyList<JobRunRecord>> GetRecentAsync(string? jobName, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<JobRunRecord>>([]);

        public Task<DateTimeOffset?> GetLastScheduledForAsync(string jobName, CancellationToken cancellationToken) =>
            Task.FromResult<DateTimeOffset?>(null);
    }
}
