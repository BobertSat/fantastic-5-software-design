using EuroGlobe.Accounts.Domain;

namespace EuroGlobe.Accounts.Repositories;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> ListByAccountAsync(
        Guid accountId,
        int take = 100,
        int skip = 0,
        CancellationToken cancellationToken = default);
}