using EuroGlobe.Investments.Domain;

namespace EuroGlobe.Investments.Tests.Domain;

public class InvestmentOrderTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TransactionId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateRoundUp_CalculatesUnitsFromAmountAndPrice()
    {
        var order = InvestmentOrder.CreateRoundUp(UserId, TransactionId, "BTC", 0.60m, 80000m, Now);

        Assert.Equal(0.0000075m, order.Units);
        Assert.Equal(OrderSource.RoundUp, order.Source);
        Assert.Equal(TransactionId, order.SourceTransactionId);
        Assert.Equal(Now, order.ExecutedAt);
    }

    [Fact]
    public void CreateRoundUp_WithEmptyTransactionId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            InvestmentOrder.CreateRoundUp(UserId, Guid.Empty, "BTC", 0.60m, 80000m, Now));
    }

    [Fact]
    public void CreateManual_HasNoTransactionId()
    {
        var order = InvestmentOrder.CreateManual(UserId, "SPY", 50m, 500m, Now);

        Assert.Equal(OrderSource.Manual, order.Source);
        Assert.Null(order.SourceTransactionId);
        Assert.Equal(0.1m, order.Units);
    }

    [Fact]
    public void Create_UppercasesSymbol()
    {
        var order = InvestmentOrder.CreateManual(UserId, "btc", 10m, 80000m, Now);

        Assert.Equal("BTC", order.AssetSymbol);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_WithNonPositiveAmount_Throws(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InvestmentOrder.CreateManual(UserId, "BTC", amount, 80000m, Now));
    }

    [Fact]
    public void Create_WithNonPositivePrice_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InvestmentOrder.CreateManual(UserId, "BTC", 10m, 0m, Now));
    }

    [Fact]
    public void Create_WithEmptyUserId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            InvestmentOrder.CreateManual(Guid.Empty, "BTC", 10m, 80000m, Now));
    }

    [Fact]
    public void Create_WithBlankSymbol_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            InvestmentOrder.CreateManual(UserId, "   ", 10m, 80000m, Now));
    }
}
