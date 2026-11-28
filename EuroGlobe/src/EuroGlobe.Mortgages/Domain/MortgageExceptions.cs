namespace EuroGlobe.Mortgages.Domain;

/// <summary>
/// Base for every error the mortgage module reports to callers. The API maps each type to an HTTP status
/// and returns <see cref="Code"/> in the problem response (ADR-M018).
/// </summary>
public abstract class MortgageException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>400: the request itself is invalid.</summary>
public sealed class MortgageValidationException(IReadOnlyDictionary<string, string[]> errors)
    : MortgageException("VALIDATION_FAILED", "One or more fields are invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

/// <summary>401: no authenticated user.</summary>
public sealed class MortgageUnauthenticatedException()
    : MortgageException("UNAUTHENTICATED", "You must be signed in.");

/// <summary>403: the mortgage or account belongs to someone else.</summary>
public sealed class MortgageAccessDeniedException(string message)
    : MortgageException("FORBIDDEN", message);

/// <summary>404</summary>
public sealed class MortgageNotFoundException(Guid mortgageId)
    : MortgageException("MORTGAGE_NOT_FOUND", $"Mortgage {mortgageId} was not found.")
{
    public Guid MortgageId { get; } = mortgageId;
}

/// <summary>409: the action is not allowed in the mortgage's current state (State pattern).</summary>
public sealed class InvalidMortgageStateException(MortgageState state, string action)
    : MortgageException("INVALID_MORTGAGE_STATE", $"Cannot {action} a mortgage that is {state}.")
{
    public MortgageState State { get; } = state;

    public string Action { get; } = action;
}

/// <summary>
/// 422: a business rule failed, e.g. <c>KYC_NOT_VERIFIED</c>, <c>SCA_FAILED</c>, <c>INSUFFICIENT_FUNDS</c>.
/// </summary>
public sealed class MortgageRuleViolationException(string code, string message)
    : MortgageException(code, message);
