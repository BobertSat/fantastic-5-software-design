namespace EuroGlobe.Investments.MarketData;

public sealed record MarketPrice(string AssetSymbol, decimal PriceEur, DateTimeOffset AsOf, string Source, bool IsStale);