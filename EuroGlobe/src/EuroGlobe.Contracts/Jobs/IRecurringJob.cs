namespace EuroGlobe.Contracts.Jobs;

/// <summary>
/// A job run by the shared scheduler (ADR-J001). Each run resolves the job from a new DI scope.
/// Rules for job authors:
/// <list type="number">
/// <item>Idempotent: running again for the same "now" must do nothing new.</item>
/// <item>Take time only from <see cref="JobContext.Clock"/>, never <c>DateTime.Now</c>.</item>
/// <item>Let exceptions propagate (the run is recorded as Failed); log per-item failures and call <see cref="JobContext.ReportFailed"/>.</item>
/// <item>Keep runs short, work in batches, honour the <see cref="CancellationToken"/>.</item>
/// </list>
/// </summary>
public interface IRecurringJob
{
    /// <summary>Unique, stable, kebab-case (e.g. <c>mortgage-repayments</c>); used in URLs, logs and locks.</summary>
    string Name { get; }

    JobSchedule Schedule { get; }

    Task ExecuteAsync(JobContext context, CancellationToken cancellationToken);
}
