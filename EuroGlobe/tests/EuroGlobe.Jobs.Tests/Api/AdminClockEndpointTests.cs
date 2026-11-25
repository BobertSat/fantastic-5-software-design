using System.Net;
using System.Net.Http.Json;
using EuroGlobe.Api.Controllers.Jobs;

namespace EuroGlobe.Jobs.Tests.Api;

public sealed class AdminClockEndpointTests
{
    [Fact]
    public async Task Clock_AdvanceThenReset_MovesSimulatedTimeAndBack()
    {
        await using var api = new ApiFactory();
        var client = api.CreateClient();

        var initial = await client.GetFromJsonAsync<ClockResponse>("/api/admin/clock");
        var advanced = await (await client.PostAsJsonAsync("/api/admin/clock/advance", new { days = 30, hours = 2, minutes = 5 }))
            .Content.ReadFromJsonAsync<ClockResponse>();
        var reset = await (await client.PostAsync("/api/admin/clock/reset", null)).Content.ReadFromJsonAsync<ClockResponse>();

        Assert.True(initial!.Simulated);
        Assert.Equal(TimeSpan.Zero, initial.Offset);
        var expectedOffset = new TimeSpan(30, 2, 5, 0);
        Assert.Equal(expectedOffset, advanced!.Offset);
        Assert.InRange(advanced.UtcNow - initial.UtcNow, expectedOffset, expectedOffset + TimeSpan.FromMinutes(1));
        Assert.Equal(DateOnly.FromDateTime(advanced.UtcNow.UtcDateTime), advanced.Today);
        Assert.Equal(TimeSpan.Zero, reset!.Offset);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -5)]
    [InlineData(0, 0, 0)]
    [InlineData(3651, 0, 0)]
    public async Task Advance_InvalidAmount_Returns400(int days, int hours, int minutes)
    {
        await using var api = new ApiFactory();

        var response = await api.CreateClient().PostAsJsonAsync("/api/admin/clock/advance", new { days, hours, minutes });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("INVALID_CLOCK_ADVANCE", (await ApiFactory.ReadProblemAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Clock_SimulationDisabled_ReadsRealTimeAndRefusesToMove()
    {
        await using var api = new ApiFactory(overrides: ("Jobs:UseSimulatedClock", "false"));
        var client = api.CreateClient();

        var clock = await client.GetFromJsonAsync<ClockResponse>("/api/admin/clock");
        var advance = await client.PostAsJsonAsync("/api/admin/clock/advance", new { days = 1 });
        var reset = await client.PostAsync("/api/admin/clock/reset", null);

        Assert.False(clock!.Simulated);
        Assert.InRange(clock.UtcNow, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.Conflict, advance.StatusCode);
        Assert.Equal("SIMULATED_CLOCK_DISABLED", (await ApiFactory.ReadProblemAsync(advance)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, reset.StatusCode);
    }
}
