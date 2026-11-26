using EuroGlobe.Contracts.Interfaces;

namespace EuroGlobe.Accounts;

public sealed class MockScaService : IScaService
{
    public Task<bool> VerifyTotpAsync(Guid userId, string totp, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // For testing: accept any 6-digit TOTP
        return Task.FromResult(totp.Length == 6 && totp.All(char.IsDigit));
    }
}
