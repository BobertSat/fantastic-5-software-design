namespace EuroGlobe.Investments.MarketData;

public sealed class PriceUnavailableException(string assetSymbol) : Exception($"No price is available for {assetSymbol}.")
{
    public string AssetSymbol { get; } = assetSymbol;
}