using EuroGlobe.Contracts.Jobs;
using EuroGlobe.Jobs.Locking;
using EuroGlobe.Jobs.Running;
using EuroGlobe.Jobs.Runs;
using EuroGlobe.Jobs.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

namespace EuroGlobe.Jobs.Tests.Running;

public sealed class JobRunnerTests
{
    private static JobsTestContext Create() => JobsTestContext.Create(s => s.AddRecurringJob<DailyJob>());

    [Fact]
    public async Task RunNowAsync_JobSucceeds_RecordsSucceededRunWithCounters()
    {
        using var ctx = Create();
        JobRunStatus? statusWhileRunning = null;
        ctx.Probe.Behaviors[DailyJob.JobName] = async (context, _) =>
        {
            statusWhileRunning = (await ctx.RunsAsync()).Single().Status;
            context.ReportProcessed(3);
            context.ReportFailed();
            ctx.Clock.UtcNow = ctx.Clock.UtcNow.AddSeconds(2);
        };

        var run = await ctx.Runner.RunNowAsync(DailyJob.JobName, CancellationToken.None);

        Assert.Equal(JobRunStatus.Running, statusWhileRunning);
        Assert.Equal(JobRunStatus.Succeeded, run.Status);
        Assert.Equal(JobTrigger.Manual, run.Trigger);
        Assert.Equal(JobsTestContext.Start, run.ScheduledFor);
        Assert.Equal(JobsTestContext.Start, run.StartedAt);
        Assert.Equal(JobsTestContext.Start.AddSeconds(2), run.FinishedAt);
        Assert.Equal(3, run.ItemsProcessed);
        Assert.Equal(1, run.ItemsFailed);
        Assert.Null(run.Error);
        Assert.Equal(run.RunId, Assert.Single(await ctx.RunsAsync()).RunId);
    }

    [Fact]
    public async Task RunNowAsync_JobRuns_ContextDescribesTheRun()
    {
        using var ctx = Create();

        var run = await ctx.Runner.RunNowAsync(DailyJob.JobName, CancellationToken.None);

        var context = Assert.Single(ctx.Probe.Runs).Context;
        Assert.Equal(run.RunId, context.RunId);
        Assert.Equal(DailyJob.JobName, context.JobName);
        Assert.Equal(JobTrigger.Manual, context.Trigger);
        Assert.Same(ctx.Clock, context.Clock);
    }

    [Fact]
    public async Task RunNowAsync_JobThrows_RecordsFailedWithoutThrowing()
    {
        using var ctx = Create();
        ctx.Probe.Behaviors[DailyJob.JobName] = (_, _) => throw new InvalidOperationException("boom");

        var run = await ctx.Runner.RunNowAsync(DailyJob.JobName, CancellationToken.None);

        Assert.Equal(JobRunStatus.Failed, run.Status);
        Assert.Equal("InvalidOperationException: boom", run.Error);
        Assert.Equal(JobRunStatus.Failed, Assert.Single(await ctx.RunsAsync()).Status);
    }

    [Fact]
    public async Task RunNowAsync_UnknownJob_ThrowsJobNotFound()
    {
        using var ctx = Create();

        var ex = await Assert.ThrowsAsync<JobNotFoundException>(() => ctx.Runner.RunNowAsync("nope", CancellationToken.None));

        Assert.Equal("nope", ex.JobName);
        Assert.Empty(await ctx.RunsAsync());
    }

    [Fact]
    public async Task RunNowAsync_EachRun_GetsItsOwnScopeThatIsDisposedAfterwards()
    {
        using var ctx = Create();

        await ctx.Runner.RunNowAsync(DailyJob.JobName, CancellationToken.None);
        await ctx.Runner.RunNowAsync(DailyJob.JobName, CancellationToken.None);

        var runs = ctx.Probe.Runs;
        Assert.Equal(2, runs.Count);
        Assert.NotEqual(runs[0].Dependency.Id, runs[1].Dependency.Id);
        Assert.All(runs, r => Assert.True(r.Dependency.Disposed));
    }

