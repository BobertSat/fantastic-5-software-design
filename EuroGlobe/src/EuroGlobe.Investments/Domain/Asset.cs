namespace EuroGlobe.Investments.Domain;

public class Asset
{
    public string Symbol { get; private set; }
    public string Name { get; private set; }
    public AssetType Type { get; private set; }
    public string QuoteCurrency { get; private set; }
    public string ExternalId { get; private set; }

    public Asset(string symbol, string name, AssetType type, string quoteCurrency, string externalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(quoteCurrency);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        Symbol = symbol.ToUpperInvariant();
        Name = name;
        Type = type;
        QuoteCurrency = quoteCurrency.ToUpperInvariant();
        ExternalId = externalId;
    }
}