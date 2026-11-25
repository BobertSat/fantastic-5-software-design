using EuroGlobe.Investments.Allocation;
using EuroGlobe.Investments.Domain;

namespace EuroGlobe.Investments.Tests.Allocation;

public class AllocationStrategyTests
{
    private static WeightedAllocationStrategy Weighted(params (string Symbol, decimal Percent)[] parts)
    {
        return new WeightedAllocationStrategy(parts.Select(p => new AssetAllocation(p.Symbol, p.Percent)));
    }

    [Fact]
    public void Single_ReturnsWholeAmountForAsset()
    {
        var strategy = new SingleAssetAllocationStrategy("btc");

        var result = Assert.Single(strategy.Allocate(0.60m));

        Assert.Equal("BTC", result.AssetSymbol);
        Assert.Equal(0.60m, result.AmountEur);
    }

    [Fact]
    public void Single_WithBlankSymbol_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SingleAssetAllocationStrategy(" "));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Single_NonPositiveAmount_Throws(decimal amount)
    {
        var strategy = new SingleAssetAllocationStrategy("BTC");

        Assert.Throws<ArgumentOutOfRangeException>(() => strategy.Allocate(amount));
    }

    [Fact]
    public void Weighted_80_20_SplitsAmount()
    {
        var result = Weighted(("BTC", 80m), ("SPY", 20m)).Allocate(0.60m);

        Assert.Equal(2, result.Count);
        Assert.Equal(0.48m, result.Single(r => r.AssetSymbol == "BTC").AmountEur);
        Assert.Equal(0.12m, result.Single(r => r.AssetSymbol == "SPY").AmountEur);
    }

    [Fact]
    public void Weighted_RemainderGoesToLargestShare_AndSumMatches()
    {
        var result = Weighted(("BTC", 33.33m), ("SPY", 33.33m), ("ETH", 33.34m)).Allocate(0.10m);

        Assert.Equal(0.10m, result.Sum(r => r.AmountEur));
        Assert.Equal(0.04m, result.Single(r => r.AssetSymbol == "ETH").AmountEur);
    }

    [Fact]
    public void Weighted_TinyAmount_SkipsZeroShares()
    {
        var result = Weighted(("BTC", 50m), ("SPY", 50m)).Allocate(0.01m);

        var only = Assert.Single(result);
        Assert.Equal(0.01m, only.AmountEur);
    }

    [Fact]
    public void Weighted_NotSummingTo100_Throws()
    {
        Assert.Throws<ArgumentException>(() => Weighted(("BTC", 50m), ("SPY", 40m)));
    }

    [Fact]
    public void Weighted_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => new WeightedAllocationStrategy(Array.Empty<AssetAllocation>()));
    }

    [Fact]
    public void Weighted_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new WeightedAllocationStrategy(null!));
    }

    [Fact]
    public void Weighted_NonPositiveAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Weighted(("BTC", 100m)).Allocate(0m));
    }

    [Fact]
    public void Factory_SingleAllocation_ReturnsSingleStrategy()
    {
        var settings = RoundUpSettings.CreateDefault(Guid.NewGuid());

        Assert.IsType<SingleAssetAllocationStrategy>(AllocationStrategyFactory.For(settings));
    }

    [Fact]
    public void Factory_MultipleAllocations_ReturnsWeightedStrategy()
    {
        var settings = RoundUpSettings.CreateDefault(Guid.NewGuid());
        settings.SetAllocations(new[] { new AssetAllocation("BTC", 80m), new AssetAllocation("SPY", 20m) });

        Assert.IsType<WeightedAllocationStrategy>(AllocationStrategyFactory.For(settings));
    }

    [Fact]
    public void Factory_NullSettings_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AllocationStrategyFactory.For(null!));
    }
}