namespace EuroGlobe.Accounts.Domain;

public abstract class DomainException(string message) : Exception(message)
{
}

public sealed class InsufficientFundsException(decimal requested, decimal available) : DomainException($"Insufficient funds: requested {requested:0.00}, available {available:0.00}.")
{
}

public sealed class AccountNotActiveException(Guid accountId, AccountStatus status) : DomainException($"Account {accountId} is {status} and cannot be used for this operation.")
{
}

public sealed class CurrencyMismatchException(string expected, string actual) : DomainException($"Currency mismatch: expected {expected}, got {actual}.")
{
}

public sealed class AccountNotFoundException(Guid accountId) : DomainException($"Account {accountId} was not found.")
{
}
