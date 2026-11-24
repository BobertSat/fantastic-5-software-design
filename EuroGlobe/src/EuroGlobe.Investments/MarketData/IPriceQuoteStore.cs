using EuroGlobe.Investments.Domain;

namespace EuroGlobe.Investments.MarketData;

public interface IPriceQuoteStore
{
    Task SaveAsync(PriceQuote quote, CancellationToken cancellationToken);
    Task<PriceQuote?> GetLatestAsync(string assetSymbol, CancellationToken cancellationToken);
}