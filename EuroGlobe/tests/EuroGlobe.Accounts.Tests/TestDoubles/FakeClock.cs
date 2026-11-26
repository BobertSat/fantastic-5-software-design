using EuroGlobe.Contracts.Time;

namespace EuroGlobe.Accounts.Tests.TestDoubles;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
}