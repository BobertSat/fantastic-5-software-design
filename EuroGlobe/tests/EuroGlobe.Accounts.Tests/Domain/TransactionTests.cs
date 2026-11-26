using EuroGlobe.Accounts.Domain;
using EuroGlobe.Accounts.Tests.TestDoubles;

namespace EuroGlobe.Accounts.Tests.Domain;

public class TransactionTests
{
    private readonly FakeClock _clock = new();

    [Fact]
    public void Constructor_StoresAllValues()
    {
        var accountId = Guid.NewGuid();
        var reference = Guid.NewGuid();

        var tx = new Transaction(accountId, TransactionType.Transfer, -12.50m, "EUR",
            "Transfer to DE89", _clock, "DE89370400440532013000", reference);

        Assert.Equal(accountId, tx.AccountId);
        Assert.Equal(TransactionType.Transfer, tx.Type);
        Assert.Equal(-12.50m, tx.Amount);
        Assert.Equal("EUR", tx.Currency);
        Assert.Equal("Transfer to DE89", tx.Description);
        Assert.Equal("DE89370400440532013000", tx.CounterpartyIban);
        Assert.Equal(reference, tx.Reference);
    }

    [Fact]
    public void OptionalFields_DefaultToNull()
    {
        var tx = new Transaction(Guid.NewGuid(), TransactionType.Deposit, 5m, "EUR", "Top-up", _clock);

        Assert.Null(tx.CounterpartyIban);
        Assert.Null(tx.Reference);
    }

    [Fact]
    public void CreatedAt_IsTakenFromTheClockAtCreation()
    {
        var created = _clock.UtcNow;
        var tx = new Transaction(Guid.NewGuid(), TransactionType.Deposit, 5m, "EUR", "Top-up", _clock);

        _clock.UtcNow = _clock.UtcNow.AddDays(30);

        Assert.Equal(created, tx.CreatedAt);
    }

    [Fact]
    public void Id_IsUniquePerTransaction()
    {
        var a = new Transaction(Guid.NewGuid(), TransactionType.Deposit, 5m, "EUR", "a", _clock);
        var b = new Transaction(Guid.NewGuid(), TransactionType.Deposit, 5m, "EUR", "b", _clock);

        Assert.NotEqual(Guid.Empty, a.Id);
        Assert.NotEqual(a.Id, b.Id);
    }
}
