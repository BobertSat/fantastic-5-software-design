using EuroGlobe.Contracts.Jobs;
using EuroGlobe.Jobs.Persistence;
using EuroGlobe.Jobs.Runs;
using EuroGlobe.Jobs.Tests.Runs;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EuroGlobe.Jobs.Tests.Postgres;

[Collection(PostgresCollection.Name)]
public sealed class EfJobRunStoreTests(PostgresFixture postgres) : JobRunStoreContractTests, IDisposable
{
    private readonly List<ServiceProvider> _providers = [];

    protected override async Task<IJobRunStore> CreateStoreAsync()
    {
        var provider = PostgresFixture.BuildJobsServices(await postgres.CreateDatabaseAsync());
        _providers.Add(provider);
        return provider.GetRequiredService<IJobRunStore>();
    }

    [Fact]
    public async Task Registration_WithConnectionString_UsesTheDatabaseStore()
    {
        Assert.IsType<EfJobRunStore>(await CreateStoreAsync());
    }

    [Fact]
    public async Task SaveAsync_NonUtcOffset_StoredAsTheSameInstantInUtc()
    {
        var store = await CreateStoreAsync();
        var vilnius = new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.FromHours(2));

        await store.SaveAsync(new JobRunRecord
        {
            RunId = Guid.NewGuid(),
            JobName = "job-a",
            Trigger = JobTrigger.Scheduled,
            ScheduledFor = vilnius,
            StartedAt = vilnius,
            FinishedAt = vilnius,
            Status = JobRunStatus.Succeeded,
        }, Ct);

        var stored = Assert.Single(await store.GetRecentAsync(null, 1, Ct));
        Assert.Equal(vilnius, stored.StartedAt);
        Assert.Equal(TimeSpan.Zero, stored.StartedAt.Offset);
    }

    [Fact]
    public async Task Migration_FreshDatabase_CreatesJobsSchemaAndOwnHistoryTable()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'jobs' ORDER BY table_name", connection);

        var tables = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        Assert.Equal(["__EFMigrationsHistory", "job_runs"], tables);
    }

    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }
    }
}
