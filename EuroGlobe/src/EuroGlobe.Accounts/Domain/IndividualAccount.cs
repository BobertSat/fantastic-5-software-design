namespace EuroGlobe.Accounts.Domain;

public class IndividualAccount : IAccount
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OwnerId { get; private set; }
    public string Iban { get; private set; }
    public string Currency { get; private set; }
    public AccountType Type { get; private set; }
    public decimal Balance { get; private set; }
    public decimal ReservedAmount { get; private set; }
    public AccountStatus Status { get; private set; } = AccountStatus.Active;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public decimal AvailableBalance => Balance - ReservedAmount;

    public IndividualAccount(Guid ownerId, string iban, string currency, AccountType type = AccountType.Individual)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            throw new ArgumentException("IBAN is required.", nameof(iban));
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));
        }

        OwnerId = ownerId;
        Iban = iban;
        Currency = currency.ToUpperInvariant();
        Type = type;
    }

    public decimal GetBalance() => Balance;
    public decimal GetAvailableBalance() => AvailableBalance;

    public void Credit(decimal amount)
    {
        ValidateAmount(amount);
        EnsureActive();

        Balance += amount;
    }

    public void Debit(decimal amount)
    {
        ValidateAmount(amount);
        EnsureActive();

        if (amount > AvailableBalance) 
        {
            throw new InsufficientFundsException(amount, AvailableBalance);
        }

        Balance -= amount;
    }

    public void Reserve(decimal amount)
    {
        ValidateAmount(amount);
        EnsureActive();

        if (amount > AvailableBalance)
        {
            throw new InsufficientFundsException(amount, AvailableBalance);
        }

        ReservedAmount += amount;
    }

    public void Release(decimal amount)
    {
        ValidateAmount(amount);

        if (amount > ReservedAmount)
        {
            throw new InvalidOperationException("Cannot release more than is reserved.");
        }
            
        ReservedAmount -= amount;
    }

    public void Block() 
    {
        if (Status == AccountStatus.Active)
        {
            Status = AccountStatus.Blocked;
        }
    }

    public void Unblock()
    {
        if (Status == AccountStatus.Blocked)
        {
            Status = AccountStatus.Active;
        }
    }

    public void Close()
    {
        if (Balance != 0 || ReservedAmount != 0)
        {
            throw new InvalidOperationException("Only an empty account can be closed.");
        }

        Status = AccountStatus.Closed;
    }

    private void EnsureActive()
    {
        if (Status != AccountStatus.Active)
        {
            throw new AccountNotActiveException(Id, Status);
        }
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException("Amount cannot have more than 2 decimal places.", nameof(amount));
        }
    }
}
