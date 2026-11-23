using EuroGlobe.Contracts.Jobs;
using EuroGlobe.Jobs.Runs;

namespace EuroGlobe.Jobs.Tests.Runs;

public sealed class InMemoryJobRunStoreTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static JobRunRecord Run(
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
    public async Task SaveAsync_SameRunId_ReplacesTheRun()
    {
        var store = new InMemoryJobRunStore();
        var id = Guid.NewGuid();

        await store.SaveAsync(Run(id: id, status: JobRunStatus.Running), Ct);
        await store.SaveAsync(Run(id: id, status: JobRunStatus.Succeeded), Ct);

        var run = Assert.Single(await store.GetRecentAsync(null, 10, Ct));
        Assert.Equal(JobRunStatus.Succeeded, run.Status);
    }

    [Fact]
    public async Task SaveAsync_Null_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => new InMemoryJobRunStore().SaveAsync(null!, Ct));
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsNewestFirstFilteredAndLimited()
    {
        var store = new InMemoryJobRunStore();
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
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new InMemoryJobRunStore().GetRecentAsync(null, limit, Ct));
    }

    [Fact]
    public async Task GetLastScheduledForAsync_IgnoresManualRunsAndOtherJobs()
    {
        var store = new InMemoryJobRunStore();
        await store.SaveAsync(Run("job-a", 1), Ct);
        await store.SaveAsync(Run("job-a", 5, JobTrigger.Manual), Ct);
        await store.SaveAsync(Run("job-a", 2, status: JobRunStatus.Skipped), Ct);
        await store.SaveAsync(Run("job-b", 9), Ct);

        Assert.Equal(T0.AddMinutes(2), await store.GetLastScheduledForAsync("job-a", Ct));
    }

    [Fact]
    public async Task GetLastScheduledForAsync_NoScheduledRuns_ReturnsNull()
    {
        var store = new InMemoryJobRunStore();
        await store.SaveAsync(Run("job-a", 1, JobTrigger.Manual), Ct);

        Assert.Null(await store.GetLastScheduledForAsync("job-a", Ct));
    }

    [Fact]
    public async Task SaveAsync_OverCapacity_DropsOldestRuns()
    {
        var store = new InMemoryJobRunStore();
        for (var i = 0; i < InMemoryJobRunStore.Capacity + 5; i++)
        {
            await store.SaveAsync(Run(minutes: i), Ct);
        }

        var runs = await store.GetRecentAsync(null, InMemoryJobRunStore.Capacity + 5, Ct);

        Assert.Equal(InMemoryJobRunStore.Capacity, runs.Count);
        Assert.Equal(T0.AddMinutes(5), runs.Min(r => r.StartedAt));
    }
}
