namespace EuroGlobe.Payments.Transactions;

public interface ITransaction
{
    string Currency { get; }
    string Description { get; }
    decimal Amount { get; }
    decimal RoundUpAmount { get; }
    decimal TotalCharge { get; }
}
