namespace EuroGlobe.Contracts.Interfaces;

public interface IScaService
{
    Task<bool> VerifyTotpAsync(Guid userId, string totpCode, CancellationToken cancellationToken = default);
}