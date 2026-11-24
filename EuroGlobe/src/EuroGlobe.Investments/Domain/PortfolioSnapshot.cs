namespace EuroGlobe.Investments.Domain;

public class PortfolioSnapshot
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateOnly Date { get; private set; }
    public decimal ValueEur { get; private set; }
    public decimal InvestedEur { get; private set; }

    public decimal ProfitEur => ValueEur - InvestedEur;

    public decimal YieldPercent => InvestedEur == 0 ? 0 : Math.Round(ProfitEur / InvestedEur * 100, 2);

    public PortfolioSnapshot(Guid userId, DateOnly date, decimal valueEur, decimal investedEur)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(valueEur);
        ArgumentOutOfRangeException.ThrowIfNegative(investedEur);

        Id = Guid.NewGuid();
        UserId = userId;
        Date = date;
        ValueEur = valueEur;
        InvestedEur = investedEur;
    }
}