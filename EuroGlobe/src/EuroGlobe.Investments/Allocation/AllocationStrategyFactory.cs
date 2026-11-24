using EuroGlobe.Investments.Domain;

namespace EuroGlobe.Investments.Allocation;

public static class AllocationStrategyFactory
{
    public static IAllocationStrategy For(RoundUpSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.Allocations.Count == 1)
        {
            return new SingleAssetAllocationStrategy(settings.Allocations[0].AssetSymbol);
        }

        return new WeightedAllocationStrategy(settings.Allocations);
    }
}