using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace EuroGlobe.Jobs.Locking;

/// <summary>
/// Cross-process guard so the same job never runs in two app instances at once (ADR-J003).
/// The in-process guard is the runner's per-job semaphore.
/// </summary>
internal interface IJobLockProvider
{
    /// <summary>Returns a handle that releases the lock when disposed, or <c>null</c> if another instance holds it.</summary>
    Task<IAsyncDisposable?> TryAcquireAsync(string jobName, CancellationToken cancellationToken);
}

/// <summary>Used without a database: a single process needs no cross-process lock.</summary>
internal sealed class NoOpJobLockProvider : IJobLockProvider
{
    public Task<IAsyncDisposable?> TryAcquireAsync(string jobName, CancellationToken cancellationToken) =>
        Task.FromResult<IAsyncDisposable?>(NoOpHandle.Instance);

    private sealed class NoOpHandle : IAsyncDisposable
    {
        public static readonly NoOpHandle Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>
/// PostgreSQL session advisory lock. The lock belongs to the database session, so the handle keeps one
/// dedicated connection open for the whole run and unlocks before closing it. If the process dies, the
/// session ends and PostgreSQL releases the lock.
/// </summary>
internal sealed class PostgresAdvisoryJobLockProvider(NpgsqlDataSource dataSource) : IJobLockProvider
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(string jobName, CancellationToken cancellationToken)
    {
        var key = LockKey(jobName);
        var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection);
            command.Parameters.AddWithValue("key", key);
            var acquired = (bool)(await command.ExecuteScalarAsync(cancellationToken))!;

            if (acquired)
            {
                return new Handle(connection, key);
            }
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        await connection.DisposeAsync();
        return null;
    }

    /// <summary>
    /// Stable 64-bit key: the first 8 bytes (big-endian) of SHA-256 of <c>euroglobe.jobs:{jobName}</c>.
    /// Never <c>string.GetHashCode()</c>, which differs between processes.
    /// </summary>
    internal static long LockKey(string jobName)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"euroglobe.jobs:{jobName}"));
        return BinaryPrimitives.ReadInt64BigEndian(hash);
    }

    private sealed class Handle(NpgsqlConnection connection, long key) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", connection);
                command.Parameters.AddWithValue("key", key);
                await command.ExecuteScalarAsync(CancellationToken.None);
            }
            finally
            {
                // Npgsql resets pooled connections (DISCARD ALL), which also drops any lock left behind.
                await connection.DisposeAsync();
            }
        }
    }
}
