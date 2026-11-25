# ADR-J006: Demo/admin endpoints, Development only and switched off by default

- **Status:** Proposed
- **Date:** 2026-11-23
- **Area / owner:** Background jobs (the mortgage admin endpoints will reuse this) / member 3
- **Spec deviation:** Yes. The spec's API (§9.2) has no admin endpoints, and the spec has no admin actor.

## Context

The spec's time-driven rules can't be shown in a live demo without tooling:
- a mortgage becoming Delinquent and later Terminated (§6.3, Fig. 8),
- monthly leasing billing (§2.1 Must #4),
- 24-hour vault expiry (§2.2 Story 2 (c)).

Our evaluation notes that the spec has "No bank-employee or admin actor" (`Evaluation.pdf`, criterion 3, Weak). Its API covers only customer actions (§9.2).

For the demo we need to:
- see the registered jobs, their schedules and their recent runs,
- run a job now,
- read the clock, move the simulated clock forward (ADR-J002), and reset it.

These are powerful and **unauthenticated** operations. They must never exist in a production-like deployment.

## Decision

- Admin controllers carry the `[DemoAdminEndpoint]` attribute. An MVC `ControllerFeature` provider **removes these controllers entirely** unless `Demo:EnableAdminEndpoints=true`. They are then not routed (404) and not in the OpenAPI document.
- Setting `Demo:EnableAdminEndpoints=true` **outside Development fails startup**, the same rule as `Jobs:UseSimulatedClock` (ADR-J005).
- Jobs and clock endpoints, under `/api/admin`:

  | Method | Path | Result |
  |---|---|---|
  | GET | `/api/admin/jobs` | Jobs with schedule, next occurrence and last 10 runs; plus `schedulerEnabled`, `persistentHistory` and `simulatedClock` |
  | POST | `/api/admin/jobs/{name}/run` | 200 with the finished run (a job failure is still 200 with `status: "Failed"`); 404 `JOB_NOT_FOUND`; 409 `JOB_ALREADY_RUNNING` |
  | GET | `/api/admin/clock` | `utcNow`, `today`, `simulated`, `offset` |
  | POST | `/api/admin/clock/advance` | Body `{ days, hours, minutes }`, each ≥ 0 and in total more than 0 and at most 3650 days; 400 `INVALID_CLOCK_ADVANCE` otherwise; 409 `SIMULATED_CLOCK_DISABLED` if the clock is not simulated |
  | POST | `/api/admin/clock/reset` | Back to real time; 409 `SIMULATED_CLOCK_DISABLED` if not simulated |

- **Errors** are RFC 7807 `application/problem+json` with an extra machine-readable `code`. This is the format we plan for the whole API (ADR-M018 will formalise it once member 2 provides the team ProblemDetails middleware).
- The mortgage admin endpoints (Euribor, terminate) will use the same attribute and flag.

## Alternatives considered

| Option | Why not |
|---|---|
| **Map them always and protect them with an admin role** | There is no admin actor or role in the spec, and authentication isn't built yet (member 1). It also leaves powerful tools in every deployment. |
| **Map them always and return 403/404 at runtime when disabled** (filter) | The endpoints would still appear in OpenAPI and in routing, which misleads API consumers. Removing the controllers is cleaner. |
| **A CLI or a separate admin app** | More moving parts. The simulated clock lives in the API process (ADR-J002), so the tools must run inside it. |
| **Allow the flag in any environment** | A single config mistake would expose unauthenticated time travel and job triggering. Failing startup is safer. |

## Consequences

- Good:
  - The demo can drive the entire time-based lifecycle through the API.
  - The endpoints are invisible and impossible to call anywhere except Development with the flag on.
- Bad / costs:
  - In Development with the flag on, anyone who can reach the API can trigger jobs and move time. That is acceptable for local/demo use only.
  - A demo must run in the Development environment.
- Follow-ups:
  - Add the mortgage admin endpoints (Phases 8 and 9) under the same attribute.
  - Revisit once member 1 delivers authentication: the endpoints could additionally require an admin claim.
