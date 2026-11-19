using EuroGlobe.Investments.Domain;

namespace EuroGlobe.Investments.Tests.Domain;

public class PortfolioSnapshotTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 19);

    [Fact]
    public void Constructor_SetsValues()
    {
        var snapshot = new PortfolioSnapshot(UserId, Today, 1420.80m, 1244.40m);

        Assert.NotEqual(Guid.Empty, snapshot.Id);
        Assert.Equal(UserId, snapshot.UserId);
        Assert.Equal(Today, snapshot.Date);
        Assert.Equal(1420.80m, snapshot.ValueEur);
        Assert.Equal(1244.40m, snapshot.InvestedEur);
    }

    [Fact]
    public void Profit_And_Yield_AreCalculated()
    {
        var snapshot = new PortfolioSnapshot(UserId, Today, 1420.80m, 1244.40m);

        Assert.Equal(176.40m, snapshot.ProfitEur);
        Assert.Equal(14.18m, snapshot.YieldPercent);
    }

    [Fact]
    public void Loss_GivesNegativeYield()
    {
        var snapshot = new PortfolioSnapshot(UserId, Today, 90m, 100m);

        Assert.Equal(-10m, snapshot.ProfitEur);
        Assert.Equal(-10m, snapshot.YieldPercent);
    }

    [Fact]
    public void EmptyPortfolio_HasZeroYield()
    {
        var snapshot = new PortfolioSnapshot(UserId, Today, 0m, 0m);

        Assert.Equal(0m, snapshot.YieldPercent);
    }

    [Fact]
    public void Constructor_WithEmptyUserId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PortfolioSnapshot(Guid.Empty, Today, 0m, 0m));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void Constructor_WithNegativeAmount_Throws(decimal value, decimal invested)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PortfolioSnapshot(UserId, Today, value, invested));
    }
}