namespace EuroGlobe.Contracts.Interfaces;

public interface IAuditLog
{
    Task LogAsync(Guid? userId, string action, string entityType, string entityId, string? details = null, CancellationToken cancellationToken = default);
}