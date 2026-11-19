using EuroGlobe.Accounts.Domain;

namespace EuroGlobe.Accounts.Repositories;

public interface IAccountRepository
{
    Task<IndividualAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IndividualAccount?> GetByIbanAsync(string iban, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IndividualAccount>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task AddAsync(IndividualAccount account, CancellationToken cancellationToken = default);
    Task UpdateAsync(IndividualAccount account, CancellationToken cancellationToken = default);
}