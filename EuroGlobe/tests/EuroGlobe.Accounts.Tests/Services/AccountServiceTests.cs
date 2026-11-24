using System.Globalization;
using EuroGlobe.Accounts.Domain;
using EuroGlobe.Accounts.Services;
using EuroGlobe.Accounts.Transfers;
using Xunit;

namespace EuroGlobe.Accounts.Tests.Services;

public class AccountServiceTests
{
    private readonly ServiceEnv _env = new();
    private AccountService Service => _env.Service;

    // ---- lookups ----

    [Fact]
    public async Task GetBalance_ReturnsBalance_AndAvailableBalanceSubtractsReserved()
    {
        var acc = await _env.NewAccountAsync(100m);
        await Service.ReserveAsync(acc.Id, 30m);

        Assert.Equal(100m, await Service.GetBalanceAsync(acc.Id));
        Assert.Equal(70m, await Service.GetAvailableBalanceAsync(acc.Id));
    }

    [Fact]
    public async Task UnknownAccount_ThrowsNotFoundForEveryOperation()
    {
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<AccountNotFoundException>(() => Service.GetBalanceAsync(id));
        await Assert.ThrowsAsync<AccountNotFoundException>(() => Service.GetAvailableBalanceAsync(id));
        await Assert.ThrowsAsync<AccountNotFoundException>(() => Service.ReserveAsync(id, 1m));
        await Assert.ThrowsAsync<AccountNotFoundException>(() => Service.ReleaseAsync(id, 1m));
        await Assert.ThrowsAsync<AccountNotFoundException>(() => Service.DebitAsync(id, 1m, "x"));
        await Assert.ThrowsAsync<AccountNotFoundException>(() => Service.CreditAsync(id, 1m, "x"));
    }

    [Fact]
    public async Task GetAccounts_ReturnsOnlyTheUsersAccounts()
    {
        var owner = Guid.NewGuid();
        var mine1 = await _env.NewAccountAsync(ownerId: owner);
        var mine2 = await _env.NewAccountAsync(ownerId: owner);
        await _env.NewAccountAsync();

        var result = (await Service.GetAccountsAsync(owner)).Cast<object>().ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains((object)mine1, result);
        Assert.Contains((object)mine2, result);
    }

    [Fact]
    public async Task GetAccounts_ForUserWithoutAccounts_IsEmpty()
        => Assert.Empty(await Service.GetAccountsAsync(Guid.NewGuid()));

    // ---- credit / debit ----

    [Fact]
    public async Task Credit_IncreasesBalance_AndWritesLedgerAndAudit()
    {
        var acc = await _env.NewAccountAsync(100m);

        await Service.CreditAsync(acc.Id, 25.50m, "salary");

        Assert.Equal(125.50m, await Service.GetBalanceAsync(acc.Id));

        var entry = Assert.Single(await _env.Ledger.ListByAccountAsync(acc.Id));
        Assert.Equal(TransactionType.Deposit, entry.Type);
        Assert.Equal(25.50m, entry.Amount);
        Assert.Equal("EUR", entry.Currency);
        Assert.Equal("salary", entry.Description);
        Assert.Equal(_env.Clock.UtcNow, entry.CreatedAt);

        var audit = Assert.Single(_env.Audit.Entries);
        Assert.Equal("ACCOUNT_CREDIT", audit.Action);
        Assert.Equal(acc.OwnerId, audit.UserId);
        Assert.Equal("Account", audit.EntityType);
        Assert.Equal(acc.Id.ToString(), audit.EntityId);
        Assert.Equal("25.50 EUR: salary", audit.Details);
    }

    [Fact]
    public async Task Debit_DecreasesBalance_AndWritesNegativeLedgerEntryAndAudit()
    {
        var acc = await _env.NewAccountAsync(100m);

        await Service.DebitAsync(acc.Id, 30m, "coffee");

        Assert.Equal(70m, await Service.GetBalanceAsync(acc.Id));

        var entry = Assert.Single(await _env.Ledger.ListByAccountAsync(acc.Id));
        Assert.Equal(TransactionType.Withdrawal, entry.Type);
        Assert.Equal(-30m, entry.Amount);
        Assert.Equal("coffee", entry.Description);

        var audit = Assert.Single(_env.Audit.Entries);
        Assert.Equal("ACCOUNT_DEBIT", audit.Action);
        Assert.Equal("30.00 EUR: coffee", audit.Details);
    }

