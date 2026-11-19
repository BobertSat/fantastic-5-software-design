namespace EuroGlobe.Accounts.Domain;

public class GroupAccount : IAccount
{
    private readonly List<IAccount> _children = [];

    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; }
    public string Currency { get; }
    public IReadOnlyList<IAccount> Children => _children;

    public GroupAccount(string name, string currency)
    {
        if (string.IsNullOrWhiteSpace(name)) 
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        Name = name;
        Currency = currency.ToUpperInvariant();
    }

    public void Add(IAccount child)
    {
        if (ReferenceEquals(child, this))
        {
            throw new InvalidOperationException("A group cannot contain itself.");
        }
        if (child.Currency != Currency)
        {
            throw new CurrencyMismatchException(Currency, child.Currency);
        }
        if (_children.Any(c => c.Id == child.Id))
        {
            return;
        }

        _children.Add(child);
    }

    public bool Remove(Guid childId) => _children.RemoveAll(c => c.Id == childId) > 0;

    public decimal GetBalance() => _children.Sum(c => c.GetBalance());
    public decimal GetAvailableBalance() => _children.Sum(c => c.GetAvailableBalance());
}
