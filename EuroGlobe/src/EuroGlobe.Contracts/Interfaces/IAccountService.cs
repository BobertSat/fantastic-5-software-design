namespace EuroGlobe.Contracts.Interfaces;

public interface IAccountService
{
    Task<decimal> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<decimal> GetAvailableBalanceAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task ReserveAsync(Guid accountId, decimal amount, CancellationToken cancellationToken = default);
    Task ReleaseAsync(Guid accountId, decimal amount, CancellationToken cancellationToken = default);
    Task DebitAsync(Guid accountId, decimal amount, string description, CancellationToken cancellationToken = default);
    Task CreditAsync(Guid accountId, decimal amount, string description, CancellationToken cancellationToken = default);
    Task<Guid> TransferAsync(Guid fromAccountId, string toIban, decimal amount, string currency, string totpCode, CancellationToken cancellationToken = default);
}