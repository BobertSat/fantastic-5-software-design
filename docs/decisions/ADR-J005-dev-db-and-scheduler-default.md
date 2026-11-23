# ADR-J005: Scheduler off by default in Development; local Postgres per developer

- **Status:** Proposed (needs team agreement)
- **Date:** 2026-11-23
- **Area / owner:** Background jobs / member 3, affects everyone running the API
- **Spec deviation:** Gap filled (the spec defines no environments or job infrastructure)

## Context

The scheduler (ADR-J001) runs every registered job when it is due:
- mortgage repayments and delinquency,
- leasing billing,
- vault withdrawal expiry.

The team database is one **Supabase** PostgreSQL instance. Five developers may run the API against it at the same time. With the scheduler on everywhere:

- **Every instance runs every job on the same data.** The in-process overlap guard can't stop this. The advisory lock (ADR-J003, Phase 3) can prevent simultaneous runs, but not repeated runs, and not runs from instances with different code versions.
- **Simulated clocks differ per instance** (ADR-J002). One person's "advance 90 days" would make *their* instance terminate *everyone's* test mortgages and expire everyone's vault withdrawals.

That breaks the shared demo data in ways that are hard to trace.

## Decision

1. **`Jobs:SchedulerEnabled` defaults by environment.**
   - If it is not set, the scheduler is **off in Development** and **on in every other environment**.
   - Setting it explicitly (`true`/`false`) always wins.
   - Manual runs through `IJobRunner` (and the Phase 3 admin endpoint) work either way. So in Development you run jobs on demand, which suits demos and debugging.
2. **`Jobs:UseSimulatedClock` is allowed only in Development.** Startup fails if it is enabled in any other environment, so simulated time can never reach a production-like deployment.
3. **Recommended database setup (to agree with the team):**
   - For day-to-day development, each developer uses a **local Docker PostgreSQL** (`postgres:17`, matching the Supabase major version). Each developer's local Postgres also holds that developer's migrations.
   - **Supabase** is the shared integration/demo database. **Exactly one** instance runs against it with the scheduler enabled.
   - Switching between them changes only `ConnectionStrings:EuroGlobe` (plus `dotnet ef database update` per module context).
4. The other settings:
   - `Jobs:TickSeconds` defaults to 15 and must be between 1 and 3600.
   - Invalid values fail startup.

## Alternatives considered

| Option | Why not |
|---|---|
| **Scheduler on everywhere by default** | It causes the shared-database problems described in Context as soon as two people run the API against Supabase. |
| **Scheduler off everywhere unless configured** | Safe, but a production-like deployment that forgets the setting silently never bills, expires or marks anything delinquent. Off-by-default is only right where many instances share data, which is Development. |
| **Leader election via a database lock** (only one instance schedules) | It solves duplicate scheduling, but not one developer's simulated clock affecting shared data. It is also more machinery than a student project with one demo instance needs. It could be added later on top of ADR-J003's advisory locks. |
| **Simulated clock stored in the database** | See ADR-J002: it couples all modules to a table and makes time travel global for everyone. |

## Consequences

- Good:
  - Running the API locally never surprises teammates.
  - Production-like environments run jobs automatically.
  - Simulated time can't leak outside Development.
- Bad / costs:
  - In Development, jobs don't run by themselves. To see automatic behaviour, set `"Jobs": { "SchedulerEnabled": true }` in your own `appsettings.Development.json` (with a local DB), or use the run-now endpoint.
  - There is an environment-dependent default to remember. This ADR and the `JobsOptions` doc comment record it.
- Follow-ups:
  - Agree point 3 with the team (open ask in the team chat). Update this ADR's status to Accepted or Superseded accordingly.
  - Phase 3: the advisory-lock overlap guard (ADR-J003) and the run-now admin endpoint.
