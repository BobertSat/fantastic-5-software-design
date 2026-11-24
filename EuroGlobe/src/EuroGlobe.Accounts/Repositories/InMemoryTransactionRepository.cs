using EuroGlobe.Accounts.Domain;

namespace EuroGlobe.Accounts.Repositories;

public sealed class InMemoryTransactionRepository : ITransactionRepository
{
    private readonly List<Transaction> _transactions = new();

    public Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _transactions.Add(transaction);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Transaction>> ListByAccountAsync(Guid accountId, int take = 100, int skip = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var transactions = _transactions
            .Where(t => t.AccountId == accountId)
            .Skip(skip)
            .Take(take)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<Transaction>>(transactions);
    }
}
