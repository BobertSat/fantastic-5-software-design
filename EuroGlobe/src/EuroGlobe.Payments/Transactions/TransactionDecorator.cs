namespace EuroGlobe.Payments.Transactions;

public abstract class TransactionDecorator(ITransaction inner) : ITransaction
{
    protected readonly ITransaction Inner = inner;

    public virtual string Currency => Inner.Currency;
    public virtual string Description => Inner.Description;
    public virtual decimal Amount => Inner.Amount;
    public virtual decimal RoundUpAmount => Inner.RoundUpAmount;
    public virtual decimal TotalCharge => Inner.TotalCharge;
}
