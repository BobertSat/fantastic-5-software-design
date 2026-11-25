using EuroGlobe.Jobs.Persistence;
using EuroGlobe.Jobs.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace EuroGlobe.Jobs.Tests.Postgres;

/// <summary>One PostgreSQL container for all database tests; each test gets its own fresh database.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Same major version as the local dev container and Supabase (ADR-J005).
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Creates an empty database and, by default, applies the jobs migrations to it.</summary>
    public async Task<string> CreateDatabaseAsync(bool migrate = true)
    {
        var name = "test_" + Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name }.ConnectionString;
        if (migrate)
        {
            await using var provider = BuildJobsServices(connectionString);
            await using var db = await provider.GetRequiredService<IDbContextFactory<JobsDbContext>>().CreateDbContextAsync();
            await db.Database.MigrateAsync();
        }

        return connectionString;
    }

    /// <summary>The production registration (<c>AddEuroGlobeJobs</c>) pointed at the given database.</summary>
    internal static ServiceProvider BuildJobsServices(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEuroGlobeJobs(
            JobsTestContext.Configuration(("ConnectionStrings:EuroGlobe", connectionString), ("Jobs:SchedulerEnabled", "false")),
            JobsTestContext.Environment(Environments.Production));
        return services.BuildServiceProvider();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
