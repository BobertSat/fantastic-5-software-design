using EuroGlobe.Investments.Domain;

namespace EuroGlobe.Investments.Tests.Domain;

public class AssetAllocationTests
{
    [Fact]
    public void Constructor_SetsValuesAndUppercasesSymbol()
    {
        var allocation = new AssetAllocation("btc", 80m);

        Assert.Equal("BTC", allocation.AssetSymbol);
        Assert.Equal(80m, allocation.Percent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(100.01)]
    public void Constructor_WithInvalidPercent_Throws(decimal percent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AssetAllocation("BTC", percent));
    }

    [Fact]
    public void Constructor_WithBlankSymbol_Throws()
    {
        Assert.Throws<ArgumentException>(() => new AssetAllocation(" ", 50m));
    }

    [Fact]
    public void SameValues_AreEqual()
    {
        Assert.Equal(new AssetAllocation("BTC", 50m), new AssetAllocation("btc", 50m));
    }
}