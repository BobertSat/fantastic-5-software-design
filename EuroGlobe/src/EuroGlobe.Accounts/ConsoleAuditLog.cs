using EuroGlobe.Contracts.Interfaces;
using EuroGlobe.Contracts.Time;

namespace EuroGlobe.Accounts;

public sealed class ConsoleAuditLog(IClock clock) : IAuditLog
{
    private readonly IClock _clock = clock;

    public Task LogAsync(Guid? userId, string action, string entityType, string entityId, string? details = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var timestamp = _clock.UtcNow.ToString("O");
        var userInfo = userId?.ToString() ?? "ANONYMOUS";
        Console.WriteLine($"[{timestamp}] AUDIT: {userInfo} | {action} | {entityType}:{entityId} | {details}");
        return Task.CompletedTask;
    }
}
