using EuroGlobe.Contracts.Jobs;
using EuroGlobe.Contracts.Time;
using EuroGlobe.Jobs.Locking;
using EuroGlobe.Jobs.Persistence;
using EuroGlobe.Jobs.Registry;
using EuroGlobe.Jobs.Runs;
using EuroGlobe.Jobs.Running;
using EuroGlobe.Jobs.Scheduling;
using EuroGlobe.Jobs.Tests.TestDoubles;
using EuroGlobe.Jobs.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EuroGlobe.Jobs.Tests;

public sealed class JobsServiceCollectionExtensionsTests
{
    private static ServiceProvider Build(string environment, params (string Key, string Value)[] settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEuroGlobeJobs(JobsTestContext.Configuration(settings), JobsTestContext.Environment(environment));
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static bool HasScheduler(ServiceProvider provider) =>
        provider.GetServices<IHostedService>().OfType<JobSchedulerHostedService>().Any();

    [Theory]
    [InlineData("Development", null, false)]
    [InlineData("Production", null, true)]
    [InlineData("Staging", null, true)]
    [InlineData("Development", "true", true)]
    [InlineData("Production", "false", false)]
    public void AddEuroGlobeJobs_SchedulerEnabled_DefaultsToOffOnlyInDevelopment(string environment, string? setting, bool expected)
    {
        using var provider = setting is null
            ? Build(environment)
            : Build(environment, ("Jobs:SchedulerEnabled", setting));

        Assert.Equal(expected, HasScheduler(provider));
    }

    [Fact]
    public void AddEuroGlobeJobs_NoConfiguration_UsesDefaults()
    {
        using var provider = Build(Environments.Production);

        Assert.IsType<SystemClock>(provider.GetRequiredService<IClock>());
        Assert.Equal(15, provider.GetRequiredService<JobsOptions>().TickSeconds);
        Assert.IsType<JobRunner>(provider.GetRequiredService<IJobRunner>());
        Assert.Empty(provider.GetRequiredService<IJobRegistry>().Jobs);
        Assert.IsType<InMemoryJobRunStore>(provider.GetRequiredService<IJobRunStore>());
        Assert.IsType<NoOpJobLockProvider>(provider.GetRequiredService<IJobLockProvider>());

        var runtime = provider.GetRequiredService<JobsRuntime>();
        Assert.True(runtime.SchedulerEnabled);
        Assert.False(runtime.PersistentHistory);
        Assert.False(runtime.SimulatedClock);
    }

    [Fact]
    public void AddEuroGlobeJobs_WithConnectionString_UsesDatabaseStoreAndAdvisoryLock()
    {
        // Resolving these doesn't open a connection, so no database is needed here.
        using var provider = Build(Environments.Development,
            ("ConnectionStrings:EuroGlobe", "Host=localhost;Database=unused;Username=u;Password=p"),
            ("Jobs:UseSimulatedClock", "true"));

        Assert.IsType<EfJobRunStore>(provider.GetRequiredService<IJobRunStore>());
        Assert.IsType<PostgresAdvisoryJobLockProvider>(provider.GetRequiredService<IJobLockProvider>());

        var runtime = provider.GetRequiredService<JobsRuntime>();
        Assert.False(runtime.SchedulerEnabled);
        Assert.True(runtime.PersistentHistory);
        Assert.True(runtime.SimulatedClock);
    }

    [Fact]
    public void AddEuroGlobeJobs_BlankConnectionString_FallsBackToInMemory()
    {
        using var provider = Build(Environments.Production, ("ConnectionStrings:EuroGlobe", " "));

        Assert.IsType<InMemoryJobRunStore>(provider.GetRequiredService<IJobRunStore>());
    }

    [Fact]
    public void AddEuroGlobeJobs_SimulatedClockInDevelopment_RegistersOneSharedSimulatedClock()
    {
        using var provider = Build(Environments.Development, ("Jobs:UseSimulatedClock", "true"));

        var simulated = provider.GetRequiredService<SimulatedClock>();

        Assert.Same(simulated, provider.GetRequiredService<IClock>());
    }

    [Fact]
    public void AddEuroGlobeJobs_SimulatedClockOutsideDevelopment_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(Environments.Production, ("Jobs:UseSimulatedClock", "true")));

        Assert.Contains("Development", ex.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3601")]
    public void AddEuroGlobeJobs_TickSecondsOutOfRange_Throws(string tick)
    {
        Assert.Throws<InvalidOperationException>(() => Build(Environments.Production, ("Jobs:TickSeconds", tick)));
    }

    [Fact]
    public void AddEuroGlobeJobs_TickSecondsAtLimit_IsUsedByScheduler()
    {
        using var provider = Build(Environments.Production, ("Jobs:TickSeconds", "3600"));

        Assert.Equal(3600, provider.GetRequiredService<JobsOptions>().TickSeconds);
        Assert.True(HasScheduler(provider));
    }

    [Fact]
    public void AddEuroGlobeJobs_ClockAlreadyRegistered_KeepsIt()
    {
        var clock = new FakeClock(JobsTestContext.Start);
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(clock);

        services.AddEuroGlobeJobs(JobsTestContext.Configuration(), JobsTestContext.Environment(Environments.Production));

        using var provider = services.BuildServiceProvider();
        Assert.Same(clock, provider.GetRequiredService<IClock>());
    }

    [Fact]
    public async Task Host_WithRegisteredJob_RunsItOnScheduleAndOnDemand()
    {
        var clock = new FakeClock(JobsTestContext.Start);
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection([new("Jobs:TickSeconds", "1")]);
        builder.Services.AddSingleton<IClock>(clock);
        builder.Services.AddSingleton<JobProbe>();
        builder.Services.AddScoped<ScopedDependency>();
        builder.Services.AddEuroGlobeJobs(builder.Configuration, builder.Environment);
        builder.Services.AddRecurringJob<MinuteJob>();
        using var host = builder.Build();
        var probe = host.Services.GetRequiredService<JobProbe>();

        await host.StartAsync();
        clock.UtcNow = JobsTestContext.Start.AddMinutes(1);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (probe.Count(MinuteJob.JobName) == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        var manual = await host.Services.GetRequiredService<IJobRunner>().RunNowAsync(MinuteJob.JobName, CancellationToken.None);
        await host.StopAsync();

        Assert.Equal(JobTrigger.Scheduled, probe.Runs[0].Context.Trigger);
        Assert.Equal(JobTrigger.Manual, manual.Trigger);
        Assert.Equal(2, probe.Count(MinuteJob.JobName));
    }

    [Fact]
    public void AddRecurringJob_RegistersScopedJobAndAddsItToRegistry()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<JobProbe>();
        services.AddScoped<ScopedDependency>();
        services.AddEuroGlobeJobs(JobsTestContext.Configuration(), JobsTestContext.Environment(Environments.Production));

        services.AddRecurringJob<DailyJob>();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<DailyJob>());
        Assert.Equal(DailyJob.JobName, Assert.Single(provider.GetRequiredService<IJobRegistry>().Jobs).Name);
    }
}
