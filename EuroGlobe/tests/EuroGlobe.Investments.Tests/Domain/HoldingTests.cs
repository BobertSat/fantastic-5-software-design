using EuroGlobe.Investments.Domain;

namespace EuroGlobe.Investments.Tests.Domain;

public class HoldingTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    private static InvestmentOrder Order(decimal amountEur, decimal priceEur, string symbol = "BTC", Guid? userId = null)
    {
        return InvestmentOrder.CreateManual(userId ?? UserId, symbol, amountEur, priceEur, Now);
    }

    [Fact]
    public void NewHolding_isEmpty()
    {
        var holding = new Holding(UserId, "btc");

        Assert.Equal("BTC", holding.AssetSymbol);
        Assert.Equal(0m, holding.Units);
        Assert.Equal(0m, holding.CostBasisEur);
        Assert.Equal(0m, holding.AverageBuyPrice);
    }

    [Fact]
    public void Constructor_WithEmptyUserId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Holding(Guid.Empty, "BTC"));
    }

    [Fact]
    public void AddPurchase_AddUnitsAndCost()
    {
        var holding = new Holding(UserId, "btc");

        holding.AddPurchase(Order(100m, 50000m));
        holding.AddPurchase(Order(100m, 100000m));

        Assert.Equal(0.003m, holding.Units);
        Assert.Equal(200m, holding.CostBasisEur);
        Assert.Equal(66666.67m, holding.AverageBuyPrice);
    }

    [Fact]
    public void AddPurchase_WithNullOrder_Throws()
    {
        var holding = new Holding(UserId, "btc");

        Assert.Throws<ArgumentNullException>(() => holding.AddPurchase(null!));
    }

    [Fact]
    public void AddPurchase_FromDifferentUser_Throws()
    {
        var holding = new Holding(UserId, "BTC");

        Assert.Throws<ArgumentException>(() => holding.AddPurchase(Order(10m, 80000m, userId: Guid.NewGuid())));
    }

    [Fact]
    public void AddPurchase_ForDifferentAsset_Throws()
    {
        var holding = new Holding(UserId, "BTC");

        Assert.Throws<ArgumentException>(() => holding.AddPurchase(Order(10m, 500m, symbol: "SPY")));
    }

    [Fact]
    public void Sell_Half_HalvesUnitsAndCost()
    {
        var holding = new Holding(UserId, "BTC");
        holding.AddPurchase(Order(100m, 50000m));

        holding.Sell(0.001m);

        Assert.Equal(0.001m, holding.Units);
        Assert.Equal(50m, holding.CostBasisEur);
    }

    [Fact]
    public void Sell_Everything_ResetsCostToZero()
    {
        var holding = new Holding(UserId, "BTC");
        holding.AddPurchase(Order(100m, 30000m));

        holding.Sell(holding.Units);

        Assert.Equal(0m, holding.Units);
        Assert.Equal(0m, holding.CostBasisEur);
    }

    [Fact]
    public void Sell_MoreThanOwned_Throws()
    {
        var holding = new Holding(UserId, "BTC");
        holding.AddPurchase(Order(100m, 50000m));

        Assert.Throws<InvalidOperationException>(() => holding.Sell(1m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Sell_NonPositiveUnits_Throws(decimal units)
    {
        var holding = new Holding(UserId, "BTC");

        Assert.Throws<ArgumentOutOfRangeException>(() => holding.Sell(units));
    }

    [Fact]
    public void CurrentValue_IsUnitsTimesPrice()
    {
        var holding = new Holding(UserId, "BTC");
        holding.AddPurchase(Order(100m, 50000m));

        Assert.Equal(160m, holding.CurrentValue(80000m));
    }

    [Fact]
    public void CurrentValue_WithNonPositivePrice_Throws()
    {
        var holding = new Holding(UserId, "BTC");

        Assert.Throws<ArgumentOutOfRangeException>(() => holding.CurrentValue(0m));
    }
}