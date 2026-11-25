using EuroGlobe.Jobs.Locking;
using Npgsql;

namespace EuroGlobe.Jobs.Tests.Postgres;

[Collection(PostgresCollection.Name)]
public sealed class AdvisoryLockTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    // Two data sources = two app instances sharing one database.
    private async Task<(PostgresAdvisoryJobLockProvider A, PostgresAdvisoryJobLockProvider B, NpgsqlDataSource[] Sources)> TwoInstancesAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync(migrate: false);
        var a = NpgsqlDataSource.Create(connectionString);
        var b = NpgsqlDataSource.Create(connectionString);
        return (new PostgresAdvisoryJobLockProvider(a), new PostgresAdvisoryJobLockProvider(b), [a, b]);
    }

    [Fact]
    public async Task TryAcquireAsync_HeldByAnotherInstance_ReturnsNullUntilReleased()
    {
        var (a, b, sources) = await TwoInstancesAsync();
        try
        {
            var held = await a.TryAcquireAsync("mortgage-repayments", Ct);
            Assert.NotNull(held);
            Assert.Null(await b.TryAcquireAsync("mortgage-repayments", Ct));

            await held.DisposeAsync();

            var second = await b.TryAcquireAsync("mortgage-repayments", Ct);
            Assert.NotNull(second);
            await second.DisposeAsync();
        }
        finally
        {
            foreach (var source in sources) await source.DisposeAsync();
        }
    }

    [Fact]
    public async Task TryAcquireAsync_DifferentJobs_DoNotBlockEachOther()
    {
        var (a, b, sources) = await TwoInstancesAsync();
        try
        {
            await using var mortgage = await a.TryAcquireAsync("mortgage-repayments", Ct);
            await using var leasing = await b.TryAcquireAsync("leasing-billing", Ct);

            Assert.NotNull(mortgage);
            Assert.NotNull(leasing);
        }
        finally
        {
            foreach (var source in sources) await source.DisposeAsync();
        }
    }

    [Fact]
    public async Task TryAcquireAsync_HolderSessionDies_LockIsReleased()
    {
        var (a, b, sources) = await TwoInstancesAsync();
        try
        {
            var held = await a.TryAcquireAsync("mortgage-repayments", Ct);
            Assert.NotNull(held);

            // Simulate the holding process crashing: kill every other session on this database.
            await using (var killer = await sources[1].OpenConnectionAsync())
            {
                await using var kill = new NpgsqlCommand(
                    "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = current_database() AND pid <> pg_backend_pid()",
                    killer);
                await kill.ExecuteNonQueryAsync();
            }

            // Termination is asynchronous: the lock goes away once the backend has exited.
            IAsyncDisposable? afterCrash = null;
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (afterCrash is null && DateTime.UtcNow < deadline)
            {
                afterCrash = await b.TryAcquireAsync("mortgage-repayments", Ct);
                if (afterCrash is null) await Task.Delay(50);
            }

            Assert.NotNull(afterCrash);
            await afterCrash.DisposeAsync();
            await Assert.ThrowsAnyAsync<Exception>(async () => await held.DisposeAsync());
        }
        finally
        {
            foreach (var source in sources) await source.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("mortgage-repayments", 2022745863626709181)]
    [InlineData("leasing-billing", 7597795456907053794)]
    public void LockKey_IsStableFirst8BytesOfSha256(string jobName, long expected)
    {
        // Expected values computed independently (Python hashlib), so the key never changes between processes.
        Assert.Equal(expected, PostgresAdvisoryJobLockProvider.LockKey(jobName));
    }

    [Fact]
    public async Task NoOpProvider_AlwaysAcquires()
    {
        var provider = new NoOpJobLockProvider();

        await using var first = await provider.TryAcquireAsync("job", Ct);
        await using var second = await provider.TryAcquireAsync("job", Ct);

        Assert.NotNull(first);
        Assert.NotNull(second);
    }
}
