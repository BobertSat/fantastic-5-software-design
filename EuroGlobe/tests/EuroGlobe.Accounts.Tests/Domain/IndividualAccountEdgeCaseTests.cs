using System.Globalization;
using EuroGlobe.Accounts.Domain;
using EuroGlobe.Accounts.Tests.TestDoubles;

namespace EuroGlobe.Accounts.Tests.Domain;

public class IndividualAccountEdgeCaseTests
{
    private readonly FakeClock _clock = new();

    private IndividualAccount Account(decimal funds = 0m, decimal fee = 0m)
    {
        var account = new IndividualAccount(Guid.NewGuid(), IbanGenerator.Generate(), "EUR", _clock,
            AccountType.Individual, fee);
        if (funds > 0)
        {
            account.Credit(funds);
        }

        return account;
    }

    // ---- constructor ----

    [Fact]
    public void Constructor_SetsDefaults()
    {
        var owner = Guid.NewGuid();

        var account = new IndividualAccount(owner, "LT121000011101001000", "eur", _clock);

        Assert.Equal(owner, account.OwnerId);
        Assert.Equal("LT121000011101001000", account.Iban);
        Assert.Equal("EUR", account.Currency);
        Assert.Equal(AccountType.Individual, account.Type);
        Assert.Equal(0m, account.MonthlyFee);
        Assert.Equal(0m, account.Balance);
        Assert.Equal(0m, account.ReservedAmount);
        Assert.Equal(AccountStatus.Active, account.Status);
    }

    [Fact]
    public void Constructor_TakesCreatedAtFromClock_AndKeepsIt()
    {
        var created = _clock.UtcNow;
        var account = Account();

        _clock.UtcNow = _clock.UtcNow.AddDays(1);

        Assert.Equal(created, account.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("EU")]
    [InlineData("EURO")]
    public void Constructor_RejectsInvalidCurrency(string currency)
        => Assert.Throws<ArgumentException>(() =>
            new IndividualAccount(Guid.NewGuid(), "LT121000011101001000", currency, _clock));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsMissingIban(string iban)
        => Assert.Throws<ArgumentException>(() =>
            new IndividualAccount(Guid.NewGuid(), iban, "EUR", _clock));

    [Fact]
    public void Constructor_RejectsNegativeFee()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IndividualAccount(Guid.NewGuid(), "LT121000011101001000", "EUR", _clock, AccountType.Individual, -1m));

    // ---- amount validation ----

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("10.123")]
    public void Operations_RejectInvalidAmounts(string raw)
    {
        var amount = decimal.Parse(raw, CultureInfo.InvariantCulture);
        var account = Account(100m);
        account.Reserve(50m);

        Assert.ThrowsAny<ArgumentException>(() => account.Credit(amount));
        Assert.ThrowsAny<ArgumentException>(() => account.Debit(amount));
        Assert.ThrowsAny<ArgumentException>(() => account.Reserve(amount));
        Assert.ThrowsAny<ArgumentException>(() => account.Release(amount));

        Assert.Equal(100m, account.Balance);
        Assert.Equal(50m, account.ReservedAmount);
    }

    // ---- funds ----

    [Fact]
    public void Debit_CanUseExactlyTheAvailableBalance()
    {
        var account = Account(100m);
        account.Reserve(40m);

        account.Debit(60m);

        Assert.Equal(40m, account.Balance);
        Assert.Equal(0m, account.AvailableBalance);
    }

    [Fact]
    public void Debit_CannotTouchReservedFunds()
    {
        var account = Account(100m);
        account.Reserve(40m);

        Assert.Throws<InsufficientFundsException>(() => account.Debit(60.01m));
        Assert.Equal(100m, account.Balance);
    }

    [Fact]
    public void Reserve_CannotExceedAvailableBalance()
    {
        var account = Account(100m);
        account.Reserve(60m);

        Assert.Throws<InsufficientFundsException>(() => account.Reserve(40.01m));
        account.Reserve(40m);
        Assert.Equal(100m, account.ReservedAmount);
    }

    [Fact]
    public void Release_CannotExceedReservedAmount()
    {
        var account = Account(100m);
        account.Reserve(10m);

        Assert.Throws<InvalidOperationException>(() => account.Release(10.01m));
        Assert.Equal(10m, account.ReservedAmount);
    }

    // ---- status ----

    [Fact]
    public void BlockedAccount_RejectsCreditDebitAndReserve()
    {
        var account = Account(100m);
        account.Block();

        Assert.Throws<AccountNotActiveException>(() => account.Credit(1m));
        Assert.Throws<AccountNotActiveException>(() => account.Debit(1m));
        Assert.Throws<AccountNotActiveException>(() => account.Reserve(1m));
    }

    [Fact]
    public void BlockedAccount_StillAllowsReleasingReservedFunds()
    {
        var account = Account(100m);
        account.Reserve(10m);
        account.Block();

        account.Release(10m);

        Assert.Equal(0m, account.ReservedAmount);
    }

    [Fact]
    public void ClosedAccount_RejectsOperations()
    {
        var account = Account();
        account.Close();

        Assert.Throws<AccountNotActiveException>(() => account.Credit(1m));
    }

    [Fact]
    public void Close_RequiresEmptyAccount()
    {
        var account = Account(10m);

        Assert.Throws<InvalidOperationException>(() => account.Close());

        account.Debit(10m);
        account.Close();
        Assert.Equal(AccountStatus.Closed, account.Status);
    }

    [Fact]
    public void BlockAndUnblock_DoNotReopenAClosedAccount()
    {
        var account = Account();
        account.Close();

        account.Block();
        Assert.Equal(AccountStatus.Closed, account.Status);

        account.Unblock();
        Assert.Equal(AccountStatus.Closed, account.Status);
    }

    [Fact]
    public void Unblock_OnActiveAccount_ChangesNothing()
    {
        var account = Account();

        account.Unblock();

        Assert.Equal(AccountStatus.Active, account.Status);
    }

    // ---- fees ----

    [Fact]
    public void ApplyFees_CanTakeTheLastOfTheAvailableBalance()
    {
        var account = Account(5m, fee: 5m);

        account.ApplyFees();

        Assert.Equal(0m, account.Balance);
    }

    [Fact]
    public void ApplyFees_SkipsWhenFundsAreInsufficient()
    {
        var account = Account(4m, fee: 5m);

        account.ApplyFees();

        Assert.Equal(4m, account.Balance);
    }

    [Fact]
    public void ApplyFees_IgnoresReservedFunds()
    {
        var account = Account(10m, fee: 5m);
        account.Reserve(8m);

        account.ApplyFees();

        Assert.Equal(10m, account.Balance);
    }

    [Fact]
    public void ApplyFees_SkipsBlockedAccount()
    {
        var account = Account(100m, fee: 5m);
        account.Block();

        account.ApplyFees();

        Assert.Equal(100m, account.Balance);
    }

    [Fact]
    public void ApplyFees_DoesNothingWithoutAFee()
    {
        var account = Account(100m);

        account.ApplyFees();

        Assert.Equal(100m, account.Balance);
    }
}
