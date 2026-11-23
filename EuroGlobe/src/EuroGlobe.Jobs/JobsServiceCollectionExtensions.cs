using EuroGlobe.Contracts.Jobs;
using EuroGlobe.Contracts.Time;
using EuroGlobe.Jobs.Registry;
using EuroGlobe.Jobs.Running;
using EuroGlobe.Jobs.Runs;
using EuroGlobe.Jobs.Scheduling;
using EuroGlobe.Jobs.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EuroGlobe.Jobs;

public static class JobsServiceCollectionExtensions
{
    internal const int MaxTickSeconds = 3_600;

    /// <summary>Registers the clock, the job runner and (if enabled) the scheduler. See ADR-J001, J002, J005.</summary>
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

        services.TryAddSingleton<IJobRunStore, InMemoryJobRunStore>();
        services.TryAddSingleton<IJobRegistry, JobRegistry>();
        services.TryAddSingleton<JobRunner>();
        services.TryAddSingleton<IJobRunner>(sp => sp.GetRequiredService<JobRunner>());

        if (options.SchedulerEnabled ?? !environment.IsDevelopment())
        {
            services.AddHostedService<JobSchedulerHostedService>();
        }

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
