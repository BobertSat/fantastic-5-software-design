namespace EuroGlobe.Investments.Allocation;

public sealed class SingleAssetAllocationStrategy : IAllocationStrategy
{
    private readonly string _assetSymbol;

    public SingleAssetAllocationStrategy(string assetSymbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetSymbol);

        _assetSymbol = assetSymbol.ToUpperInvariant();
    }

    public IReadOnlyList<AllocationResult> Allocate(decimal amountEur)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amountEur);

        return [new AllocationResult(_assetSymbol, amountEur)];
    }
}