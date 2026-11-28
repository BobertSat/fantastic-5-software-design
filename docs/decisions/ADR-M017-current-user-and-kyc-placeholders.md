# ADR-M017: Temporary current-user and KYC placeholders

- **Status:** Proposed. Superseded once member 1 publishes the identity contracts.
- **Date:** 2026-11-28
- **Area / owner:** Smart Mortgage / member 3 (identity is owned by member 1)
- **Spec deviation:** No. These are temporary stand-ins for spec behaviour that another module will provide.

## Context

The mortgage flow needs two facts that the Identity module (member 1) owns:

1. **Who is calling.** Each user may only see and act on their own mortgages. The spec's API uses "a simple session token returned by /auth/login" (§9) but doesn't define it. Our evaluation notes: "No token details (header name, expiry)" (`Evaluation.pdf`, criterion 8).
2. **Whether the caller passed KYC.** "KYC incomplete? Accounts, mortgages, leasing, cards, transfers return 422" (§11.1). Fig. 1 also starts with "Verify identity and KYC".

Member 1 is still designing these contracts. Without placeholders, the mortgage module couldn't be built or demonstrated until identity is finished.

Member 2's `AddAccountsModule()` already registers `IAccountService` (in-memory), `IScaService` (a mock that accepts any 6 digits) and `IAuditLog` (console). The mortgage module therefore needs **no** stubs of its own for those, and tests use their own fakes.

## Decision

- Inside the mortgage module, define two interfaces:
  - `ICurrentUser`, with `UserId` and `RequireUserId()`. `RequireUserId()` throws `MortgageUnauthenticatedException`, which becomes 401 (ADR-M018).
  - `IMortgageKycGate`, with `IsVerifiedAsync(userId)`. When it returns false, the operation fails with 422 `KYC_NOT_VERIFIED`.
- Placeholder implementations, registered with `TryAdd` so a real implementation registered earlier wins:
  - **`HttpCurrentUser`** takes the user id from the `sub` (or name-identifier) claim. **In Development only**, it also accepts an `X-User-Id: <guid>` header, so the API can be tried before login exists. Outside Development the header is ignored. A valid claim always wins over the header.
  - **`AlwaysVerifiedKycGate`** returns verified for everyone.
- **When member 1 delivers:** an adapter maps their contract to these two interfaces, or the interfaces are replaced. Then this ADR moves to Superseded (plan Phase 12).

## Alternatives considered

| Option | Why not |
|---|---|
| **Wait for member 1** | It blocks the whole mortgage module (Phases 4–11) on another person's timeline. |
| **Take `userId` as a request parameter** | Any caller could act as anyone, and the API shape would be wrong once authentication exists. |
| **Accept the header in every environment** | Trivially spoofable. Limiting it to Development keeps it a developer convenience only. |
| **Put the placeholders in Contracts** | They would look like an agreed team contract when they're temporary and mortgage-specific. |

## Consequences

- Good:
  - The mortgage API can be built, tested and demoed now (`X-User-Id` in Development).
  - Switching to the real identity contracts is a registration change, not a rewrite.
- Bad / costs:
  - Until member 1 delivers, nothing outside Development is authenticated (no claim, so 401), and KYC is never enforced.
  - The `X-User-Id` header must not leak into demo material as if it were real authentication.
- Follow-ups:
  - Phase 12: swap in member 1's contracts.
  - Ask member 2 for a typed ownership check on `IAccountService` (e.g. `IsOwnerAsync(accountId, userId)`). `GetAccountsAsync` currently returns `IReadOnlyList<dynamic>`, which can't be checked reliably.
