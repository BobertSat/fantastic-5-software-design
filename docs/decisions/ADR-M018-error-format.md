# ADR-M018: Errors as RFC 7807 problem details with a machine-readable `code`

- **Status:** Proposed
- **Date:** 2026-11-28
- **Area / owner:** Smart Mortgage API (also used by the jobs admin endpoints, ADR-J006) / member 3
- **Spec deviation:** Yes. §9.1 says "No custom error codes are needed" and defines no error body.

## Context

The spec lists only HTTP status codes (§9.1): 400, 401, 403, 404, 409 ("Duplicate or invalid state"), 422 ("Business rule failed (insufficient funds, KYC pending)") and 500. Our evaluation notes: "No error response body is defined" (`Evaluation.pdf`, criterion 8).

A status alone isn't enough for the mortgage screens. Several different failures share **422**, and each needs a different message or action on the React side:
- KYC not verified,
- wrong TOTP,
- insufficient funds,
- an early repayment above the remaining principal.

A team-wide error middleware is an open request to member 2, so the mortgage module must not depend on it.

## Decision

- Every error response is **RFC 7807 `application/problem+json`**: `type`, `title`, `status`, `detail`, `traceId`, plus an extra **`code`** field with a stable UPPER_SNAKE_CASE value.
- The module throws typed exceptions, all derived from `MortgageException` (which carries the `code`). The API maps them in one place: the `[MortgageProblemDetails]` exception filter on `MortgageControllerBase`.

  | Exception | Status | `code` |
  |---|---|---|
  | `MortgageValidationException` | 400 | `VALIDATION_FAILED`, plus `errors: { field: [messages] }` |
  | `MortgageUnauthenticatedException` | 401 | `UNAUTHENTICATED` |
  | `MortgageAccessDeniedException` | 403 | `FORBIDDEN` |
  | `MortgageNotFoundException` | 404 | `MORTGAGE_NOT_FOUND` |
  | `InvalidMortgageStateException` | 409 | `INVALID_MORTGAGE_STATE` |
  | `MortgageRuleViolationException` | 422 | specific: `KYC_NOT_VERIFIED`, `SCA_FAILED`, `INSUFFICIENT_FUNDS`, `AMOUNT_EXCEEDS_REMAINING` (new ones are added in the PR that introduces them) |

- Any other exception is left to the framework (500, no internals leaked). An unmapped `MortgageException` subtype is also left alone: a test makes forgetting a mapping visible instead of silently guessing a status.
- The jobs admin endpoints (ADR-J006) already use the same shape and codes, for example `JOB_NOT_FOUND`.
- **If member 2 adds a team middleware later**, these exceptions can be mapped there instead. The response shape stays the same.

## Alternatives considered

| Option | Why not |
|---|---|
| **Status codes only** (§9.1) | The client can't tell the different 422 failures apart without parsing English messages. |
| **A custom error envelope** (`{ error: { code, message } }`) | It reinvents a standard that ASP.NET Core already produces, and the framework's own 400s and 404s would look different from ours. |
| **Global `IExceptionHandler` middleware** | It's the cleaner long-term place, but it needs `Program.cs` changes that member 2 owns and that affect every module. A controller filter keeps it local until the team agrees. |
| **Return result objects instead of throwing** | It adds noise through every layer for no gain in a request/response API; exceptions map naturally to one response. |

## Consequences

- Good:
  - A uniform, standard error body.
  - The React screens can switch on `code`.
  - Validation errors list each field.
  - One mapping table, with tests for every row.
- Bad / costs:
  - It deviates from §9.1's "no custom error codes".
  - Codes become part of the API contract, so they must not be renamed lightly.
  - Only controllers deriving from `MortgageControllerBase` get the mapping.
