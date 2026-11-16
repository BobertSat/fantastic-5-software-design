namespace EuroGlobe.Contracts.Interfaces;

public sealed record CardValidationResult(bool IsValid, Guid AccountId, Guid UserId, string? FailureReason = null);

public interface ICardService
{
    Task<CardValidationResult> ValidateForPaymentAsync(Guid cardId, decimal amount, CancellationToken cancellationToken = default);
}