    [Fact]
    public async Task Debit_WithInsufficientFunds_ThrowsAndWritesNothing()
    {
        var acc = await _env.NewAccountAsync(10m);

        await Assert.ThrowsAsync<InsufficientFundsException>(() => Service.DebitAsync(acc.Id, 10.01m, "too much"));

        Assert.Equal(10m, await Service.GetBalanceAsync(acc.Id));
        Assert.Empty(await _env.Ledger.ListByAccountAsync(acc.Id));
        Assert.Empty(_env.Audit.Entries);
    }

    [Fact]
    public async Task BlockedAccount_RejectsCreditAndDebit_WithoutWritingAnything()
    {
        var acc = await _env.NewAccountAsync(100m);
        acc.Block();

        await Assert.ThrowsAsync<AccountNotActiveException>(() => Service.CreditAsync(acc.Id, 1m, "x"));
        await Assert.ThrowsAsync<AccountNotActiveException>(() => Service.DebitAsync(acc.Id, 1m, "x"));

        Assert.Empty(await _env.Ledger.ListByAccountAsync(acc.Id));
        Assert.Empty(_env.Audit.Entries);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.234")]
    public async Task InvalidAmounts_AreRejected_WithoutWritingAnything(string raw)
    {
        var amount = decimal.Parse(raw, CultureInfo.InvariantCulture);
        var acc = await _env.NewAccountAsync(100m);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => Service.CreditAsync(acc.Id, amount, "x"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => Service.DebitAsync(acc.Id, amount, "x"));

        Assert.Equal(100m, await Service.GetBalanceAsync(acc.Id));
        Assert.Empty(await _env.Ledger.ListByAccountAsync(acc.Id));
        Assert.Empty(_env.Audit.Entries);
    }

    [Fact]
    public async Task LedgerTimestamps_FollowTheSimulatedClock()
    {
        var acc = await _env.NewAccountAsync(100m);
        _env.Clock.UtcNow = _env.Clock.UtcNow.AddDays(30);

        await Service.DebitAsync(acc.Id, 1m, "later");

        var entry = Assert.Single(await _env.Ledger.ListByAccountAsync(acc.Id));
        Assert.Equal(_env.Clock.UtcNow, entry.CreatedAt);
    }

    // ---- reserve / release ----

    [Fact]
    public async Task Reserve_ReducesAvailableBalanceOnly_AndIsAudited()
    {
        var acc = await _env.NewAccountAsync(100m);

        await Service.ReserveAsync(acc.Id, 40m);

        Assert.Equal(100m, await Service.GetBalanceAsync(acc.Id));
        Assert.Equal(60m, await Service.GetAvailableBalanceAsync(acc.Id));
        Assert.Empty(await _env.Ledger.ListByAccountAsync(acc.Id));

        var audit = Assert.Single(_env.Audit.Entries);
        Assert.Equal("ACCOUNT_RESERVE", audit.Action);
        Assert.Equal("40.00 EUR", audit.Details);
    }

    [Fact]
    public async Task Reserve_CannotExceedAvailableBalance()
    {
        var acc = await _env.NewAccountAsync(100m);
        await Service.ReserveAsync(acc.Id, 60m);

        await Assert.ThrowsAsync<InsufficientFundsException>(() => Service.ReserveAsync(acc.Id, 40.01m));
    }

    [Fact]
    public async Task Release_RestoresAvailableBalance_AndIsAudited()
    {
        var acc = await _env.NewAccountAsync(100m);
        await Service.ReserveAsync(acc.Id, 40m);

        await Service.ReleaseAsync(acc.Id, 40m);

        Assert.Equal(100m, await Service.GetAvailableBalanceAsync(acc.Id));
        Assert.Contains(_env.Audit.Entries, e => e.Action == "ACCOUNT_RELEASE" && e.Details == "40.00 EUR");
    }

    [Fact]
    public async Task Release_CannotExceedReservedAmount()
    {
        var acc = await _env.NewAccountAsync(100m);
        await Service.ReserveAsync(acc.Id, 10m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.ReleaseAsync(acc.Id, 10.01m));
    }

    // ---- locking and cancellation ----

    [Fact]
    public async Task FailedOperation_DoesNotLeaveTheLockHeld()
    {
        var acc = await _env.NewAccountAsync(10m);

        await Assert.ThrowsAsync<InsufficientFundsException>(() => Service.DebitAsync(acc.Id, 50m, "too much"));
        await Service.CreditAsync(acc.Id, 5m, "ok").WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(15m, await Service.GetBalanceAsync(acc.Id));
    }

    [Fact]
    public async Task CancelledToken_ThrowsAndChangesNothing()
    {
        var acc = await _env.NewAccountAsync(100m);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service.DebitAsync(acc.Id, 10m, "x", cts.Token));

        Assert.Equal(100m, await Service.GetBalanceAsync(acc.Id));
        Assert.Empty(_env.Audit.Entries);
    }

