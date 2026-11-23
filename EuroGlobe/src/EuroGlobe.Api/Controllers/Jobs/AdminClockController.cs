using EuroGlobe.Api.Controllers.Demo;
using EuroGlobe.Contracts.Time;
using EuroGlobe.Jobs.Time;
using Microsoft.AspNetCore.Mvc;

namespace EuroGlobe.Api.Controllers.Jobs;

/// <summary>Demo tools: read the clock, and move the simulated clock forward or back to real time (ADR-J002).</summary>
[ApiController]
[DemoAdminEndpoint]
[Route("api/admin/clock")]
public sealed class AdminClockController(IClock clock) : ControllerBase
{
    internal const int MaxAdvanceDays = 3_650;

    [HttpGet]
    public ClockResponse GetClock() => ClockResponse.From(clock);

    /// <summary>Moves the simulated clock forward. Due jobs run on the scheduler's next tick, or use run-now.</summary>
    [HttpPost("advance")]
    [ProducesResponseType<ClockResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Advance(AdvanceClockRequest request)
    {
        if (clock is not SimulatedClock simulated)
        {
            return SimulationDisabled();
        }

        if (request.Days < 0 || request.Hours < 0 || request.Minutes < 0)
        {
            return this.ProblemWithCode(StatusCodes.Status400BadRequest, "INVALID_CLOCK_ADVANCE", "Invalid clock advance",
                "Days, hours and minutes must not be negative; use reset to go back to real time.");
        }

        var by = TimeSpan.FromDays(request.Days) + TimeSpan.FromHours(request.Hours) + TimeSpan.FromMinutes(request.Minutes);
        if (by <= TimeSpan.Zero || by > TimeSpan.FromDays(MaxAdvanceDays))
        {
            return this.ProblemWithCode(StatusCodes.Status400BadRequest, "INVALID_CLOCK_ADVANCE", "Invalid clock advance",
                $"Advance by more than zero and at most {MaxAdvanceDays} days.");
        }

        simulated.Advance(by);
        return Ok(ClockResponse.From(clock));
    }

    /// <summary>Returns the simulated clock to real time.</summary>
    [HttpPost("reset")]
    [ProducesResponseType<ClockResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Reset()
    {
        if (clock is not SimulatedClock simulated)
        {
            return SimulationDisabled();
        }

        simulated.Reset();
        return Ok(ClockResponse.From(clock));
    }

    private ObjectResult SimulationDisabled() =>
        this.ProblemWithCode(StatusCodes.Status409Conflict, "SIMULATED_CLOCK_DISABLED", "Simulated clock is disabled",
            "Set Jobs:UseSimulatedClock=true (Development only) to move time.");
}

public sealed class AdvanceClockRequest
{
    public int Days { get; init; }

    public int Hours { get; init; }

    public int Minutes { get; init; }
}

public sealed class ClockResponse
{
    public required DateTimeOffset UtcNow { get; init; }

    public required DateOnly Today { get; init; }

    public required bool Simulated { get; init; }

    /// <summary>How far the simulated clock is ahead of real time; zero for the real clock.</summary>
    public required TimeSpan Offset { get; init; }

    internal static ClockResponse From(IClock clock)
    {
        var simulated = clock as SimulatedClock;
        return new ClockResponse
        {
            UtcNow = clock.UtcNow,
            Today = clock.Today,
            Simulated = simulated is not null,
            Offset = simulated?.Offset ?? TimeSpan.Zero,
        };
    }
}
