namespace EuroGlobe.Accounts.Tests.Domain;

using EuroGlobe.Accounts.Domain;
using EuroGlobe.Accounts.Tests.TestDoubles;

public class GroupAccountTests
{
    private const string TestIban = "LT121000011101001000";
    private const string TestCurrency = "EUR";
    private const string AltCurrency = "USD";
    private readonly FakeClock _clock = new();


    private static GroupAccount CreateGroupAccount(string name = "Test Group", string currency = TestCurrency) => new(name, currency);

    private IndividualAccount CreateIndividualAccount(string currency = TestCurrency)
    {
        return new IndividualAccount(Guid.NewGuid(), TestIban, currency, _clock);
    }

    [Fact]
    public void Constructor_CreatesGroupWithValidData()
    {
        var group = new GroupAccount("Test Group", TestCurrency);
        Assert.NotEqual(Guid.Empty, group.Id);
        Assert.Equal("Test Group", group.Name);
        Assert.Equal(TestCurrency, group.Currency);
        Assert.Empty(group.Members);
    }

    [Fact]
    public void Constructor_ThrowsOnEmptyName()
    {
        Assert.Throws<ArgumentException>(() => new GroupAccount("", TestCurrency));
    }

    [Fact]
    public void AddAccount_AddsAccountToGroup()
    {
        var group = CreateGroupAccount();
        var member = CreateIndividualAccount();

        group.AddAccount(member);

        Assert.Single(group.Members);
        Assert.Contains(member, group.Members);
    }

    [Fact]
    public void AddAccount_ThrowsWhenSelfReference()
    {
        var group = CreateGroupAccount();
        Assert.Throws<InvalidOperationException>(() => group.AddAccount(group));
    }

    [Fact]
    public void AddAccount_ThrowsOnCurrencyMismatch()
    {
        var group = CreateGroupAccount("Group", TestCurrency);
        var member = CreateIndividualAccount(AltCurrency);

        Assert.Throws<CurrencyMismatchException>(() => group.AddAccount(member));
    }

    [Fact]
    public void RemoveAccount_RemovesAccountFromGroup()
    {
        var group = CreateGroupAccount();
        var member = CreateIndividualAccount();
        group.AddAccount(member);

        var removed = group.RemoveAccount(member);

        Assert.True(removed);
        Assert.Empty(group.Members);
    }

    [Fact]
    public void RemoveAccount_ReturnsFalseWhenNotFound()
    {
        var group = CreateGroupAccount();
        var member = CreateIndividualAccount();

        var removed = group.RemoveAccount(member);

        Assert.False(removed);
    }

    [Fact]
    public void CalculateBalance_ReturnsSumOfMemberBalances()
    {
        var group = CreateGroupAccount();
        var member1 = CreateIndividualAccount();
        var member2 = CreateIndividualAccount();

        member1.Credit(100m);
        member2.Credit(50m);

        group.AddAccount(member1);
        group.AddAccount(member2);

        Assert.Equal(150m, group.CalculateBalance());
    }

    [Fact]
    public void ApplyFees_AppliesFeesToAllMembers()
    {
        var group = CreateGroupAccount();
        var member = new IndividualAccount(Guid.NewGuid(), TestIban, TestCurrency, _clock, AccountType.Individual, 5m);

        member.Credit(100m);
        group.AddAccount(member);
        group.ApplyFees();

        Assert.Equal(95m, member.Balance);
    }

    [Fact]
    public void NestedGroups_WorkCorrectly()
    {
        var parentGroup = CreateGroupAccount("Parent");
        var childGroup = CreateGroupAccount("Child");
        var member = CreateIndividualAccount();

        member.Credit(100m);
        childGroup.AddAccount(member);
        parentGroup.AddAccount(childGroup);

        Assert.Equal(100m, parentGroup.CalculateBalance());
    }

    [Fact]
    public void AddAccount_ThrowsOnIndirectCycle()
    {
        var outer = CreateGroupAccount("Outer");
        var inner = CreateGroupAccount("Inner");
        outer.AddAccount(inner);

        Assert.Throws<InvalidOperationException>(() => inner.AddAccount(outer));
    }

    [Fact]
    public void AddAccount_IgnoresDuplicate()
    {
        var group = CreateGroupAccount();
        var member = CreateIndividualAccount();

        group.AddAccount(member);
        group.AddAccount(member);

        Assert.Single(group.Members);
    }

    [Fact]
    public void ApplyFees_SkipsMemberThatCannotPay()
    {
        var group = CreateGroupAccount();
        var member = new IndividualAccount(Guid.NewGuid(), TestIban, TestCurrency, _clock, AccountType.Individual, 5m);
        member.Credit(2m);
        group.AddAccount(member);

        group.ApplyFees();

        Assert.Equal(2m, member.Balance);
    }
}
