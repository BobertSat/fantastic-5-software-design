using EuroGlobe.Contracts.Events;
using EuroGlobe.Contracts.Interfaces;
using EuroGlobe.Payments.Transactions;

namespace EuroGlobe.Payments.Services;

public sealed record CardPaymentResult(
    bool Success,
    Guid? TransactionId,
    decimal Amount,
    decimal RoundUpAmount,
    decimal TotalCharged,
    string Currency,
    string? FailureReason = null)
{
    public static CardPaymentResult Failed(string reason, decimal amount = 0, string currency = "")
        => new(false, null, amount, 0, 0, currency, reason);
}

public interface ICardPaymentService
{
    Task<CardPaymentResult> ProcessAsync(Guid cardId, decimal amount, string currency, string merchant,
        CancellationToken cancellationToken = default);
}

public sealed class CardPaymentService(ICardService cards, IAccountService accounts, IEventBus events) : ICardPaymentService
{
    private readonly ICardService _cards = cards;
    private readonly IAccountService _accounts = accounts;
    private readonly IEventBus _events = events;

    public async Task<CardPaymentResult> ProcessAsync(Guid cardId, decimal amount, string currency, string merchant,
        CancellationToken cancellationToken = default)
    {
        RoundUpDecorator transaction;
        try
        {
            transaction = new RoundUpDecorator(new BaseTransaction(amount, currency, $"Card payment: {merchant}"));
        }
        catch (ArgumentException ex)
        {
            return CardPaymentResult.Failed(ex.Message, amount, currency);
        }

        var validation = await _cards.ValidateForPaymentAsync(cardId, transaction.TotalCharge, cancellationToken);
        if (!validation.IsValid)
            return CardPaymentResult.Failed(validation.FailureReason ?? "Card validation failed.", amount, transaction.Currency);

        try
        {
            await _accounts.DebitAsync(validation.AccountId, transaction.TotalCharge, transaction.Description, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CardPaymentResult.Failed(ex.Message, amount, transaction.Currency);
        }

        var transactionId = Guid.NewGuid();

        try
        {
            await _events.PublishAsync(new PaymentCompleted(
                transactionId, validation.UserId, transaction.Amount, transaction.RoundUpAmount, transaction.Currency));
        }
        catch (Exception)
        {
            // Money is already moved; the investment side must never break the payment.
        }

        return new CardPaymentResult(true, transactionId, transaction.Amount, transaction.RoundUpAmount, transaction.TotalCharge, transaction.Currency);
    }
}
