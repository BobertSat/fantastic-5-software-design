using EuroGlobe.Accounts.Domain;

namespace EuroGlobe.Accounts.Repositories;

public sealed class InMemoryTransactionRepository : ITransactionRepository
{
    private readonly List<Transaction> _transactions = new();
    private readonly Lock _lock = new();

    public Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _transactions.Add(transaction);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Transaction>> ListByAccountAsync(Guid accountId, int take = 100, int skip = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            var transactions = _transactions
                .Where(t => t.AccountId == accountId)
                .Skip(skip)
                .Take(take)
                .ToList()
                .AsReadOnly();
            return Task.FromResult<IReadOnlyList<Transaction>>(transactions);
        }
    }
}
