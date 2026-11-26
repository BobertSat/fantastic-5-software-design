using EuroGlobe.Accounts.Domain;
using Xunit;

namespace EuroGlobe.Accounts.Tests.Services;

public class AccountManagementServiceTests
{
    private readonly ServiceEnv _env = new();

    [Fact]
    public async Task Create_ReturnsAnActiveAccountWithAValidLithuanianIban()
    {
        var owner = Guid.NewGuid();

        var account = await _env.Management.CreateAsync(owner, "eur");

        Assert.Equal(owner, account.OwnerId);
        Assert.Equal("EUR", account.Currency);
        Assert.Equal(AccountType.Individual, account.Type);
        Assert.Equal(AccountStatus.Active, account.Status);
        Assert.Equal(0m, account.Balance);
        Assert.StartsWith("LT", account.Iban);
        Assert.True(IbanGenerator.IsValid(account.Iban));
    }

    [Fact]
    public async Task Create_TakesCreatedAtFromTheClock()
    {
        _env.Clock.UtcNow = new DateTimeOffset(2030, 5, 1, 8, 0, 0, TimeSpan.Zero);

        var account = await _env.Management.CreateAsync(Guid.NewGuid(), "EUR");

        Assert.Equal(_env.Clock.UtcNow, account.CreatedAt);
    }

    [Fact]
    public async Task Create_StoresTheAccount()
    {
        var account = await _env.Management.CreateAsync(Guid.NewGuid(), "EUR");

        var loaded = await _env.Management.GetAsync(account.Id);

        Assert.NotNull(loaded);
        Assert.Equal(account.Iban, loaded.Iban);
    }

    [Fact]
    public async Task Create_WritesAnAuditEntry()
    {
        var owner = Guid.NewGuid();

        var account = await _env.Management.CreateAsync(owner, "EUR", AccountType.Group);

        var audit = Assert.Single(_env.Audit.Entries);
        Assert.Equal("ACCOUNT_CREATED", audit.Action);
        Assert.Equal(owner, audit.UserId);
        Assert.Equal(account.Id.ToString(), audit.EntityId);
        Assert.Contains("Group", audit.Details);
        Assert.Contains("EUR", audit.Details);
        Assert.Contains(account.Iban, audit.Details);
    }

    [Fact]
    public async Task Create_HonoursTheRequestedType()
    {
        var account = await _env.Management.CreateAsync(Guid.NewGuid(), "EUR", AccountType.Group);

        Assert.Equal(AccountType.Group, account.Type);
    }

    [Fact]
    public async Task Create_GivesEveryAccountADistinctIban()
    {
        var owner = Guid.NewGuid();
        var accounts = new List<IndividualAccount>();
        for (var i = 0; i < 25; i++)
        {
            accounts.Add(await _env.Management.CreateAsync(owner, "EUR"));
        }

        Assert.Equal(25, accounts.Select(a => a.Iban).Distinct().Count());
    }

    [Theory]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("")]
    public async Task Create_WithInvalidCurrency_ThrowsAndStoresNothing(string currency)
    {
        var owner = Guid.NewGuid();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _env.Management.CreateAsync(owner, currency));

        Assert.Empty(await _env.Management.ListByUserAsync(owner));
        Assert.Empty(_env.Audit.Entries);
    }

    [Fact]
    public async Task Get_UnknownAccount_ReturnsNull()
        => Assert.Null(await _env.Management.GetAsync(Guid.NewGuid()));

    [Fact]
    public async Task ListByUser_ReturnsOnlyThatUsersAccounts()
    {
        var owner = Guid.NewGuid();
        var mine1 = await _env.Management.CreateAsync(owner, "EUR");
        var mine2 = await _env.Management.CreateAsync(owner, "USD");
        await _env.Management.CreateAsync(Guid.NewGuid(), "EUR");

        var list = await _env.Management.ListByUserAsync(owner);

        Assert.Equal(2, list.Count);
        Assert.Contains(list, a => a.Id == mine1.Id);
        Assert.Contains(list, a => a.Id == mine2.Id);
    }

    [Fact]
    public async Task ListByUser_ForUserWithoutAccounts_IsEmpty()
        => Assert.Empty(await _env.Management.ListByUserAsync(Guid.NewGuid()));
}
