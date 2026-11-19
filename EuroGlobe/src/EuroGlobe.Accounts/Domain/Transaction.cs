namespace EuroGlobe.Accounts.Domain;

public enum TransactionType
{
    Deposit,
    Withdrawal,
    CardPayment,
    Transfer,
    RoundUp
}


public class Transaction(Guid accountId, TransactionType type, decimal amount, string currency,
    string description, string? counterpartyIban = null, Guid? reference = null)
{
    public Guid Id { get; } = Guid.NewGuid();
    public Guid AccountId { get; } = accountId;
    public TransactionType Type { get; } = type;
    public decimal Amount { get; } = amount; // Ledger entry. Amount is signed: negative = money out, positive = money in
    public string Currency { get; } = currency;
    public string Description { get; } = description;
    public string? CounterpartyIban { get; } = counterpartyIban;
    public Guid? Reference { get; } = reference;
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
}
