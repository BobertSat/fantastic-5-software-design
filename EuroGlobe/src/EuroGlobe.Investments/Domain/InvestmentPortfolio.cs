namespace EuroGlobe.Investments.Domain;

public class InvestmentPortfolio
{
    private readonly List<Holding> _holdings = new();

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public IReadOnlyList<Holding> Holdings => _holdings.AsReadOnly();

    public decimal TotalInvestedEur => _holdings.Sum(h => h.CostBasisEur);

    public InvestmentPortfolio(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        Id = Guid.NewGuid();
        UserId = userId;
    }

    public Holding? FindHolding(string assetSymbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetSymbol);

        var symbol = assetSymbol.ToUpperInvariant();

        return _holdings.FirstOrDefault(h => h.AssetSymbol == symbol);
    }

    public void ApplyOrder(InvestmentOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (order.UserId != UserId)
        {
            throw new ArgumentException("Order belongs to a different user.", nameof(order));
        }

        var holding = FindHolding(order.AssetSymbol);

        if (holding is null)
        {
            holding = new Holding(UserId, order.AssetSymbol);
            _holdings.Add(holding);
        }

        holding.AddPurchase(order);
    }

    public void Sell(string assetSymbol, decimal units)
    {
        var holding = FindHolding(assetSymbol);

        if (holding is null)
        {
            throw new InvalidOperationException($"No holding for asset {assetSymbol}.");
        }

        holding.Sell(units);
    }
}