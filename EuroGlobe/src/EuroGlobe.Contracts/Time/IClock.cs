namespace EuroGlobe.Contracts.Time;

/// <summary>
/// The only source of "now" (ADR-J002). Never use <c>DateTime.Now</c>/<c>UtcNow</c> directly:
/// in Development the clock may be simulated and advanced through the admin endpoints.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}
