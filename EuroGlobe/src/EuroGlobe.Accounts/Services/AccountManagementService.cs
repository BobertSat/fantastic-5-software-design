using EuroGlobe.Accounts.Domain;
using EuroGlobe.Accounts.Repositories;
using EuroGlobe.Contracts.Interfaces;
using EuroGlobe.Contracts.Time;

namespace EuroGlobe.Accounts.Services;

public interface IAccountManagementService
{
    Task<IndividualAccount> CreateAsync(Guid userId, string currency, AccountType type = AccountType.Individual, CancellationToken cancellationToken = default);
    Task<IndividualAccount?> GetAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IndividualAccount>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class AccountManagementService(IAccountRepository accounts, IAuditLog audit, IClock clock) : IAccountManagementService
{
    private readonly IAccountRepository _accounts = accounts;
    private readonly IAuditLog _audit = audit;
    private readonly IClock _clock = clock;

    public async Task<IndividualAccount> CreateAsync(Guid userId, string currency, AccountType type = AccountType.Individual,
        CancellationToken cancellationToken = default)
    {
        string iban;
        do
        {
            iban = IbanGenerator.Generate();
        }
        while (await _accounts.GetByIbanAsync(iban, cancellationToken) is not null);

        var account = new IndividualAccount(userId, iban, currency, _clock, type);
        await _accounts.AddAsync(account, cancellationToken);
        await _audit.LogAsync(userId, "ACCOUNT_CREATED", "Account", account.Id.ToString(),
            $"{type} {account.Currency} {account.Iban}", cancellationToken);
        return account;
    }

    public Task<IndividualAccount?> GetAsync(Guid accountId, CancellationToken ct = default)
        => _accounts.GetByIdAsync(accountId, ct);

    public Task<IReadOnlyList<IndividualAccount>> ListByUserAsync(Guid userId, CancellationToken ct = default)
        => _accounts.ListByOwnerAsync(userId, ct);
}