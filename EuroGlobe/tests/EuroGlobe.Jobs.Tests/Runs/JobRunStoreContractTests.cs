using EuroGlobe.Contracts.Jobs;
using EuroGlobe.Jobs.Runs;

namespace EuroGlobe.Jobs.Tests.Runs;

/// <summary>Behaviour every <see cref="IJobRunStore"/> must have; run against each implementation.</summary>
public abstract class JobRunStoreContractTests
{
    protected static readonly DateTimeOffset T0 = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);
    protected static readonly CancellationToken Ct = CancellationToken.None;

    protected abstract Task<IJobRunStore> CreateStoreAsync();

    protected static JobRunRecord Run(
        string job = "job-a",
        int minutes = 0,
        JobTrigger trigger = JobTrigger.Scheduled,
        JobRunStatus status = JobRunStatus.Succeeded,
        Guid? id = null) => new()
    {
        RunId = id ?? Guid.NewGuid(),
        JobName = job,
        Trigger = trigger,
        ScheduledFor = T0.AddMinutes(minutes),
        StartedAt = T0.AddMinutes(minutes),
        Status = status,
    };

    [Fact]
    public async Task SaveAsync_NewRun_StoresAllFields()
    {
        var store = await CreateStoreAsync();
        var run = new JobRunRecord
        {
            RunId = Guid.NewGuid(),
            JobName = "job-a",
            Trigger = JobTrigger.Manual,
            ScheduledFor = T0,
            StartedAt = T0.AddSeconds(1),
            FinishedAt = T0.AddSeconds(3),
            Status = JobRunStatus.Failed,
            Error = "InvalidOperationException: boom",
            ItemsProcessed = 7,
            ItemsFailed = 2,
        };

        await store.SaveAsync(run, Ct);

        var stored = Assert.Single(await store.GetRecentAsync(null, 10, Ct));
        Assert.Equal(run.RunId, stored.RunId);
        Assert.Equal(run.JobName, stored.JobName);
        Assert.Equal(run.Trigger, stored.Trigger);
        Assert.Equal(run.ScheduledFor, stored.ScheduledFor);
        Assert.Equal(run.StartedAt, stored.StartedAt);
        Assert.Equal(run.FinishedAt, stored.FinishedAt);
        Assert.Equal(run.Status, stored.Status);
        Assert.Equal(run.Error, stored.Error);
        Assert.Equal(7, stored.ItemsProcessed);
        Assert.Equal(2, stored.ItemsFailed);
    }

    [Fact]
    public async Task SaveAsync_SameRunId_ReplacesTheRun()
    {
        var store = await CreateStoreAsync();
        var id = Guid.NewGuid();

        await store.SaveAsync(Run(id: id, status: JobRunStatus.Running), Ct);
        await store.SaveAsync(Run(id: id, status: JobRunStatus.Succeeded), Ct);

        var run = Assert.Single(await store.GetRecentAsync(null, 10, Ct));
        Assert.Equal(JobRunStatus.Succeeded, run.Status);
    }

    [Fact]
    public async Task SaveAsync_Null_Throws()
    {
        var store = await CreateStoreAsync();

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.SaveAsync(null!, Ct));
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsNewestFirstFilteredAndLimited()
    {
        var store = await CreateStoreAsync();
        await store.SaveAsync(Run("job-a", 1), Ct);
        await store.SaveAsync(Run("job-a", 3), Ct);
        await store.SaveAsync(Run("job-b", 2), Ct);
        await store.SaveAsync(Run("job-a", 2), Ct);

        var all = await store.GetRecentAsync(null, 10, Ct);
        var jobA = await store.GetRecentAsync("job-a", 2, Ct);

        Assert.Equal(4, all.Count);
        Assert.Equal([T0.AddMinutes(3), T0.AddMinutes(2)], jobA.Select(r => r.StartedAt));
        Assert.All(jobA, r => Assert.Equal("job-a", r.JobName));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetRecentAsync_NonPositiveLimit_Throws(int limit)
    {
        var store = await CreateStoreAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.GetRecentAsync(null, limit, Ct));
    }

    [Fact]
    public async Task GetLastScheduledForAsync_IgnoresManualRunsAndOtherJobs()
    {
        var store = await CreateStoreAsync();
        await store.SaveAsync(Run("job-a", 1), Ct);
        await store.SaveAsync(Run("job-a", 5, JobTrigger.Manual), Ct);
        await store.SaveAsync(Run("job-a", 2, status: JobRunStatus.Skipped), Ct);
        await store.SaveAsync(Run("job-b", 9), Ct);

        Assert.Equal(T0.AddMinutes(2), await store.GetLastScheduledForAsync("job-a", Ct));
    }

    [Fact]
    public async Task GetLastScheduledForAsync_NoScheduledRuns_ReturnsNull()
    {
        var store = await CreateStoreAsync();
        await store.SaveAsync(Run("job-a", 1, JobTrigger.Manual), Ct);

        Assert.Null(await store.GetLastScheduledForAsync("job-a", Ct));
    }
}
