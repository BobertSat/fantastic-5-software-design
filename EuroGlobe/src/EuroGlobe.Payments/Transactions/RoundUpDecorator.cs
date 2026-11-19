namespace EuroGlobe.Payments.Transactions;

public class RoundUpDecorator(ITransaction inner) : TransactionDecorator(inner)
{
    private decimal Extra => Math.Ceiling(Inner.TotalCharge) - Inner.TotalCharge;

    public override decimal RoundUpAmount => Inner.RoundUpAmount + Extra;
    public override decimal TotalCharge => Inner.TotalCharge + Extra;
}
