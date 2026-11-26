using EuroGlobe.Accounts.Domain;

namespace EuroGlobe.Accounts.Transfers;

public sealed class RequestValidationHandler : TransferHandler
{
    public override Task HandleAsync(TransferContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Amount <= 0 || decimal.Round(context.Amount, 2) != context.Amount)
        {
            throw new TransferException(TransferFailure.ValidationFailed,
                "Amount must be positive with at most 2 decimal places.");
        }

        if (string.IsNullOrWhiteSpace(context.Currency) || context.Currency.Trim().Length != 3)
        {
            throw new TransferException(TransferFailure.ValidationFailed,
                "Currency must be a 3-letter ISO code.");
        }

        if (string.IsNullOrWhiteSpace(context.TotpCode))
        {
            throw new TransferException(TransferFailure.ValidationFailed,
                "TOTP code is required.");
        }

        if (string.IsNullOrWhiteSpace(context.ToIban) || !IbanGenerator.IsValid(context.ToIban))
        {
            throw new TransferException(TransferFailure.ValidationFailed,
                "Destination IBAN is invalid.");
        }

        context.ToIban = context.ToIban.Replace(" ", string.Empty).ToUpperInvariant();
        context.Currency = context.Currency.Trim().ToUpperInvariant();
        context.TotpCode = context.TotpCode.Trim();

        if (string.Equals(context.ToIban, context.From.Iban, StringComparison.OrdinalIgnoreCase))
        {
            throw new TransferException(TransferFailure.ValidationFailed,
                "Cannot transfer to the same account.");
        }

        return PassOnAsync(context, cancellationToken);
    }
}
