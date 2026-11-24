using EuroGlobe.Accounts.Domain;
using EuroGlobe.Accounts.Transfers;
using Xunit;

namespace EuroGlobe.Accounts.Tests.Domain;

public class DomainExceptionsTests
{
    [Fact]
    public void AccountNotFound_NamesTheAccount()
    {
        var id = Guid.NewGuid();

        var ex = new AccountNotFoundException(id);

        Assert.Contains(id.ToString(), ex.Message);
        Assert.IsAssignableFrom<DomainException>(ex);
    }

    [Fact]
    public void AccountNotActive_NamesTheAccountAndStatus()
    {
        var id = Guid.NewGuid();

        var ex = new AccountNotActiveException(id, AccountStatus.Blocked);

        Assert.Contains(id.ToString(), ex.Message);
        Assert.Contains("Blocked", ex.Message);
        Assert.IsAssignableFrom<DomainException>(ex);
    }

    [Fact]
    public void CurrencyMismatch_NamesBothCurrencies()
    {
        var ex = new CurrencyMismatchException("EUR", "USD");

        Assert.Contains("EUR", ex.Message);
        Assert.Contains("USD", ex.Message);
        Assert.IsAssignableFrom<DomainException>(ex);
    }

    [Fact]
    public void InsufficientFunds_IsADomainException()
        => Assert.IsAssignableFrom<DomainException>(new InsufficientFundsException(10m, 5m));

    [Fact]
    public void TransferException_CarriesFailureAndMessage()
    {
        var ex = new TransferException(TransferFailure.Forbidden, "Invalid TOTP code.");

        Assert.Equal(TransferFailure.Forbidden, ex.Failure);
        Assert.Equal("Invalid TOTP code.", ex.Message);
        Assert.IsAssignableFrom<DomainException>(ex);
    }
}
