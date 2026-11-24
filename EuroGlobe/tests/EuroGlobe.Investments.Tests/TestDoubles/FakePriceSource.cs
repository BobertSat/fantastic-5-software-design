using EuroGlobe.Investments.Domain;
using EuroGlobe.Investments.MarketData;

namespace EuroGlobe.Investments.Tests.TestDoubles;

internal sealed class FakePriceSource(AssetType handles, decimal price) : IPriceSource
{
    public decimal Price { get; set; } = price;
    public bool Fail { get; set; }
    public int Calls { get; private set; }
    public string Name => "Fake";
    public bool CanHandle(Asset asset) => asset.Type == handles;

    public Task<decimal> GetPriceEurAsync(Asset asset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        if (Fail)
        {
            throw new HttpRequestException("Source down");
        }

        return Task.FromResult(Price);
    }
}