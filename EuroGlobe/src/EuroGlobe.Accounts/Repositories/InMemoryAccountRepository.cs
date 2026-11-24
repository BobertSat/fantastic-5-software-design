using EuroGlobe.Accounts.Domain;

namespace EuroGlobe.Accounts.Repositories;

public sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly Dictionary<Guid, IndividualAccount> _accounts = new();

    public Task<IndividualAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _accounts.TryGetValue(id, out var account);
        return Task.FromResult<IndividualAccount?>(account);
    }

    public Task<IndividualAccount?> GetByIbanAsync(string iban, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var account = _accounts.Values.FirstOrDefault(a => a.Iban == iban);
        return Task.FromResult<IndividualAccount?>(account);
    }

    public Task<IReadOnlyList<IndividualAccount>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var accounts = _accounts.Values.Where(a => a.OwnerId == ownerId).ToList().AsReadOnly();
        return Task.FromResult<IReadOnlyList<IndividualAccount>>(accounts);
    }

    public Task AddAsync(IndividualAccount account, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _accounts[account.Id] = account;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(IndividualAccount account, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _accounts[account.Id] = account;
        return Task.CompletedTask;
    }
}
