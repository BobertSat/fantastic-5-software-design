namespace EuroGlobe.Accounts.Transfers;

public sealed class BalanceHandler : TransferHandler
{
    public override Task HandleAsync(TransferContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Amount > context.From.AvailableBalance)
        {
            throw new TransferException(TransferFailure.Unprocessable,
                "Insufficient available balance.");
        }

        return PassOnAsync(context, cancellationToken);
    }
}
