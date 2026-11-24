namespace EuroGlobe.Accounts.Domain;

public class GroupAccount : IAccount
{
    private readonly List<IAccount> _members = [];

    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; }
    public string Currency { get; }
    public IReadOnlyList<IAccount> Members => _members;

    public GroupAccount(string name, string currency)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        Name = name;
        Currency = currency.ToUpperInvariant();
    }

    public void AddAccount(IAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (ReferenceEquals(account, this) || (account is GroupAccount g && g.Contains(this)))
        {
            throw new InvalidOperationException("A group cannot contain itself.");
        }
        if (account.Currency != Currency)
        {
            throw new CurrencyMismatchException(Currency, account.Currency);
        }
        if (_members.Any(m => m.Id == account.Id))
        {
            return;
        }

        _members.Add(account);
    }

    public bool RemoveAccount(IAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return _members.RemoveAll(m => m.Id == account.Id) > 0;
    }

    public decimal CalculateBalance() => _members.Sum(m => m.CalculateBalance());

    public void ApplyFees()
    {
        foreach (var member in _members)
        {
            member.ApplyFees();
        }
    }

    private bool Contains(IAccount target) =>
        _members.Any(m => ReferenceEquals(m, target) || (m is GroupAccount g && g.Contains(target)));
}