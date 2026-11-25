using EuroGlobe.Jobs.Locking;

namespace EuroGlobe.Jobs.Tests.TestDoubles;

/// <summary>Stands in for another app instance: when not <see cref="Available"/>, the lock is "held elsewhere".</summary>
internal sealed class FakeLockProvider : IJobLockProvider
{
    public bool Available { get; set; } = true;

    public bool Held { get; private set; }

    public string? LastJobName { get; private set; }

    public Task<IAsyncDisposable?> TryAcquireAsync(string jobName, CancellationToken cancellationToken)
    {
        LastJobName = jobName;
        if (!Available)
        {
            return Task.FromResult<IAsyncDisposable?>(null);
        }

        Held = true;
        return Task.FromResult<IAsyncDisposable?>(new Release(this));
    }

    private sealed class Release(FakeLockProvider owner) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            owner.Held = false;
            return ValueTask.CompletedTask;
        }
    }
}
