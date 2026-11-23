namespace EuroGlobe.Jobs;

/// <summary>The <c>Jobs</c> configuration section.</summary>
public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    /// <summary>Unset means off in Development and on elsewhere (ADR-J005). Manual runs always work.</summary>
    public bool? SchedulerEnabled { get; set; }

    public int TickSeconds { get; set; } = 15;

    /// <summary>Use <see cref="Time.SimulatedClock"/> instead of real time. Allowed in Development only.</summary>
    public bool UseSimulatedClock { get; set; }
}
