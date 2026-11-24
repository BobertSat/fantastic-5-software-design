using EuroGlobe.Investments.Domain;

namespace EuroGlobe.Investments.Tests.Domain;

public class InvestmentPortfolioTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    private static InvestmentOrder Order(string symbol, decimal amountEur, decimal priceEur, Guid? userId = null)
    {
        return InvestmentOrder.CreateManual(userId ?? UserId, symbol, amountEur, priceEur, Now);
    }

    [Fact]
    public void NewPortfolio_IsEmpty()
    {
        var portfolio = new InvestmentPortfolio(UserId);

        Assert.Empty(portfolio.Holdings);
        Assert.Equal(0m, portfolio.TotalInvestedEur);
    }

    [Fact]
    public void Constructor_WithEmptyUserId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new InvestmentPortfolio(Guid.Empty));
    }

    [Fact]
    public void ApplyOrder_FirstOrder_CreatesHolding()
    {
        var portfolio = new InvestmentPortfolio(UserId);

        portfolio.ApplyOrder(Order("BTC", 100m, 50000m));

        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal("BTC", holding.AssetSymbol);
        Assert.Equal(0.002m, holding.Units);
    }

    [Fact]
    public void ApplyOrder_SameAssetTwice_AddsToSameHolding()
    {
        var portfolio = new InvestmentPortfolio(UserId);

        portfolio.ApplyOrder(Order("BTC", 100m, 50000m));
        portfolio.ApplyOrder(Order("BTC", 100m, 50000m));

        var holding = Assert.Single(portfolio.Holdings);
        Assert.Equal(0.004m, holding.Units);
        Assert.Equal(200m, portfolio.TotalInvestedEur);
    }

    [Fact]
    public void ApplyOrder_DifferentAssets_CreatesSeparateHoldings()
    {
        var portfolio = new InvestmentPortfolio(UserId);

        portfolio.ApplyOrder(Order("BTC", 100m, 50000m));
        portfolio.ApplyOrder(Order("SPY", 50m, 500m));

        Assert.Equal(2, portfolio.Holdings.Count);
        Assert.Equal(150m, portfolio.TotalInvestedEur);
    }

    [Fact]
    public void ApplyOrder_FromDifferentUser_Throws()
    {
        var portfolio = new InvestmentPortfolio(UserId);

        Assert.Throws<ArgumentException>(() => portfolio.ApplyOrder(Order("BTC", 10m, 50000m, Guid.NewGuid())));
    }

    [Fact]
    public void ApplyOrder_Null_Throws()
    {
        var portfolio = new InvestmentPortfolio(UserId);

        Assert.Throws<ArgumentNullException>(() => portfolio.ApplyOrder(null!));
    }

    [Fact]
    public void FindHolding_IsCaseInsensitive()
    {
        var portfolio = new InvestmentPortfolio(UserId);
        portfolio.ApplyOrder(Order("BTC", 100m, 50000m));

        Assert.NotNull(portfolio.FindHolding("btc"));
    }

    [Fact]
    public void FindHolding_WhenNotOwned_ReturnsNull()
    {
        var portfolio = new InvestmentPortfolio(UserId);

        Assert.Null(portfolio.FindHolding("SPY"));
    }

    [Fact]
    public void Sell_ReducesHoldingUnits()
    {
        var portfolio = new InvestmentPortfolio(UserId);
        portfolio.ApplyOrder(Order("BTC", 100m, 50000m));

        portfolio.Sell("BTC", 0.001m);

        Assert.Equal(0.001m, portfolio.FindHolding("BTC")!.Units);
        Assert.Equal(50m, portfolio.TotalInvestedEur);
    }

    [Fact]
    public void Sell_AssetNotOwned_Throws()
    {
        var portfolio = new InvestmentPortfolio(UserId);

        Assert.Throws<InvalidOperationException>(() => portfolio.Sell("SPY", 1m));
    }
}