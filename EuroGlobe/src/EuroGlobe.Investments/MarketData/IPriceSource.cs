using EuroGlobe.Investments.Domain;

namespace EuroGlobe.Investments.MarketData;

public interface IPriceSource
{
    string Name { get; }
    bool CanHandle(Asset asset);
    Task<decimal> GetPriceEurAsync(Asset asset, CancellationToken cancellationToken);
}