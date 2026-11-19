using System.Globalization;
using EuroGlobe.Accounts.Domain;
using EuroGlobe.Accounts.Repositories;
using EuroGlobe.Accounts.Transfers;
using EuroGlobe.Contracts.Interfaces;

namespace EuroGlobe.Accounts.Services;

public sealed class AccountService(IAccountRepository accounts, ITransactionRepository ledger,
    IAuditLog audit, IScaService sca, ISepaGateway sepa) : IAccountService
{
    private readonly IAccountRepository _accounts = accounts;
    private readonly ITransactionRepository _ledger = ledger;
    private readonly IAuditLog _audit = audit;
    private readonly TransferPipeline _pipeline = new(accounts, ledger, audit, sca, sepa);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<decimal> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken = default)
        => (await RequireAsync(accountId, cancellationToken)).Balance;

    public async Task<decimal> GetAvailableBalanceAsync(Guid accountId, CancellationToken cancellationToken = default)
        => (await RequireAsync(accountId, cancellationToken)).AvailableBalance;

    public Task ReserveAsync(Guid accountId, decimal amount, CancellationToken cancellationToken = default)
        => RunLockedAsync(async () =>
        {
            var acc = await RequireAsync(accountId, cancellationToken);
            acc.Reserve(amount);
            await _audit.LogAsync(acc.OwnerId, "ACCOUNT_RESERVE", "Account", acc.Id.ToString(),
                $"{Fmt(amount)} {acc.Currency}", cancellationToken);
        }, cancellationToken);

    public Task ReleaseAsync(Guid accountId, decimal amount, CancellationToken cancellationToken = default)
        => RunLockedAsync(async () =>
        {
            var acc = await RequireAsync(accountId, cancellationToken);
            acc.Release(amount);
            await _audit.LogAsync(acc.OwnerId, "ACCOUNT_RELEASE", "Account", acc.Id.ToString(),
                $"{Fmt(amount)} {acc.Currency}", cancellationToken);
        }, cancellationToken);

    public Task DebitAsync(Guid accountId, decimal amount, string description, CancellationToken cancellationToken = default)
        => RunLockedAsync(async () =>
        {
            var acc = await RequireAsync(accountId, cancellationToken);
            acc.Debit(amount);
            await _ledger.AddAsync(new Transaction(acc.Id, TransactionType.Withdrawal, -amount, acc.Currency, description), cancellationToken);
            await _audit.LogAsync(acc.OwnerId, "ACCOUNT_DEBIT", "Account", acc.Id.ToString(),
                $"{Fmt(amount)} {acc.Currency}: {description}", cancellationToken);
        }, cancellationToken);

    public Task CreditAsync(Guid accountId, decimal amount, string description, CancellationToken cancellationToken = default)
        => RunLockedAsync(async () =>
        {
            var acc = await RequireAsync(accountId, cancellationToken);
            acc.Credit(amount);
            await _ledger.AddAsync(new Transaction(acc.Id, TransactionType.Deposit, amount, acc.Currency, description), cancellationToken);
            await _audit.LogAsync(acc.OwnerId, "ACCOUNT_CREDIT", "Account", acc.Id.ToString(),
                $"{Fmt(amount)} {acc.Currency}: {description}", cancellationToken);
        }, cancellationToken);

    public async Task<Guid> TransferAsync(Guid fromAccountId, string toIban, decimal amount, string currency,
        string totpCode, CancellationToken cancellationToken = default)
    {
        var transferId = Guid.NewGuid();

        await RunLockedAsync(async () =>
        {
            var from = await _accounts.GetByIdAsync(fromAccountId, cancellationToken)
                ?? throw new TransferException(TransferFailure.NotFound, $"Account {fromAccountId} was not found.");

            var ctx = new TransferContext(from, toIban, amount, currency, totpCode, transferId);
            await _pipeline.RunAsync(ctx, cancellationToken);
        }, cancellationToken);

        return transferId;
    }

    private async Task<IndividualAccount> RequireAsync(Guid id, CancellationToken ct)
        => await _accounts.GetByIdAsync(id, ct) ?? throw new AccountNotFoundException(id);

    private async Task RunLockedAsync(Func<Task> work, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);

        try 
        { 
            await work();
        }
        finally 
        {
            _gate.Release();
        }
    }

    private static string Fmt(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
}
