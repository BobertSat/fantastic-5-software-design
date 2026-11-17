# ADR-J002: `IClock` in Contracts (with a simulated clock), not `TimeProvider`

- **Status:** Proposed
- **Date:** 2026-11-17
- **Area / owner:** Background jobs / shared time abstraction / member 3
- **Spec deviation:** Gap filled (the spec does not define a time source)

## Context

Several rules depend on "now":

- **Mortgage payments.** Payments fall due monthly. A mortgage becomes Delinquent when a payment is overdue (spec §6.3, Fig. 8) and is terminated after a long delinquency (our rule, ADR-M010).
- **Leasing billing.** Bills are monthly (§2.1 Must #4).
- **Vault withdrawals.** They expire after exactly 24 h (§2.2 Story 2 (c), §4.3).

We need to:

1. **Demonstrate** these transitions in a live demo without waiting days or months. An admin/demo endpoint must be able to move "now" forward.
2. **Test** them deterministically, with no `Thread.Sleep` and no flaky date boundaries.
3. Let **every module** use the same notion of "now", through `EuroGlobe.Contracts` only. This is the team rule that modules talk only through Contracts.

Our evaluation notes that the spec leaves time-driven behaviour undefined ("The document also does not say what moves a mortgage into Delinquent", `Evaluation.pdf` criterion 3, Weak; and criterion 2 on billing).

.NET 8+ ships `System.TimeProvider`, with `FakeTimeProvider` in `Microsoft.Extensions.TimeProvider.Testing`.

## Decision

- `EuroGlobe.Contracts.Time.IClock` has two members:
  - `DateTimeOffset UtcNow`.
  - `DateOnly Today`, a default interface member equal to the UTC date of `UtcNow`.
- **All business logic and jobs take time only from `IClock`**, either injected or `JobContext.Clock`. Calling `DateTime.Now`, `DateTime.UtcNow` or `DateTimeOffset.Now` in domain code is not allowed.
- `EuroGlobe.Jobs` (Phase 2) provides:
  - `SystemClock`, the real time.
  - `SimulatedClock`, which is real time plus an offset, with `Advance(TimeSpan)` and `Reset()`. It is in-memory, per process, and used only when `Jobs:UseSimulatedClock=true` (Development/demo).
- Admin endpoints (Phase 3, `Demo:EnableAdminEndpoints` only): `GET /api/admin/clock`, `POST /api/admin/clock/advance` and `POST /api/admin/clock/reset`.
- The scheduler's own tick timer (`PeriodicTimer`) keeps using real time. Only the clock that decides what is *due* is simulated. After an "advance", the next tick sees the jumped clock and catches a missed daily run up once (ADR-J001).

## Alternatives considered

| Option | Why not |
|---|---|
| **`TimeProvider` everywhere** | It is a good, standard abstraction. However, it is an abstract class with timers, timestamps and local time zones, which is more than our domain needs. It has no `Today`/`DateOnly`. A "simulated" runtime provider that jumps forward would also affect timers and `CancellationTokenSource` timeouts in framework code if registered globally. A tiny interface is easier for teammates to fake and to read in reviews. |
| **`DateTime.UtcNow` + test seams per class** | Not demo-able, inconsistent across modules, and leads to flaky tests around midnight. |
| **Simulated time stored in the database** (shared offset) | It would let several instances share one demo clock. But it adds a query to every "now" and couples all modules to a table. It also makes one developer's "advance 90 days" affect everybody on a shared Supabase DB (see ADR-J005). It can be revisited if we ever run more than one instance in the demo. |

## Consequences

- Good:
  - Deterministic tests (a trivial `FakeClock`).
  - One-click time travel for the demo.
  - Teammates depend only on Contracts.
  - `Today` is consistently the UTC date.
- Bad / costs:
  - It is not the BCL standard. If a library needs `TimeProvider`, we add a small adapter (`IClock` → `TimeProvider`) later.
  - The simulated offset is per process and lost on restart. That is acceptable for a demo and documented for teammates.
  - Discipline is required: code review must reject direct `DateTime.Now` in domain code.
- Follow-ups:
  - Phase 2: `SystemClock`, `SimulatedClock`, registration.
  - Phase 3: clock admin endpoints.
