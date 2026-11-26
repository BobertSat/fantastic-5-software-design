using EuroGlobe.Accounts.Domain;
using EuroGlobe.Accounts.Repositories;
using EuroGlobe.Accounts.Services;
using EuroGlobe.Accounts.Tests.TestDoubles;
using EuroGlobe.Accounts.Transfers;

namespace EuroGlobe.Accounts.Tests.Services;

internal sealed class ServiceEnv
{
    public FakeClock Clock { get; } = new();
    public InMemoryAccountRepository Accounts { get; } = new();
    public InMemoryTransactionRepository Ledger { get; } = new();
    public FakeAuditLog Audit { get; } = new();
    public FakeScaService Sca { get; } = new();
    public MockSepaGateway Sepa { get; } = new();
    public AccountService Service { get; }
    public AccountManagementService Management { get; }

    public ServiceEnv()
    {
        Service = new AccountService(Accounts, Ledger, Audit, Sca, Sepa, Clock);
        Management = new AccountManagementService(Accounts, Audit, Clock);
    }

    /// <summary>Adds an account straight to the repository. Seed funds leave no ledger or audit entries.</summary>
    public async Task<IndividualAccount> NewAccountAsync(decimal funds = 0m, string currency = "EUR", Guid? ownerId = null)
    {
        var account = TestData.NewAccount(Clock, currency, funds, ownerId);
        await Accounts.AddAsync(account);
        return account;
    }
}
