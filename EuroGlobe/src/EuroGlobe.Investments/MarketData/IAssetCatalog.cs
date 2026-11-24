using EuroGlobe.Investments.Domain;

namespace EuroGlobe.Investments.MarketData;

public interface IAssetCatalog
{
    IReadOnlyList<Asset> GetAll();
    Asset? Find(string assetSymbol);
}