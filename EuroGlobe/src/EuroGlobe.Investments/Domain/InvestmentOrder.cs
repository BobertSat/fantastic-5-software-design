namespace EuroGlobe.Investments.Domain;

public class InvestmentOrder
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string AssetSymbol { get; private set; }
    public decimal AmountEur { get; private set; }
    public decimal PriceEur { get; private set; }
    public decimal Units { get; private set; }
    public OrderSource Source { get; private set; }
    public Guid? SourceTransactionId { get; private set; }
    public DateTimeOffset ExecutedAt { get; private set; }

    private InvestmentOrder(Guid userId, string assetSymbol, decimal amountEur, decimal priceEur, OrderSource source, Guid? sourceTransactionId, DateTimeOffset executedAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }
            
        ArgumentException.ThrowIfNullOrWhiteSpace(assetSymbol);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amountEur);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(priceEur);

        Id = Guid.NewGuid();
        UserId = userId;
        AssetSymbol = assetSymbol.ToUpperInvariant();
        AmountEur = amountEur;
        PriceEur = priceEur;
        Units = Math.Round(amountEur / priceEur, 10);
        Source = source;
        SourceTransactionId = sourceTransactionId;
        ExecutedAt = executedAt;
    }

    public static InvestmentOrder CreateRoundUp(Guid userId, Guid transactionId, string assetSymbol, decimal amountEur, decimal priceEur, DateTimeOffset now)
    {
        if (transactionId == Guid.Empty)
        {
            throw new ArgumentException("Round-up order requires a transaction id.", nameof(transactionId));
        }

        return new InvestmentOrder(userId, assetSymbol, amountEur, priceEur, OrderSource.RoundUp, transactionId, now);
    }

    public static InvestmentOrder CreateManual(Guid userId, string assetSymbol, decimal amountEur, decimal priceEur, DateTimeOffset now)
    {
        return new InvestmentOrder(userId, assetSymbol, amountEur, priceEur, OrderSource.Manual, null, now);
    }
}