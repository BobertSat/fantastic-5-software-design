using EuroGlobe.Investments.Domain;

namespace EuroGlobe.Investments.MarketData;

public sealed class InMemoryAssetCatalog : IAssetCatalog
{
    private readonly IReadOnlyList<Asset> _assets =
    [
        new Asset("BTC", "Bitcoin", AssetType.Crypto, "EUR", "bitcoin"),
        new Asset("SPY", "S&P 500 ETF", AssetType.Etf, "USD", "spy.us")
    ];

    public IReadOnlyList<Asset> GetAll() => _assets;

    public Asset? Find(string assetSymbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetSymbol);

        var symbol = assetSymbol.ToUpperInvariant();

        return _assets.FirstOrDefault(a => a.Symbol == symbol);
    }
}