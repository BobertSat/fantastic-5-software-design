using EuroGlobe.Accounts.Domain;
using System.Security.Principal;

namespace EuroGlobe.Accounts.Transfers;

public enum TransferFailure
{
    ValidationFailed,
    Forbidden,
    NotFound,
    Conflict,
    Unprocessable
}

public sealed class TransferException : DomainException
{
    public TransferFailure Failure { get; }

    public TransferException(TransferFailure failure, string message) : base(message)
        => Failure = failure;
}

public sealed class TransferContext(
    IndividualAccount from,
    string toIban,
    decimal amount,
    string currency,
    string totpCode,
    Guid transferId)
{
    public IndividualAccount From { get; } = from ?? throw new ArgumentNullException(nameof(from));
    public string ToIban { get; set; } = toIban ?? string.Empty;
    public decimal Amount { get; } = amount;
    public string Currency { get; set; } = currency ?? string.Empty;
    public string TotpCode { get; set; } = totpCode ?? string.Empty;
    public Guid TransferId { get; } = transferId;
    public IndividualAccount? To { get; set; }
    public bool IsExternal => To is null;
}

public interface ISepaGateway
{
    Task<bool> SendAsync(
        string fromIban,
        string toIban,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default);
}
