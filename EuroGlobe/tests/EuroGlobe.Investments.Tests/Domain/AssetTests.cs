using EuroGlobe.Investments.Domain;

namespace EuroGlobe.Investments.Tests.Domain;

public class AssetTests
{
    [Fact]
    public void Constructor_SetsValuesAndUppercasesSymbolAndCurrency()
    {
        var asset = new Asset("btc", "Bitcoin", AssetType.Crypto, "eur", "bitcoin");

        Assert.Equal("BTC", asset.Symbol);
        Assert.Equal("Bitcoin", asset.Name);
        Assert.Equal(AssetType.Crypto, asset.Type);
        Assert.Equal("EUR", asset.QuoteCurrency);
        Assert.Equal("bitcoin", asset.ExternalId);
    }

    [Theory]
    [InlineData("", "Bitcoin", "EUR", "bitcoin")]
    [InlineData("BTC", " ", "EUR", "bitcoin")]
    [InlineData("BTC", "Bitcoin", "", "bitcoin")]
    [InlineData("BTC", "Bitcoin", "EUR", "  ")]
    public void Constructor_WithBlankValue_Throws(string symbol, string name, string currency, string externalId)
    {
        Assert.Throws<ArgumentException>(() =>
            new Asset(symbol, name, AssetType.Crypto, currency, externalId));
    }

    [Fact]
    public void Constructor_WithNullSymbol_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Asset(null!, "Bitcoin", AssetType.Crypto, "EUR", "bitcoin"));
    }
}
