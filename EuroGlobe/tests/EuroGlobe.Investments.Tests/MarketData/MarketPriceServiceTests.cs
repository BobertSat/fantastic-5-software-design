using EuroGlobe.Investments.Domain;
using EuroGlobe.Investments.MarketData;
using EuroGlobe.Investments.Tests.TestDoubles;

namespace EuroGlobe.Investments.Tests.MarketData;

public class MarketPriceServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeClock _clock = new(Now);
    private readonly InMemoryPriceQuoteStore _store = new();
    private readonly FakePriceSource _crypto = new(AssetType.Crypto, 80000m);

    private MarketPriceService CreateService(params IPriceSource[] sources)
    {
        return new MarketPriceService(new InMemoryAssetCatalog(), sources, _store, _clock);
    }

    [Fact]
    public async Task SourceWorks_ReturnsFreshPriceAndSavesIt()
    {
        var service = CreateService(_crypto);

        var price = await service.GetCurrentPriceAsync("btc");

        Assert.Equal("BTC", price.AssetSymbol);
        Assert.Equal(80000m, price.PriceEur);
        Assert.Equal(Now, price.AsOf);
        Assert.False(price.IsStale);
        Assert.NotNull(await _store.GetLatestAsync("BTC", CancellationToken.None));
    }

    [Fact]
    public async Task SourceFails_ReturnsLastKnownPriceAsStale()
    {
        var service = CreateService(_crypto);
        await service.GetCurrentPriceAsync("BTC");

        _crypto.Fail = true;
        _clock.UtcNow = Now.AddHours(1);

        var price = await service.GetCurrentPriceAsync("BTC");

        Assert.Equal(80000m, price.PriceEur);
        Assert.Equal(Now, price.AsOf);
        Assert.True(price.IsStale);
    }

    [Fact]
    public async Task SourceFails_AndNoStoredPrice_Throws()
    {
        _crypto.Fail = true;
        var service = CreateService(_crypto);

        await Assert.ThrowsAsync<PriceUnavailableException>(() => service.GetCurrentPriceAsync("BTC"));
    }

    [Fact]
    public async Task NoSourceForAssetType_UsesStoredPrice()
    {
        await _store.SaveAsync(new PriceQuote("SPY", 500m, Now, "Earlier"), CancellationToken.None);
        var service = CreateService(_crypto);

        var price = await service.GetCurrentPriceAsync("SPY");

        Assert.Equal(500m, price.PriceEur);
        Assert.True(price.IsStale);
    }

    [Fact]
    public async Task UnknownAsset_Throws()
    {
        var service = CreateService(_crypto);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetCurrentPriceAsync("XYZ"));
    }

    [Fact]
    public async Task CallerCancels_IsNotTreatedAsApiFailure()
    {
        var service = CreateService(_crypto);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetCurrentPriceAsync("BTC", cts.Token));
    }
}