namespace EuroGlobe.Investments.Domain;

public class PriceQuote
{
    public Guid Id { get; private set; }
    public string AssetSymbol { get; private set; }
    public decimal PriceEur { get; private set; }
    public DateTimeOffset FetchedAt { get; private set; }
    public string Source { get; private set; }

    public PriceQuote(string assetSymbol, decimal priceEur, DateTimeOffset fetchedAt, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetSymbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(priceEur);

        Id = Guid.NewGuid();
        AssetSymbol = assetSymbol.ToUpperInvariant();
        PriceEur = priceEur;
        FetchedAt = fetchedAt;
        Source = source;
    }

    public bool IsStale(DateTimeOffset now, TimeSpan maxAge)
    {
        return now - FetchedAt > maxAge;
    }
}