    [Fact]
    public async Task ParallelDebits_NeverOverdraw()
    {
        var acc = await _env.NewAccountAsync(100m);

        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
        {
            try
            {
                await Service.DebitAsync(acc.Id, 10m, "parallel");
                return true;
            }
            catch (InsufficientFundsException)
            {
                return false;
            }
        })));

        Assert.Equal(10, results.Count(ok => ok));
        Assert.Equal(0m, await Service.GetBalanceAsync(acc.Id));
        Assert.Equal(10, (await _env.Ledger.ListByAccountAsync(acc.Id)).Count);
    }

    [Fact]
    public async Task ParallelCreditsAndDebits_KeepTheBalanceConsistent()
    {
        var acc = await _env.NewAccountAsync(100m);

        var credits = Enumerable.Range(0, 100).Select(_ => Task.Run(() => Service.CreditAsync(acc.Id, 1m, "in")));
        var debits = Enumerable.Range(0, 100).Select(_ => Task.Run(() => Service.DebitAsync(acc.Id, 1m, "out")));
        await Task.WhenAll(credits.Concat(debits));

        Assert.Equal(100m, await Service.GetBalanceAsync(acc.Id));
        Assert.Equal(200, (await _env.Ledger.ListByAccountAsync(acc.Id, take: int.MaxValue)).Count);
    }

    // ---- transfers ----

    [Fact]
    public async Task InternalTransfer_MovesMoney_AndLinksBothLedgerEntriesToTheReturnedId()
    {
        var from = await _env.NewAccountAsync(100m);
        var to = await _env.NewAccountAsync();

        var id = await Service.TransferAsync(from.Id, to.Iban, 25.50m, "EUR", "123456");

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(74.50m, await Service.GetBalanceAsync(from.Id));
        Assert.Equal(25.50m, await Service.GetBalanceAsync(to.Id));
        Assert.Equal(id, Assert.Single(await _env.Ledger.ListByAccountAsync(from.Id)).Reference);
        Assert.Equal(id, Assert.Single(await _env.Ledger.ListByAccountAsync(to.Id)).Reference);
        Assert.Contains(_env.Audit.Entries, e => e.Action == "TRANSFER_SENT");
        Assert.Contains(_env.Audit.Entries, e => e.Action == "TRANSFER_RECEIVED");
    }

    [Fact]
    public async Task EachTransfer_GetsItsOwnId()
    {
        var from = await _env.NewAccountAsync(100m);
        var to = await _env.NewAccountAsync();

        var first = await Service.TransferAsync(from.Id, to.Iban, 1m, "EUR", "123456");
        var second = await Service.TransferAsync(from.Id, to.Iban, 1m, "EUR", "123456");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task ExternalTransfer_DebitsOnlyTheSender()
    {
        var from = await _env.NewAccountAsync(100m);

        await Service.TransferAsync(from.Id, TestData.ExternalIban, 40m, "EUR", "123456");

        Assert.Equal(60m, await Service.GetBalanceAsync(from.Id));
        Assert.Single(await _env.Ledger.ListByAccountAsync(from.Id));
    }

    [Fact]
    public async Task WrongTotp_IsForbidden_AndNothingMoves()
    {
        var from = await _env.NewAccountAsync(100m);
        var to = await _env.NewAccountAsync();
        _env.Sca.Result = false;

        await TestData.AssertTransferFailsAsync(TransferFailure.Forbidden,
            () => Service.TransferAsync(from.Id, to.Iban, 10m, "EUR", "000000"));

        Assert.Equal(100m, await Service.GetBalanceAsync(from.Id));
        Assert.Equal(0m, await Service.GetBalanceAsync(to.Id));
        Assert.Empty(_env.Audit.Entries);
    }

    [Fact]
    public async Task UnknownSourceAccount_IsNotFound()
    {
        var to = await _env.NewAccountAsync();

        await TestData.AssertTransferFailsAsync(TransferFailure.NotFound,
            () => Service.TransferAsync(Guid.NewGuid(), to.Iban, 10m, "EUR", "123456"));
    }

    [Fact]
    public async Task InvalidTransferRequest_IsValidationFailure()
    {
        var from = await _env.NewAccountAsync(100m);

        await TestData.AssertTransferFailsAsync(TransferFailure.ValidationFailed,
            () => Service.TransferAsync(from.Id, TestData.ExternalIban, 0m, "EUR", "123456"));
    }

    [Fact]
    public async Task InsufficientFunds_IsUnprocessable()
    {
        var from = await _env.NewAccountAsync(5m);

        await TestData.AssertTransferFailsAsync(TransferFailure.Unprocessable,
            () => Service.TransferAsync(from.Id, TestData.ExternalIban, 10m, "EUR", "123456"));
    }

    [Fact]
    public async Task RejectedSepaTransfer_RestoresTheBalance()
    {
        var from = await _env.NewAccountAsync(100m);
        _env.Sepa.RejectRule = _ => true;

        await TestData.AssertTransferFailsAsync(TransferFailure.Unprocessable,
            () => Service.TransferAsync(from.Id, TestData.ExternalIban, 40m, "EUR", "123456"));

        Assert.Equal(100m, await Service.GetBalanceAsync(from.Id));
    }

    [Fact]
    public async Task FailedTransfer_DoesNotBlockLaterOperations()
    {
        var from = await _env.NewAccountAsync(100m);
        _env.Sca.Result = false;

        await Assert.ThrowsAsync<TransferException>(
            () => Service.TransferAsync(from.Id, TestData.ExternalIban, 10m, "EUR", "000000"));
        await Service.CreditAsync(from.Id, 1m, "ok").WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(101m, await Service.GetBalanceAsync(from.Id));
    }

    [Fact]
    public async Task ParallelTransfers_NeverOverdraw()
    {
        var from = await _env.NewAccountAsync(100m);
        var to = await _env.NewAccountAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
        {
            try
            {
                await Service.TransferAsync(from.Id, to.Iban, 10m, "EUR", "123456");
                return true;
            }
            catch (TransferException)
            {
                return false;
            }
        })));

        Assert.Equal(10, results.Count(ok => ok));
        Assert.Equal(0m, await Service.GetBalanceAsync(from.Id));
        Assert.Equal(100m, await Service.GetBalanceAsync(to.Id));
    }

    [Fact]
    public async Task ParallelTransfersInBothDirections_ConserveTheTotal()
    {
        var a = await _env.NewAccountAsync(100m);
        var b = await _env.NewAccountAsync(100m);

        var forward = Enumerable.Range(0, 50).Select(_ => Task.Run(() => Service.TransferAsync(a.Id, b.Iban, 1m, "EUR", "123456")));
        var backward = Enumerable.Range(0, 50).Select(_ => Task.Run(() => Service.TransferAsync(b.Id, a.Iban, 1m, "EUR", "123456")));
        await Task.WhenAll(forward.Concat(backward));

        var balanceA = await Service.GetBalanceAsync(a.Id);
        var balanceB = await Service.GetBalanceAsync(b.Id);
        Assert.Equal(200m, balanceA + balanceB);
        Assert.Equal(100m, balanceA);
        Assert.Equal(100m, balanceB);
    }
}
