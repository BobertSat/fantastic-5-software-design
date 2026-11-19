using EuroGlobe.Accounts.Repositories;
using EuroGlobe.Contracts.Interfaces;

namespace EuroGlobe.Accounts.Transfers;

public sealed class TransferPipeline
{
    private readonly TransferHandler _first;

    public TransferPipeline(
        IAccountRepository accounts,
        ITransactionRepository ledger,
        IAuditLog audit,
        IScaService sca,
        ISepaGateway sepa)
    {
        var first = new RequestValidationHandler();
        first.SetNext(new AccountStatusHandler())
             .SetNext(new RecipientResolverHandler(accounts))
             .SetNext(new BalanceHandler())
             .SetNext(new ScaHandler(sca))
             .SetNext(new ExecuteTransferHandler(ledger, audit, sepa));

        _first = first;
    }

    public Task RunAsync(TransferContext ctx, CancellationToken ct = default)
        => _first.HandleAsync(ctx, ct);
}