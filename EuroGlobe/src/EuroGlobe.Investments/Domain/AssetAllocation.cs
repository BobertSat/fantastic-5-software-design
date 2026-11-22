namespace EuroGlobe.Investments.Domain;

public sealed record AssetAllocation
{
    public string AssetSymbol { get; }
    public decimal Percent { get; }

    public AssetAllocation(string assetSymbol, decimal percent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetSymbol);

        if (percent <= 0 || percent > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percent), "Percent must be between 0 (exclusive) and 100.");
        }

        AssetSymbol = assetSymbol.ToUpperInvariant();
        Percent = percent;
    }
}
