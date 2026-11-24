namespace EuroGlobe.Investments.Domain;

public class RoundUpSettings
{
    //Min and Max multipliers values are only preferences, there is no documentation what they should be only that MinMultiplier should be 1x
    public const int MinMultiplier = 1;
    public const int MaxMultiplier = 10;
    public const string DefaultAssetSymbol = "BTC";

    private List<AssetAllocation> _allocations = new();

    public Guid UserId { get; private set; }
    public bool Enabled { get; private set; }
    public int Multiplier { get; private set; }
    public IReadOnlyList<AssetAllocation> Allocations => _allocations.AsReadOnly();

    private RoundUpSettings(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        UserId = userId;
    }

    public static RoundUpSettings CreateDefault(Guid userId)
    {
        var settings = new RoundUpSettings(userId)
        {
            Enabled = true,
            Multiplier = MinMultiplier
        };

        settings._allocations.Add(new AssetAllocation(DefaultAssetSymbol, 100m));

        return settings;
    }

    public void Enable()
    {
        Enabled = true;
    }

    public void Disable()
    {
        Enabled = false;
    }

    public void SetMultiplier(int multiplier)
    {
        if (multiplier < MinMultiplier || multiplier > MaxMultiplier)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier), $"Multiplier must be between {MinMultiplier} and {MaxMultiplier}.");
        }

        Multiplier = multiplier;
    }

    public void SetAllocations(IEnumerable<AssetAllocation> allocations)
    {
        ArgumentNullException.ThrowIfNull(allocations);

        var list = allocations.ToList();

        if (list.Count == 0)
        {
            throw new ArgumentException("At least one allocation is required.", nameof(allocations));
        }

        if (list.Select(a => a.AssetSymbol).Distinct().Count() != list.Count)
        {
            throw new ArgumentException("Each asset can appear only once.", nameof(allocations));
        }

        if (list.Sum(a => a.Percent) != 100m)
        {
            throw new ArgumentException("Allocation percentages must add up to 100.", nameof(allocations));
        }

        _allocations = list;
    }

    public decimal ApplyTo(decimal baseRoundUp)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseRoundUp);

        if (!Enabled)
        {
            return 0m;
        }

        return baseRoundUp * Multiplier;
    }
}