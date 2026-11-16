namespace EuroGlobe.Contracts.Events;

public sealed record PaymentCompleted(
    Guid TransactionId,
    Guid UserId,
    decimal Amount,
    decimal RoundedAmount,
    string Currency
);
