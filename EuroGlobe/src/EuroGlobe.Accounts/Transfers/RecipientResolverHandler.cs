using EuroGlobe.Accounts.Domain;
using EuroGlobe.Accounts.Repositories;

namespace EuroGlobe.Accounts.Transfers;

public sealed class RecipientResolverHandler(IAccountRepository accounts) : TransferHandler
{
    private readonly IAccountRepository _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));

    public override async Task HandleAsync(TransferContext context, CancellationToken cancellationToken)
    {
        var to = await _accounts.GetByIbanAsync(context.ToIban, cancellationToken);
        if (to is not null)
        {
            if (to.Id == context.From.Id)
            {
                throw new TransferException(TransferFailure.ValidationFailed,
                    "Cannot transfer to the same account.");
            }

            if (to.Status != AccountStatus.Active)
            {
                throw new TransferException(TransferFailure.Unprocessable,
                    "Destination account is not active.");
            }

            if (!string.Equals(to.Currency, context.Currency, StringComparison.Ordinal))
            {
                throw new TransferException(TransferFailure.Unprocessable,
                    $"Destination account currency is {to.Currency}; cross-currency transfers are not supported.");
            }

            context.To = to;
        }

        await PassOnAsync(context, cancellationToken);
    }
}
