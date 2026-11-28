using EuroGlobe.Mortgages.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace EuroGlobe.Mortgages.Tests.Postgres;

/// <summary>One PostgreSQL 17 container for all database tests; each test gets its own fresh database.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Creates an empty database and, by default, applies the mortgages migrations to it.</summary>
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
            await using var provider = TestSupport.BuildModule(("ConnectionStrings:EuroGlobe", connectionString));
            await using var scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<MortgagesDbContext>().Database.MigrateAsync();
        }

        return connectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
