namespace EuroGlobe.Investments.Domain;

public class Holding
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string AssetSymbol { get; private set; }
    public decimal Units { get; private set; }
    public decimal CostBasisEur { get; private set; }

    public decimal AverageBuyPrice => Units == 0 ? 0 : Math.Round(CostBasisEur / Units, 2);

    public Holding(Guid userId, string assetSymbol)
    {
        if(userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(assetSymbol);

        Id = Guid.NewGuid();
        UserId = userId;
        AssetSymbol = assetSymbol.ToUpperInvariant();
    }

    public void AddPurchase(InvestmentOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        if(order.UserId != UserId)
        {
            throw new ArgumentException("Order belongs to a different user.", nameof(order));
        }

        if(order.AssetSymbol != AssetSymbol)
        {
            throw new ArgumentException("Order is for a different asset.", nameof(order));
        }
        
        Units += order.Units;
        CostBasisEur += order.AmountEur;
    }

    public void Sell(decimal units)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(units);

        if(units > Units)
        {
            throw new InvalidOperationException("Cannot sell more unit than owned.");
        }

        var costReduction = Math.Round(CostBasisEur * units / Units, 2);

        Units -= units;
        CostBasisEur -= costReduction;

        if(Units == 0)
        {
            CostBasisEur = 0;
        }
    }

    public decimal CurrentValue(decimal priceEur)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(priceEur);
        return Math.Round(Units * priceEur, 2);
    }
}
