using EuroGlobe.Contracts.Time;
using EuroGlobe.Jobs.Registry;
using EuroGlobe.Jobs.Running;
using EuroGlobe.Jobs.Runs;
using EuroGlobe.Jobs.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace EuroGlobe.Jobs.Tests.TestDoubles;

/// <summary>A real DI container wired with <c>AddEuroGlobeJobs</c>, a fake clock and probe jobs.</summary>
internal sealed class JobsTestContext : IDisposable
{
    public static readonly DateTimeOffset Start = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);

    private JobsTestContext(ServiceProvider provider, FakeClock clock)
    {
        Provider = provider;
        Clock = clock;
    }

    public ServiceProvider Provider { get; }

    public FakeClock Clock { get; }

    public JobProbe Probe => Provider.GetRequiredService<JobProbe>();

    public IJobRunStore Store => Provider.GetRequiredService<IJobRunStore>();

    public JobRunner Runner => Provider.GetRequiredService<JobRunner>();

    public IJobRegistry Registry => Provider.GetRequiredService<IJobRegistry>();

    public static JobsTestContext Create(Action<IServiceCollection> configure)
    {
        var clock = new FakeClock(Start);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<JobProbe>();
        services.AddSingleton<ConfigurableJobSettings>();
        services.AddScoped<ScopedDependency>();
        configure(services);
        services.AddEuroGlobeJobs(Configuration(("Jobs:SchedulerEnabled", "false")), Environment(Environments.Production));

        return new JobsTestContext(services.BuildServiceProvider(validateScopes: true), clock);
    }

    public JobSchedulerHostedService CreateScheduler(TimeSpan? tick = null) =>
        new(Registry, Runner, Store, Clock, NullLogger<JobSchedulerHostedService>.Instance, tick ?? TimeSpan.FromSeconds(1));

    public async Task<IReadOnlyList<JobRunRecord>> RunsAsync(string? jobName = null) =>
        await Store.GetRecentAsync(jobName, 100, CancellationToken.None);

    public static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    public static IHostEnvironment Environment(string name) => new HostingEnvironment { EnvironmentName = name };

    public void Dispose() => Provider.Dispose();
}
