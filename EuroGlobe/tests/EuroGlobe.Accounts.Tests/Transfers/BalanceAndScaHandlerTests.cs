using System.Globalization;
using EuroGlobe.Accounts.Tests.TestDoubles;
using EuroGlobe.Accounts.Transfers;

namespace EuroGlobe.Accounts.Tests.Transfers;

public class BalanceHandlerTests
{
    private readonly FakeClock _clock = new();
    private readonly BalanceHandler _handler = new();
    private readonly RecordingHandler _next = new();

    public BalanceHandlerTests() => _handler.SetNext(_next);

    [Theory]
    [InlineData("100.00")]
    [InlineData("0.01")]
    public async Task AmountWithinBalance_PassesOn(string raw)
    {
        var amount = decimal.Parse(raw, CultureInfo.InvariantCulture);
        var ctx = TestData.NewContext(TestData.NewAccount(_clock, funds: 100m), TestData.ExternalIban, amount);

        await _handler.HandleAsync(ctx, CancellationToken.None);

        Assert.Equal(1, _next.Calls);
    }

    [Fact]
    public async Task AmountAboveBalance_IsUnprocessable()
    {
        var ctx = TestData.NewContext(TestData.NewAccount(_clock, funds: 100m), TestData.ExternalIban, 100.01m);

        await TestData.AssertTransferFailsAsync(TransferFailure.Unprocessable,
            () => _handler.HandleAsync(ctx, CancellationToken.None));
        Assert.Equal(0, _next.Calls);
    }

    [Fact]
    public async Task ReservedFundsAreNotAvailable()
    {
        var from = TestData.NewAccount(_clock, funds: 100m);
        from.Reserve(40m);

        await TestData.AssertTransferFailsAsync(TransferFailure.Unprocessable,
            () => _handler.HandleAsync(TestData.NewContext(from, TestData.ExternalIban, 61m), CancellationToken.None));

        await _handler.HandleAsync(TestData.NewContext(from, TestData.ExternalIban, 60m), CancellationToken.None);
        Assert.Equal(1, _next.Calls);
    }

    [Fact]
    public async Task CancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var ctx = TestData.NewContext(TestData.NewAccount(_clock, funds: 100m), TestData.ExternalIban);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _handler.HandleAsync(ctx, cts.Token));
    }
}

public class ScaHandlerTests
{
    private readonly FakeClock _clock = new();
    private readonly FakeScaService _sca = new();
    private readonly ScaHandler _handler;
    private readonly RecordingHandler _next = new();

    public ScaHandlerTests()
    {
        _handler = new ScaHandler(_sca);
        _handler.SetNext(_next);
    }

    [Fact]
    public async Task ValidCode_PassesOnAndVerifiesTheAccountOwner()
    {
        var owner = Guid.NewGuid();
        var ctx = TestData.NewContext(TestData.NewAccount(_clock, funds: 100m, ownerId: owner),
            TestData.ExternalIban, totp: "654321");

        await _handler.HandleAsync(ctx, CancellationToken.None);

        Assert.Equal(1, _next.Calls);
        Assert.Equal(owner, _sca.LastUserId);
        Assert.Equal("654321", _sca.LastCode);
    }

    [Fact]
    public async Task InvalidCode_IsForbiddenAndStopsTheChain()
    {
        _sca.Result = false;
        var ctx = TestData.NewContext(TestData.NewAccount(_clock, funds: 100m), TestData.ExternalIban);

        await TestData.AssertTransferFailsAsync(TransferFailure.Forbidden,
            () => _handler.HandleAsync(ctx, CancellationToken.None));
        Assert.Equal(0, _next.Calls);
    }

    [Fact]
    public void Constructor_RejectsNullService()
        => Assert.Throws<ArgumentNullException>(() => new ScaHandler(null!));
}
