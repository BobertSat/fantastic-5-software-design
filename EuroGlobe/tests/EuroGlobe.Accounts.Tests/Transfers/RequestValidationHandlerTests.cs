using System.Globalization;
using EuroGlobe.Accounts.Tests.TestDoubles;
using EuroGlobe.Accounts.Transfers;

namespace EuroGlobe.Accounts.Tests.Transfers;

public class RequestValidationHandlerTests
{
    private readonly FakeClock _clock = new();
    private readonly RequestValidationHandler _handler = new();
    private readonly RecordingHandler _next = new();

    public RequestValidationHandlerTests() => _handler.SetNext(_next);

    [Fact]
    public async Task ValidRequest_PassesToNextHandler()
    {
        var ctx = TestData.NewContext(TestData.NewAccount(_clock, funds: 100m), TestData.ExternalIban);

        await _handler.HandleAsync(ctx, CancellationToken.None);

        Assert.Equal(1, _next.Calls);
    }

    [Fact]
    public async Task ValidRequest_NormalizesIbanCurrencyAndTotp()
    {
        var ctx = TestData.NewContext(TestData.NewAccount(_clock, funds: 100m),
            "de89 3704 0044 0532 0130 00", currency: " eur ", totp: " 123456 ");

        await _handler.HandleAsync(ctx, CancellationToken.None);

        Assert.Equal(TestData.ExternalIban, ctx.ToIban);
        Assert.Equal("EUR", ctx.Currency);
        Assert.Equal("123456", ctx.TotpCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.005")]
    [InlineData("10.123")]
    public async Task InvalidAmount_IsValidationFailure(string raw)
    {
        var amount = decimal.Parse(raw, CultureInfo.InvariantCulture);
        var ctx = TestData.NewContext(TestData.NewAccount(_clock, funds: 100m), TestData.ExternalIban, amount);

        await TestData.AssertTransferFailsAsync(TransferFailure.ValidationFailed,
            () => _handler.HandleAsync(ctx, CancellationToken.None));
        Assert.Equal(0, _next.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("EU")]
    [InlineData("EURO")]
    public async Task InvalidCurrency_IsValidationFailure(string currency)
    {
        var ctx = TestData.NewContext(TestData.NewAccount(_clock, funds: 100m), TestData.ExternalIban, currency: currency);

        await TestData.AssertTransferFailsAsync(TransferFailure.ValidationFailed,
            () => _handler.HandleAsync(ctx, CancellationToken.None));
        Assert.Equal(0, _next.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingTotp_IsValidationFailure(string totp)
    {
        var ctx = TestData.NewContext(TestData.NewAccount(_clock, funds: 100m), TestData.ExternalIban, totp: totp);

        await TestData.AssertTransferFailsAsync(TransferFailure.ValidationFailed,
            () => _handler.HandleAsync(ctx, CancellationToken.None));
        Assert.Equal(0, _next.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-iban")]
    [InlineData("LT000000000000000000")]
    [InlineData("DE89370400440532013001")]
    public async Task InvalidIban_IsValidationFailure(string iban)
    {
        var ctx = TestData.NewContext(TestData.NewAccount(_clock, funds: 100m), iban);

        await TestData.AssertTransferFailsAsync(TransferFailure.ValidationFailed,
            () => _handler.HandleAsync(ctx, CancellationToken.None));
        Assert.Equal(0, _next.Calls);
    }

    [Fact]
    public async Task TransferToOwnIban_IsValidationFailure_EvenInDifferentCase()
    {
        var from = TestData.NewAccount(_clock, funds: 100m);
        var ctx = TestData.NewContext(from, from.Iban.ToLowerInvariant());

        await TestData.AssertTransferFailsAsync(TransferFailure.ValidationFailed,
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