    [Fact]
    public async Task RunNowAsync_TokenCancelledDuringRun_RecordsCancelledFailure()
    {
        using var ctx = Create();
        using var cts = new CancellationTokenSource();
        ctx.Probe.Behaviors[DailyJob.JobName] = async (_, ct) =>
        {
            await cts.CancelAsync();
            await Task.Delay(Timeout.Infinite, ct);
        };

        var run = await ctx.Runner.RunNowAsync(DailyJob.JobName, cts.Token);

        Assert.Equal(JobRunStatus.Failed, run.Status);
        Assert.Equal(JobRunner.CancelledMessage, run.Error);
    }

    [Fact]
    public async Task RunNowAsync_JobThrowsCancellationWithoutRequest_RecordsOrdinaryFailure()
    {
        using var ctx = Create();
        ctx.Probe.Behaviors[DailyJob.JobName] = (_, _) => throw new OperationCanceledException("timeout inside job");

        var run = await ctx.Runner.RunNowAsync(DailyJob.JobName, CancellationToken.None);

        Assert.Equal(JobRunStatus.Failed, run.Status);
        Assert.Equal("OperationCanceledException: timeout inside job", run.Error);
    }

    [Fact]
    public async Task RunNowAsync_AlreadyRunning_RecordsSkippedRun()
    {
        using var ctx = Create();
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        ctx.Probe.Behaviors[DailyJob.JobName] = async (_, _) =>
        {
            started.SetResult();
            await release.Task;
        };

        var first = ctx.Runner.RunNowAsync(DailyJob.JobName, CancellationToken.None);
        await started.Task;
        var second = await ctx.Runner.RunNowAsync(DailyJob.JobName, CancellationToken.None);
        release.SetResult();
        var firstRun = await first;

        Assert.Equal(JobRunStatus.Skipped, second.Status);
        Assert.Equal(JobRunner.SkippedMessage, second.Error);
        Assert.Equal(second.StartedAt, second.FinishedAt);
        Assert.Equal(JobRunStatus.Succeeded, firstRun.Status);
        Assert.Equal(1, ctx.Probe.Count(DailyJob.JobName));
        Assert.Equal(2, (await ctx.RunsAsync()).Count);
    }

    [Fact]
    public async Task RunNowAsync_AfterPreviousRunFinished_RunsAgain()
    {
        using var ctx = Create();
        ctx.Probe.Behaviors[DailyJob.JobName] = (_, _) => throw new InvalidOperationException("first fails");
        await ctx.Runner.RunNowAsync(DailyJob.JobName, CancellationToken.None);
        ctx.Probe.Behaviors.Clear();

        var second = await ctx.Runner.RunNowAsync(DailyJob.JobName, CancellationToken.None);

        Assert.Equal(JobRunStatus.Succeeded, second.Status);
    }

    [Fact]
    public async Task RunNowAsync_LockHeldByAnotherInstance_RecordsSkippedWithoutRunning()
    {
        var locks = new FakeLockProvider { Available = false };
        using var ctx = JobsTestContext.Create(s => s.AddSingleton<IJobLockProvider>(locks).AddRecurringJob<DailyJob>());

        var run = await ctx.Runner.RunNowAsync(DailyJob.JobName, CancellationToken.None);

        Assert.Equal(JobRunStatus.Skipped, run.Status);
        Assert.Equal(JobRunner.SkippedElsewhereMessage, run.Error);
        Assert.Empty(ctx.Probe.Runs);
    }

    [Fact]
    public async Task RunNowAsync_LockAcquired_HeldDuringRunAndReleasedAfterwards()
    {
        var locks = new FakeLockProvider();
        using var ctx = JobsTestContext.Create(s => s.AddSingleton<IJobLockProvider>(locks).AddRecurringJob<DailyJob>());
        bool? heldDuringRun = null;
        ctx.Probe.Behaviors[DailyJob.JobName] = (_, _) =>
        {
            heldDuringRun = locks.Held;
            throw new InvalidOperationException("job fails");
        };

        await ctx.Runner.RunNowAsync(DailyJob.JobName, CancellationToken.None);

        Assert.Equal(DailyJob.JobName, locks.LastJobName);
        Assert.True(heldDuringRun);
        Assert.False(locks.Held);
    }

    [Fact]
    public void JobRunner_IsTheRegisteredIJobRunner()
    {
        using var ctx = Create();

        Assert.Same(ctx.Runner, ctx.Provider.GetRequiredService<IJobRunner>());
    }
}
