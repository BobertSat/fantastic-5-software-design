using EuroGlobe.Accounts.Domain;
using EuroGlobe.Accounts.Repositories;
using EuroGlobe.Accounts.Tests.TestDoubles;
using EuroGlobe.Accounts.Transfers;

namespace EuroGlobe.Accounts.Tests.Transfers;

public class TransferPipelineTests
{
    private readonly FakeClock _clock = new();
    private readonly InMemoryAccountRepository _accounts = new();
    private readonly InMemoryTransactionRepository _ledger = new();
    private readonly FakeAuditLog _audit = new();
    private readonly FakeScaService _sca = new();
    private readonly MockSepaGateway _sepa = new();
    private readonly TransferPipeline _pipeline;

    public TransferPipelineTests()
        => _pipeline = new TransferPipeline(_accounts, _ledger, _audit, _sca, _sepa, _clock);

    private async Task<IndividualAccount> AddAccountAsync(decimal funds = 0m, string currency = "EUR")
    {
        var account = TestData.NewAccount(_clock, currency, funds);
        await _accounts.AddAsync(account);
        return account;
    }

    [Fact]
    public async Task InternalTransfer_RunsTheWholeChain()
    {
        var from = await AddAccountAsync(100m);
        var to = await AddAccountAsync();
        var ctx = TestData.NewContext(from, to.Iban, 30m);

        await _pipeline.RunAsync(ctx);

        Assert.Equal(70m, from.Balance);
        Assert.Equal(30m, to.Balance);
        Assert.Equal(1, _sca.Calls);
    }

    [Fact]
    public async Task ExternalTransfer_GoesThroughTheSepaGateway()
    {
        var from = await AddAccountAsync(100m);
        var ctx = TestData.NewContext(from, TestData.ExternalIban, 30m);

        await _pipeline.RunAsync(ctx);

        Assert.Equal(70m, from.Balance);
        Assert.True(ctx.IsExternal);
    }

    [Fact]
    public async Task LowercaseIbanWithSpaces_StillResolvesTheInternalAccount()
    {
        var from = await AddAccountAsync(100m);
        var to = await AddAccountAsync();
        var spaced = string.Join(' ', Enumerable.Range(0, 5).Select(i => to.Iban.Substring(i * 4, 4))).ToLowerInvariant();
        var ctx = TestData.NewContext(from, spaced, 30m);

        await _pipeline.RunAsync(ctx);

        Assert.Equal(30m, to.Balance);
        Assert.False(ctx.IsExternal);
    }

    [Fact]
    public async Task ValidationRunsBeforeScaCheck()
    {
        var from = await AddAccountAsync(100m);
        _sca.Result = false;
        var ctx = TestData.NewContext(from, "not-an-iban");

        await TestData.AssertTransferFailsAsync(TransferFailure.ValidationFailed, () => _pipeline.RunAsync(ctx));
        Assert.Equal(0, _sca.Calls);
    }

    [Fact]
    public async Task BlockedAccountIsRejectedBeforeScaCheck()
    {
        var from = await AddAccountAsync(100m);
        from.Block();
        _sca.Result = false;
        var ctx = TestData.NewContext(from, TestData.ExternalIban);

        await TestData.AssertTransferFailsAsync(TransferFailure.Conflict, () => _pipeline.RunAsync(ctx));
        Assert.Equal(0, _sca.Calls);
    }

    [Fact]
    public async Task InsufficientFundsIsRejectedBeforeScaCheck()
    {
        var from = await AddAccountAsync(5m);
        _sca.Result = false;
        var ctx = TestData.NewContext(from, TestData.ExternalIban, 10m);

        await TestData.AssertTransferFailsAsync(TransferFailure.Unprocessable, () => _pipeline.RunAsync(ctx));
        Assert.Equal(0, _sca.Calls);
    }

    [Fact]
    public async Task WrongTotp_IsForbiddenAndNothingMoves()
    {
        var from = await AddAccountAsync(100m);
        var to = await AddAccountAsync();
        _sca.Result = false;
        var ctx = TestData.NewContext(from, to.Iban, 30m);

        await TestData.AssertTransferFailsAsync(TransferFailure.Forbidden, () => _pipeline.RunAsync(ctx));

        Assert.Equal(100m, from.Balance);
        Assert.Equal(0m, to.Balance);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task DestinationInOtherCurrency_LeavesFundsUntouched()
    {
        var from = await AddAccountAsync(100m, "EUR");
        var to = await AddAccountAsync(0m, "USD");
        var ctx = TestData.NewContext(from, to.Iban, 30m);

        await TestData.AssertTransferFailsAsync(TransferFailure.Unprocessable, () => _pipeline.RunAsync(ctx));

        Assert.Equal(100m, from.Balance);
    }

    [Fact]
    public async Task RejectedSepaTransfer_RestoresBalance()
    {
        var from = await AddAccountAsync(100m);
        _sepa.RejectRule = _ => true;
        var ctx = TestData.NewContext(from, TestData.ExternalIban, 30m);

        await TestData.AssertTransferFailsAsync(TransferFailure.Unprocessable, () => _pipeline.RunAsync(ctx));

        Assert.Equal(100m, from.Balance);
    }
}
