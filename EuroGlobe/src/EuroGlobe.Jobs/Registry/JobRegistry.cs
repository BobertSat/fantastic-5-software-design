using System.Text.RegularExpressions;
using EuroGlobe.Contracts.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace EuroGlobe.Jobs.Registry;

public sealed class RegisteredJob(string name, JobSchedule schedule, Type jobType)
{
    public string Name { get; } = name;

    public JobSchedule Schedule { get; } = schedule;

    public Type JobType { get; } = jobType;
}

public interface IJobRegistry
{
    IReadOnlyList<RegisteredJob> Jobs { get; }

    RegisteredJob? Find(string name);
}

/// <summary>Marker added by <c>AddRecurringJob&lt;TJob&gt;()</c>.</summary>
internal sealed class JobRegistration(Type jobType)
{
    public Type JobType { get; } = jobType;
}

/// <summary>
/// Reads each job's name and schedule once, by resolving it in a throw-away scope, and validates them.
/// </summary>
internal sealed partial class JobRegistry : IJobRegistry
{
    internal const int MaxNameLength = 100;

    private readonly Lazy<IReadOnlyList<RegisteredJob>> _jobs;

    public JobRegistry(IEnumerable<JobRegistration> registrations, IServiceScopeFactory scopeFactory)
    {
        var snapshot = registrations.ToList();
        _jobs = new Lazy<IReadOnlyList<RegisteredJob>>(() => Build(snapshot, scopeFactory));
    }

    public IReadOnlyList<RegisteredJob> Jobs => _jobs.Value;

    public RegisteredJob? Find(string name) => Jobs.FirstOrDefault(j => j.Name == name);

    private static List<RegisteredJob> Build(List<JobRegistration> registrations, IServiceScopeFactory scopeFactory)
    {
        using var scope = scopeFactory.CreateScope();
        var jobs = new List<RegisteredJob>();

        foreach (var registration in registrations)
        {
            var job = (IRecurringJob)scope.ServiceProvider.GetRequiredService(registration.JobType);
            var name = job.Name;

            if (name is null || name.Length > MaxNameLength || !KebabCase().IsMatch(name))
            {
                throw new InvalidOperationException(
                    $"Job {registration.JobType.Name} has invalid name '{name}'. Use kebab-case, e.g. 'mortgage-repayments' (max {MaxNameLength} characters).");
            }

            var duplicate = jobs.Find(j => j.Name == name);
            if (duplicate is not null)
            {
                throw new InvalidOperationException(
                    $"Jobs {duplicate.JobType.Name} and {registration.JobType.Name} both use the name '{name}'.");
            }

            var schedule = job.Schedule
                ?? throw new InvalidOperationException($"Job '{name}' has no schedule.");

            jobs.Add(new RegisteredJob(name, schedule, registration.JobType));
        }

        return jobs;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex KebabCase();
}
