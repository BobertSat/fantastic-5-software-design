using System.Globalization;
using EuroGlobe.Accounts.Domain;
using EuroGlobe.Accounts.Repositories;
using EuroGlobe.Accounts.Tests.TestDoubles;
using EuroGlobe.Accounts.Transfers;

namespace EuroGlobe.Accounts.Tests.Transfers;

public class ExecuteTransferHandlerTests
{
    private readonly FakeClock _clock = new();
    private readonly InMemoryTransactionRepository _ledger = new();
    private readonly FakeAuditLog _audit = new();
    private readonly MockSepaGateway _sepa = new();
    private readonly ExecuteTransferHandler _handler;

    public ExecuteTransferHandlerTests() => _handler = new ExecuteTransferHandler(_ledger, _audit, _sepa, _clock);

    [Fact]
    public async Task InternalTransfer_MovesMoneyAndWritesBothLedgerSides()
    {
        var from = TestData.NewAccount(_clock, funds: 100m);
        var to = TestData.NewAccount(_clock);
        var ctx = TestData.NewContext(from, to.Iban, 25.50m);
        ctx.To = to;

        await _handler.HandleAsync(ctx, CancellationToken.None);

        Assert.Equal(74.50m, from.Balance);
        Assert.Equal(25.50m, to.Balance);

        var sent = Assert.Single(await _ledger.ListByAccountAsync(from.Id));
        Assert.Equal(TransactionType.Transfer, sent.Type);
        Assert.Equal(-25.50m, sent.Amount);
        Assert.Equal(to.Iban, sent.CounterpartyIban);
        Assert.Equal(ctx.TransferId, sent.Reference);
        Assert.Equal(_clock.UtcNow, sent.CreatedAt);

        var received = Assert.Single(await _ledger.ListByAccountAsync(to.Id));
        Assert.Equal(25.50m, received.Amount);
        Assert.Equal(from.Iban, received.CounterpartyIban);
        Assert.Equal(ctx.TransferId, received.Reference);
    }

    [Fact]
    public async Task InternalTransfer_WritesAuditEntriesForBothOwners()
    {
        var from = TestData.NewAccount(_clock, funds: 100m);
        var to = TestData.NewAccount(_clock);
        var ctx = TestData.NewContext(from, to.Iban, 25.50m);
        ctx.To = to;

        await _handler.HandleAsync(ctx, CancellationToken.None);

        var entries = _audit.Entries;
        Assert.Equal(2, entries.Count);

        var sent = Assert.Single(entries, e => e.Action == "TRANSFER_SENT");
        Assert.Equal(from.OwnerId, sent.UserId);
        Assert.Contains("25.50 EUR", sent.Details);
        Assert.Contains("internal", sent.Details);

        var received = Assert.Single(entries, e => e.Action == "TRANSFER_RECEIVED");
        Assert.Equal(to.OwnerId, received.UserId);
    }

    [Fact]
    public async Task AuditAmount_UsesInvariantCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("lt-LT");
            var from = TestData.NewAccount(_clock, funds: 100m);
            var ctx = TestData.NewContext(from, TestData.ExternalIban, 25.50m);

            await _handler.HandleAsync(ctx, CancellationToken.None);

            var entry = Assert.Single(_audit.Entries);
            Assert.Contains("25.50 EUR", entry.Details);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task ExternalTransfer_DebitsSenderAndWritesOneLedgerAndAuditEntry()
    {
        var from = TestData.NewAccount(_clock, funds: 100m);
        var ctx = TestData.NewContext(from, TestData.ExternalIban, 40m);

        await _handler.HandleAsync(ctx, CancellationToken.None);

        Assert.Equal(60m, from.Balance);
        Assert.Single(await _ledger.ListByAccountAsync(from.Id));

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("TRANSFER_SENT", entry.Action);
        Assert.Contains("external", entry.Details);
    }

    [Fact]
    public async Task RejectedSepaTransfer_RollsBackAndWritesNothing()
    {
        var from = TestData.NewAccount(_clock, funds: 100m);
        _sepa.RejectRule = iban => iban == TestData.ExternalIban;
        var ctx = TestData.NewContext(from, TestData.ExternalIban, 40m);

        await TestData.AssertTransferFailsAsync(TransferFailure.Unprocessable,
            () => _handler.HandleAsync(ctx, CancellationToken.None));

        Assert.Equal(100m, from.Balance);
        Assert.Empty(await _ledger.ListByAccountAsync(from.Id));
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task GatewayException_IsRethrownAfterRollback()
    {
        var from = TestData.NewAccount(_clock, funds: 100m);
        var handler = new ExecuteTransferHandler(_ledger, _audit, new ThrowingSepaGateway(), _clock);
        var ctx = TestData.NewContext(from, TestData.ExternalIban, 40m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(ctx, CancellationToken.None));

        Assert.Equal(100m, from.Balance);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task CancelledToken_ThrowsBeforeMovingMoney()
    {
        var from = TestData.NewAccount(_clock, funds: 100m);
        var ctx = TestData.NewContext(from, TestData.ExternalIban, 40m);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _handler.HandleAsync(ctx, cts.Token));

        Assert.Equal(100m, from.Balance);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new ExecuteTransferHandler(null!, _audit, _sepa, _clock));
        Assert.Throws<ArgumentNullException>(() => new ExecuteTransferHandler(_ledger, null!, _sepa, _clock));
        Assert.Throws<ArgumentNullException>(() => new ExecuteTransferHandler(_ledger, _audit, null!, _clock));
        Assert.Throws<ArgumentNullException>(() => new ExecuteTransferHandler(_ledger, _audit, _sepa, null!));
    }
}
