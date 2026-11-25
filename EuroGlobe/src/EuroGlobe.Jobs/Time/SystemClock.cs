using EuroGlobe.Contracts.Time;

namespace EuroGlobe.Jobs.Time;

/// <summary>Real time. The only place allowed to read <see cref="DateTimeOffset.UtcNow"/> directly.</summary>
internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
