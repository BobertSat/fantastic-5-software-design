namespace EuroGlobe.Investments.MarketData;

public interface IMarketPriceService
{
    Task<MarketPrice> GetCurrentPriceAsync(string assetSymbol, CancellationToken cancellationToken = default);
}