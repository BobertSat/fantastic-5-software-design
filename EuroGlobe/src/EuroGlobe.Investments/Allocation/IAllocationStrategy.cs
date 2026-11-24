namespace EuroGlobe.Investments.Allocation;

public interface IAllocationStrategy
{
    IReadOnlyList<AllocationResult> Allocate(decimal amountEur);
}