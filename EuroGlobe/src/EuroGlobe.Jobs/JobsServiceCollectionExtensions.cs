using EuroGlobe.Contracts.Jobs;
using EuroGlobe.Contracts.Time;
using EuroGlobe.Jobs.Locking;
using EuroGlobe.Jobs.Persistence;
using EuroGlobe.Jobs.Registry;
using EuroGlobe.Jobs.Running;
using EuroGlobe.Jobs.Runs;
using EuroGlobe.Jobs.Scheduling;
using EuroGlobe.Jobs.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace EuroGlobe.Jobs;

/// <summary>What <c>AddEuroGlobeJobs</c> actually enabled, after applying defaults.</summary>
public sealed class JobsRuntime(bool schedulerEnabled, bool persistentHistory, bool simulatedClock)
{
    public bool SchedulerEnabled { get; } = schedulerEnabled;

    /// <summary>True when run history is stored in PostgreSQL; false when it is in memory.</summary>
    public bool PersistentHistory { get; } = persistentHistory;

    public bool SimulatedClock { get; } = simulatedClock;
}

public static class JobsServiceCollectionExtensions
{
    public const string ConnectionStringName = "EuroGlobe";

    internal const int MaxTickSeconds = 3_600;

    /// <summary>
    /// Registers the clock, the job runner and (if enabled) the scheduler. With <c>ConnectionStrings:EuroGlobe</c>
    /// set, run history is stored in PostgreSQL and runs are guarded by an advisory lock; otherwise both are
    /// in-process only. See ADR-J001, J002, J003, J005.
    /// </summary>
    /// <exception cref="InvalidOperationException">The <c>Jobs</c> configuration is invalid.</exception>
    public static IServiceCollection AddEuroGlobeJobs(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration.GetSection(JobsOptions.SectionName).Get<JobsOptions>() ?? new JobsOptions();

        if (options.TickSeconds is < 1 or > MaxTickSeconds)
        {
            throw new InvalidOperationException($"Jobs:TickSeconds must be between 1 and {MaxTickSeconds}.");
        }

        if (options.UseSimulatedClock && !environment.IsDevelopment())
        {
            throw new InvalidOperationException("Jobs:UseSimulatedClock is allowed only in the Development environment.");
        }

        services.AddSingleton(options);

        if (options.UseSimulatedClock)
        {
            services.TryAddSingleton(_ => new SimulatedClock(new SystemClock()));
            services.TryAddSingleton<IClock>(sp => sp.GetRequiredService<SimulatedClock>());
        }
        else
        {
            services.TryAddSingleton<IClock, SystemClock>();
        }

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        var persistent = !string.IsNullOrWhiteSpace(connectionString);
        if (persistent)
        {
            services.TryAddSingleton(_ => NpgsqlDataSource.Create(connectionString!));
            services.AddDbContextFactory<JobsDbContext>((sp, db) => db
                .UseNpgsql(
                    sp.GetRequiredService<NpgsqlDataSource>(),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", JobsDbContext.Schema))
                .UseSnakeCaseNamingConvention());
            services.TryAddSingleton<IJobRunStore, EfJobRunStore>();
            services.TryAddSingleton<IJobLockProvider, PostgresAdvisoryJobLockProvider>();
        }
        else
        {
            services.TryAddSingleton<IJobRunStore, InMemoryJobRunStore>();
            services.TryAddSingleton<IJobLockProvider, NoOpJobLockProvider>();
        }

        services.TryAddSingleton<IJobRegistry, JobRegistry>();
        services.TryAddSingleton<JobRunner>();
        services.TryAddSingleton<IJobRunner>(sp => sp.GetRequiredService<JobRunner>());

        var schedulerEnabled = options.SchedulerEnabled ?? !environment.IsDevelopment();
        if (schedulerEnabled)
        {
            services.AddHostedService<JobSchedulerHostedService>();
        }

        services.AddSingleton(new JobsRuntime(schedulerEnabled, persistent, options.UseSimulatedClock));
        return services;
    }

    /// <summary>
    /// Registers a job. It is resolved from a new DI scope for every run, so it may depend on scoped
    /// services such as a DbContext.
    /// </summary>
    public static IServiceCollection AddRecurringJob<TJob>(this IServiceCollection services)
        where TJob : class, IRecurringJob
    {
        services.TryAddScoped<TJob>();
        services.AddSingleton(new JobRegistration(typeof(TJob)));
        return services;
    }
}
