namespace EuroGlobe.Accounts.Tests.Domain;

using EuroGlobe.Accounts.Domain;
using EuroGlobe.Accounts.Tests.TestDoubles;

public class IndividualAccountTests
{
    private const string TestIban = "LT121000011101001000";
    private const string TestCurrency = "EUR";
    private readonly Guid _testOwnerId = Guid.NewGuid();
    private readonly FakeClock _clock = new();

    [Fact]
    public void Constructor_CreatesAccountWithValidData()
    {
        var account = new IndividualAccount(_testOwnerId, TestIban, TestCurrency, _clock);
        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal(TestCurrency, account.Currency);
    }

    [Fact]
    public void Constructor_ThrowsOnInvalidIban()
    {
        Assert.Throws<ArgumentException>(() => 
            new IndividualAccount(_testOwnerId, "", TestCurrency, _clock));
    }

    [Fact]
    public void Credit_IncreasesBalance()
    {
        var account = new IndividualAccount(_testOwnerId, TestIban, TestCurrency, _clock);
        account.Credit(100m);
        Assert.Equal(100m, account.Balance);
    }

    [Fact]
    public void Debit_DecreasesBalance()
    {
        var account = new IndividualAccount(_testOwnerId, TestIban, TestCurrency, _clock);
        account.Credit(100m);
        account.Debit(30m);
        Assert.Equal(70m, account.Balance);
    }

    [Fact]
    public void Debit_ThrowsWhenInsufficientFunds()
    {
        var account = new IndividualAccount(_testOwnerId, TestIban, TestCurrency, _clock);
        account.Credit(50m);
        Assert.Throws<InsufficientFundsException>(() => account.Debit(100m));
    }

    [Fact]
    public void Reserve_IncreasesReservedAmount()
    {
        var account = new IndividualAccount(_testOwnerId, TestIban, TestCurrency, _clock);
        account.Credit(100m);
        account.Reserve(40m);
        Assert.Equal(40m, account.ReservedAmount);
        Assert.Equal(60m, account.AvailableBalance);
    }

    [Fact]
    public void Release_DecreasesReservedAmount()
    {
        var account = new IndividualAccount(_testOwnerId, TestIban, TestCurrency, _clock);
        account.Credit(100m);
        account.Reserve(60m);
        account.Release(40m);
        Assert.Equal(20m, account.ReservedAmount);
    }

    [Fact]
    public void Block_ChangesStatusToBlocked()
    {
        var account = new IndividualAccount(_testOwnerId, TestIban, TestCurrency, _clock);
        account.Block();
        Assert.Equal(AccountStatus.Blocked, account.Status);
    }

    [Fact]
    public void Unblock_ChangesStatusToActive()
    {
        var account = new IndividualAccount(_testOwnerId, TestIban, TestCurrency, _clock);
        account.Block();
        account.Unblock();
        Assert.Equal(AccountStatus.Active, account.Status);
    }

    [Fact]
    public void Close_ChangesStatusToClosed()
    {
        var account = new IndividualAccount(_testOwnerId, TestIban, TestCurrency, _clock);
        account.Close();
        Assert.Equal(AccountStatus.Closed, account.Status);
    }

    [Fact]
    public void CalculateBalance_ReturnsCurrentBalance()
    {
        var account = new IndividualAccount(_testOwnerId, TestIban, TestCurrency, _clock);
        account.Credit(75m);
        Assert.Equal(75m, account.CalculateBalance());
    }

    [Fact]
    public void ApplyFees_ChargesFeeWhenActive()
    {
        var account = new IndividualAccount(_testOwnerId, TestIban, TestCurrency, _clock, AccountType.Individual, 5m);
        account.Credit(100m);
        account.ApplyFees();
        Assert.Equal(95m, account.Balance);
    }
}
