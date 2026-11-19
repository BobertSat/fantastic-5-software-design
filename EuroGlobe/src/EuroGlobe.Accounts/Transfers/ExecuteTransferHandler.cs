using System.Globalization;
using EuroGlobe.Accounts.Domain;
using EuroGlobe.Accounts.Repositories;
using EuroGlobe.Contracts.Interfaces;

namespace EuroGlobe.Accounts.Transfers;

public sealed class ExecuteTransferHandler(
    ITransactionRepository ledger,
    IAuditLog audit,
    ISepaGateway sepa) : TransferHandler
{
    private readonly ITransactionRepository _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
    private readonly IAuditLog _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    private readonly ISepaGateway _sepa = sepa ?? throw new ArgumentNullException(nameof(sepa));

    public override async Task HandleAsync(TransferContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        context.From.Debit(context.Amount);

        try
        {
            if (context.To is not null)
            {
                context.To.Credit(context.Amount);
            }
            else
            {
                var sent = await _sepa.SendAsync(context.From.Iban, context.ToIban, context.Amount, context.Currency, cancellationToken);
                if (!sent)
                {
                    throw new TransferException(TransferFailure.Unprocessable,
                        "SEPA gateway rejected the transfer.");
                }
            }
        }
        catch
        {
            // Best-effort compensation on source account if downstream step fails
            try
            {
                context.From.Credit(context.Amount);
            }
            catch
            {
                // Avoid masking the original exception during rollback
            }

            throw;
        }

        var formattedAmount = context.Amount.ToString("0.00", CultureInfo.InvariantCulture);

        await _ledger.AddAsync(
            new Transaction(context.From.Id, TransactionType.Transfer, -context.Amount, context.Currency,
                $"Transfer to {context.ToIban}", context.ToIban, context.TransferId),
            cancellationToken);

        await _audit.LogAsync(
            context.From.OwnerId,
            "TRANSFER_SENT",
            "Account",
            context.From.Id.ToString(),
            $"{formattedAmount} {context.Currency} to {context.ToIban} (transfer {context.TransferId}, {(context.IsExternal ? "external" : "internal")})",
            cancellationToken);

        if (context.To is not null)
        {
            await _ledger.AddAsync(
                new Transaction(context.To.Id, TransactionType.Transfer, context.Amount, context.Currency,
                    $"Transfer from {context.From.Iban}", context.From.Iban, context.TransferId),
                cancellationToken);

            await _audit.LogAsync(
                context.To.OwnerId,
                "TRANSFER_RECEIVED",
                "Account",
                context.To.Id.ToString(),
                $"{formattedAmount} {context.Currency} from {context.From.Iban} (transfer {context.TransferId})",
                cancellationToken);
        }
    }
}
