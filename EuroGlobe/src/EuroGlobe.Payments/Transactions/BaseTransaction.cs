namespace EuroGlobe.Payments.Transactions;

public class BaseTransaction : ITransaction
{
    public string Currency { get; }
    public string Description { get; }
    public decimal Amount { get; }
    public decimal RoundUpAmount => 0m;
    public decimal TotalCharge => Amount;

    public BaseTransaction(decimal amount, string currency, string description)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException("Amount cannot have more than 2 decimal places.", nameof(amount));
        }

        Amount = amount;
        Currency = currency.ToUpperInvariant();
        Description = description;
    }
}
