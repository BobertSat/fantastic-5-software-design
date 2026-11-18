using EuroGlobe.Investments.Domain;

namespace EuroGlobe.Investments.Tests.Domain;

public class PriceQuoteTests
{
    private static readonly DateTimeOffset FetchedAt = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_SetsValuesAndUppercasesSymbol()
    {
        var quote = new PriceQuote("btc", 80000m, FetchedAt, "CoinGecko");

        Assert.NotEqual(Guid.Empty, quote.Id);
        Assert.Equal("BTC", quote.AssetSymbol);
        Assert.Equal(80000m, quote.PriceEur);
        Assert.Equal(FetchedAt, quote.FetchedAt);
        Assert.Equal("CoinGecko", quote.Source);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositivePrice_Throws(decimal price)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PriceQuote("BTC", price, FetchedAt, "CoinGecko"));
    }

    [Fact]
    public void Constructor_WithBlankSource_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new PriceQuote("BTC", 80000m, FetchedAt, ""));
    }

    [Fact]
    public void IsStale_WhenOlderThanMaxAge_ReturnsTrue()
    {
        var quote = new PriceQuote("BTC", 80000m, FetchedAt, "CoinGecko");

        var stale = quote.IsStale(FetchedAt.AddMinutes(16), TimeSpan.FromMinutes(15));

        Assert.True(stale);
    }

    [Fact]
    public void IsStale_WhenFresh_ReturnsFalse()
    {
        var quote = new PriceQuote("BTC", 80000m, FetchedAt, "CoinGecko");

        var stale = quote.IsStale(FetchedAt.AddMinutes(5), TimeSpan.FromMinutes(15));

        Assert.False(stale);
    }
}
