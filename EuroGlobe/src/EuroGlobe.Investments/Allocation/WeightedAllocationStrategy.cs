using EuroGlobe.Investments.Domain;

namespace EuroGlobe.Investments.Allocation;

public sealed class WeightedAllocationStrategy : IAllocationStrategy
{
    private readonly IReadOnlyList<AssetAllocation> _allocations;

    public WeightedAllocationStrategy(IEnumerable<AssetAllocation> allocations)
    {
        ArgumentNullException.ThrowIfNull(allocations);

        var list = allocations.ToList();

        if (list.Count == 0)
        {
            throw new ArgumentException("At least one allocation is required.", nameof(allocations));
        }

        if (list.Sum(a => a.Percent) != 100m)
        {
            throw new ArgumentException("Allocation percentages must add up to 100.", nameof(allocations));
        }

        _allocations = list;
    }

    public IReadOnlyList<AllocationResult> Allocate(decimal amountEur)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amountEur);

        var shares = _allocations.Select(a => new
        {
            a.AssetSymbol,
            a.Percent,
            Amount = Math.Round(amountEur * a.Percent / 100m, 2, MidpointRounding.ToZero)
        }).ToList();

        var remainder = amountEur - shares.Sum(s => s.Amount);

        var largest = shares.OrderByDescending(s => s.Percent).First();

        return shares.Select(s => new AllocationResult(s.AssetSymbol, s == largest ? s.Amount + remainder : s.Amount)).Where(r => r.AmountEur > 0).ToList();
    }
}