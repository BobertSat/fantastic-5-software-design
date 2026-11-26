using EuroGlobe.Accounts.Tests.TestDoubles;
using EuroGlobe.Accounts.Transfers;

namespace EuroGlobe.Accounts.Tests.Transfers;

public class AccountStatusHandlerTests
{
    private readonly FakeClock _clock = new();
    private readonly AccountStatusHandler _handler = new();
    private readonly RecordingHandler _next = new();

    public AccountStatusHandlerTests() => _handler.SetNext(_next);

    [Fact]
    public async Task ActiveAccountWithMatchingCurrency_PassesOn()
    {
        var ctx = TestData.NewContext(TestData.NewAccount(_clock, funds: 100m), TestData.ExternalIban);

        await _handler.HandleAsync(ctx, CancellationToken.None);

        Assert.Equal(1, _next.Calls);
    }

    [Fact]
    public async Task BlockedSource_IsConflict()
    {
        var from = TestData.NewAccount(_clock, funds: 100m);
        from.Block();
        var ctx = TestData.NewContext(from, TestData.ExternalIban);

        await TestData.AssertTransferFailsAsync(TransferFailure.Conflict,
            () => _handler.HandleAsync(ctx, CancellationToken.None));
        Assert.Equal(0, _next.Calls);
    }

    [Fact]
    public async Task ClosedSource_IsConflict()
    {
        var from = TestData.NewAccount(_clock);
        from.Close();
        var ctx = TestData.NewContext(from, TestData.ExternalIban);

        await TestData.AssertTransferFailsAsync(TransferFailure.Conflict,
            () => _handler.HandleAsync(ctx, CancellationToken.None));
        Assert.Equal(0, _next.Calls);
    }

    [Fact]
    public async Task CurrencyDifferentFromAccount_IsUnprocessable()
    {
        var ctx = TestData.NewContext(TestData.NewAccount(_clock, "EUR", 100m), TestData.ExternalIban, currency: "USD");

        await TestData.AssertTransferFailsAsync(TransferFailure.Unprocessable,
            () => _handler.HandleAsync(ctx, CancellationToken.None));
        Assert.Equal(0, _next.Calls);
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
