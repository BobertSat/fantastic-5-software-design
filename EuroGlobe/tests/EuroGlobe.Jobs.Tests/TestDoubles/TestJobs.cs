using System.Collections.Concurrent;
using EuroGlobe.Contracts.Jobs;

namespace EuroGlobe.Jobs.Tests.TestDoubles;

/// <summary>Singleton that records job runs and lets a test inject behaviour per job.</summary>
internal sealed class JobProbe
{
    private readonly ConcurrentQueue<ProbeRun> _runs = new();

    public ConcurrentDictionary<string, Func<JobContext, CancellationToken, Task>> Behaviors { get; } = new();

    public IReadOnlyList<ProbeRun> Runs => _runs.ToList();

    public int Count(string jobName) => _runs.Count(r => r.JobName == jobName);

    public Task OnRunAsync(string jobName, JobContext context, ScopedDependency dependency, CancellationToken ct)
    {
        _runs.Enqueue(new ProbeRun(jobName, context, dependency));
        return Behaviors.TryGetValue(jobName, out var behavior) ? behavior(context, ct) : Task.CompletedTask;
    }
}

internal sealed record ProbeRun(string JobName, JobContext Context, ScopedDependency Dependency);

/// <summary>Scoped service, to prove each run gets (and disposes) its own scope.</summary>
internal sealed class ScopedDependency : IDisposable
{
    public Guid Id { get; } = Guid.NewGuid();

    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;
}

internal abstract class ProbeJob(JobProbe probe, ScopedDependency dependency) : IRecurringJob
{
    public abstract string Name { get; }

    public abstract JobSchedule Schedule { get; }

    public Task ExecuteAsync(JobContext context, CancellationToken cancellationToken) =>
        probe.OnRunAsync(Name, context, dependency, cancellationToken);
}

internal sealed class DailyJob(JobProbe probe, ScopedDependency dependency) : ProbeJob(probe, dependency)
{
    public const string JobName = "daily-job";

    public override string Name => JobName;

    public override JobSchedule Schedule => JobSchedule.DailyAt(new TimeOnly(2, 0));
}

internal sealed class MinuteJob(JobProbe probe, ScopedDependency dependency) : ProbeJob(probe, dependency)
{
    public const string JobName = "minute-job";

    public override string Name => JobName;

    public override JobSchedule Schedule => JobSchedule.Every(TimeSpan.FromMinutes(1));
}

internal sealed class ConfigurableJobSettings
{
    public string? Name { get; set; } = "configurable-job";

    public JobSchedule? Schedule { get; set; } = JobSchedule.Every(TimeSpan.FromHours(1));
}

/// <summary>A job whose name and schedule come from <see cref="ConfigurableJobSettings"/>, for validation tests.</summary>
internal sealed class ConfigurableJob(ConfigurableJobSettings settings) : IRecurringJob
{
    public string Name => settings.Name!;

    public JobSchedule Schedule => settings.Schedule!;

    public Task ExecuteAsync(JobContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}
