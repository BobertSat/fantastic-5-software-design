using EuroGlobe.Investments.Domain;

namespace EuroGlobe.Investments.Tests.Domain;

public class RoundUpSettingsTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void CreateDefault_IsEnabledOneTimesAllBitcoin()
    {
        var settings = RoundUpSettings.CreateDefault(UserId);

        Assert.True(settings.Enabled);
        Assert.Equal(1, settings.Multiplier);
        var allocation = Assert.Single(settings.Allocations);
        Assert.Equal("BTC", allocation.AssetSymbol);
        Assert.Equal(100m, allocation.Percent);
    }

    [Fact]
    public void CreateDefault_WithEmptyUserId_Throws()
    {
        Assert.Throws<ArgumentException>(() => RoundUpSettings.CreateDefault(Guid.Empty));
    }

    [Fact]
    public void DisableAndEnable_ToggleFlag()
    {
        var settings = RoundUpSettings.CreateDefault(UserId);

        settings.Disable();
        Assert.False(settings.Enabled);

        settings.Enable();
        Assert.True(settings.Enabled);
    }

    [Fact]
    public void SetMultiplier_WithValidValue_Updates()
    {
        var settings = RoundUpSettings.CreateDefault(UserId);

        settings.SetMultiplier(3);

        Assert.Equal(3, settings.Multiplier);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void SetMultiplier_OutOfRange_Throws(int multiplier)
    {
        var settings = RoundUpSettings.CreateDefault(UserId);

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetMultiplier(multiplier));
    }

    [Fact]
    public void SetAllocations_WithValidSplit_Replaces()
    {
        var settings = RoundUpSettings.CreateDefault(UserId);

        settings.SetAllocations(new[] { new AssetAllocation("BTC", 80m), new AssetAllocation("SPY", 20m) });

        Assert.Equal(2, settings.Allocations.Count);
        Assert.Equal(20m, settings.Allocations[1].Percent);
    }

    [Fact]
    public void SetAllocations_NotSummingTo100_Throws()
    {
        var settings = RoundUpSettings.CreateDefault(UserId);

        Assert.Throws<ArgumentException>(() =>
            settings.SetAllocations(new[] { new AssetAllocation("BTC", 50m), new AssetAllocation("SPY", 40m) }));
    }

    [Fact]
    public void SetAllocations_WithDuplicateSymbol_Throws()
    {
        var settings = RoundUpSettings.CreateDefault(UserId);

        Assert.Throws<ArgumentException>(() =>
            settings.SetAllocations(new[] { new AssetAllocation("BTC", 50m), new AssetAllocation("btc", 50m) }));
    }

    [Fact]
    public void SetAllocations_Empty_Throws()
    {
        var settings = RoundUpSettings.CreateDefault(UserId);

        Assert.Throws<ArgumentException>(() => settings.SetAllocations(Array.Empty<AssetAllocation>()));
    }

    [Fact]
    public void SetAllocations_Null_Throws()
    {
        var settings = RoundUpSettings.CreateDefault(UserId);

        Assert.Throws<ArgumentNullException>(() => settings.SetAllocations(null!));
    }

    [Fact]
    public void ApplyTo_WhenEnabled_MultipliesRoundUp()
    {
        var settings = RoundUpSettings.CreateDefault(UserId);
        settings.SetMultiplier(2);

        Assert.Equal(1.20m, settings.ApplyTo(0.60m));
    }

    [Fact]
    public void ApplyTo_WhenDisabled_ReturnsZero()
    {
        var settings = RoundUpSettings.CreateDefault(UserId);
        settings.Disable();

        Assert.Equal(0m, settings.ApplyTo(0.60m));
    }

    [Fact]
    public void ApplyTo_NegativeAmount_Throws()
    {
        var settings = RoundUpSettings.CreateDefault(UserId);

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.ApplyTo(-1m));
    }
}