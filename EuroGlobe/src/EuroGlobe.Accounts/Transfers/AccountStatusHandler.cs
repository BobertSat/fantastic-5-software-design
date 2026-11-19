using EuroGlobe.Accounts.Domain;

namespace EuroGlobe.Accounts.Transfers;

public sealed class AccountStatusHandler : TransferHandler
{
    public override Task HandleAsync(TransferContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.From.Status != AccountStatus.Active)
        {
            throw new TransferException(TransferFailure.Conflict,
                $"Source account is {context.From.Status}.");
        }

        if (!string.Equals(context.Currency, context.From.Currency, StringComparison.Ordinal))
        {
            throw new TransferException(TransferFailure.Unprocessable,
                $"Transfer currency {context.Currency} does not match account currency {context.From.Currency}.");
        }

        return PassOnAsync(context, cancellationToken);
    }
}
