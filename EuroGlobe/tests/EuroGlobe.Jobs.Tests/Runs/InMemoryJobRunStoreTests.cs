using EuroGlobe.Jobs.Runs;

namespace EuroGlobe.Jobs.Tests.Runs;

public sealed class InMemoryJobRunStoreTests : JobRunStoreContractTests
{
    protected override Task<IJobRunStore> CreateStoreAsync() => Task.FromResult<IJobRunStore>(new InMemoryJobRunStore());

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
