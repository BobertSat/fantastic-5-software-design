using EuroGlobe.Investments.Domain;
using EuroGlobe.Investments.MarketData;

namespace EuroGlobe.Investments.Tests.MarketData;

public class InMemoryAssetCatalogTests
{
    private readonly InMemoryAssetCatalog _catalog = new();

    [Fact]
    public void GetAll_ReturnsBtcAndSpy()
    {
        var assets = _catalog.GetAll();

        Assert.Equal(2, assets.Count);
        Assert.Contains(assets, a => a.Symbol == "BTC" && a.Type == AssetType.Crypto);
        Assert.Contains(assets, a => a.Symbol == "SPY" && a.Type == AssetType.Etf);
    }

    [Theory]
    [InlineData("BTC")]
    [InlineData("btc")]
    [InlineData("Btc")]
    public void Find_IsCaseInsensitive(string symbol)
    {
        var asset = _catalog.Find(symbol);

        Assert.NotNull(asset);
        Assert.Equal("BTC", asset.Symbol);
    }

    [Fact]
    public void Find_WithUnknownSymbol_ReturnsNull()
    {
        var asset = _catalog.Find("XYZ");

        Assert.Null(asset);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Find_WithBlankSymbol_Throws(string symbol)
    {
        Assert.Throws<ArgumentException>(() => _catalog.Find(symbol));
    }
}