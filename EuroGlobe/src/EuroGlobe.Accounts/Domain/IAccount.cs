namespace EuroGlobe.Accounts.Domain;

public interface IAccount
{
    Guid Id { get; }
    string Currency { get; }
    decimal GetBalance();
    decimal GetAvailableBalance();
}
