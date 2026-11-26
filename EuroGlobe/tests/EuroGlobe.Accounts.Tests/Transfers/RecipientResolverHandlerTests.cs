using EuroGlobe.Accounts.Repositories;
using EuroGlobe.Accounts.Tests.TestDoubles;
using EuroGlobe.Accounts.Transfers;

namespace EuroGlobe.Accounts.Tests.Transfers;

public class RecipientResolverHandlerTests
{
    private readonly FakeClock _clock = new();
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly RecipientResolverHandler _handler;
    private readonly RecordingHandler _next = new();

    public RecipientResolverHandlerTests()
    {
        _handler = new RecipientResolverHandler(_accounts);
        _handler.SetNext(_next);
    }

    [Fact]
    public async Task InternalIban_ResolvesDestinationAccount()
    {
        var from = TestData.NewAccount(_clock, funds: 100m);
        var to = TestData.NewAccount(_clock);
        await _accounts.AddAsync(to);
        var ctx = TestData.NewContext(from, to.Iban);

        await _handler.HandleAsync(ctx, CancellationToken.None);

        Assert.Same(to, ctx.To);
        Assert.False(ctx.IsExternal);
        Assert.Equal(1, _next.Calls);
    }

    [Fact]
    public async Task UnknownIban_IsTreatedAsExternal()
    {
        var ctx = TestData.NewContext(TestData.NewAccount(_clock, funds: 100m), TestData.ExternalIban);

        await _handler.HandleAsync(ctx, CancellationToken.None);

        Assert.Null(ctx.To);
        Assert.True(ctx.IsExternal);
        Assert.Equal(1, _next.Calls);
    }

    [Fact]
    public async Task BlockedDestination_IsUnprocessable()
    {
        var to = TestData.NewAccount(_clock);
        to.Block();
        await _accounts.AddAsync(to);
        var ctx = TestData.NewContext(TestData.NewAccount(_clock, funds: 100m), to.Iban);

        await TestData.AssertTransferFailsAsync(TransferFailure.Unprocessable,
            () => _handler.HandleAsync(ctx, CancellationToken.None));
        Assert.Equal(0, _next.Calls);
    }

    [Fact]
    public async Task DestinationInOtherCurrency_IsUnprocessable()
    {
        var to = TestData.NewAccount(_clock, "USD");
        await _accounts.AddAsync(to);
        var ctx = TestData.NewContext(TestData.NewAccount(_clock, "EUR", 100m), to.Iban, currency: "EUR");

        await TestData.AssertTransferFailsAsync(TransferFailure.Unprocessable,
            () => _handler.HandleAsync(ctx, CancellationToken.None));
        Assert.Equal(0, _next.Calls);
    }

    [Fact]
    public async Task DestinationIsSourceAccount_IsValidationFailure()
    {
        var from = TestData.NewAccount(_clock, funds: 100m);
        await _accounts.AddAsync(from);
        var ctx = TestData.NewContext(from, from.Iban);

        await TestData.AssertTransferFailsAsync(TransferFailure.ValidationFailed,
            () => _handler.HandleAsync(ctx, CancellationToken.None));
        Assert.Equal(0, _next.Calls);
    }

    [Fact]
    public void Constructor_RejectsNullRepository()
        => Assert.Throws<ArgumentNullException>(() => new RecipientResolverHandler(null!));
}
