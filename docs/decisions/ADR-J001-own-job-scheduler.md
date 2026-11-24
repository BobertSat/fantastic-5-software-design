# ADR-J001: Our own lightweight recurring-job scheduler

- **Status:** Proposed
- **Date:** 2026-11-17
- **Area / owner:** Background jobs (shared infrastructure) / member 3
- **Spec deviation:** Gap filled (the spec describes no scheduler)

## Context

Several features need work that runs without a user request:

- **Mortgages:** monthly repayments. Missed payments move a mortgage to Delinquent, and back to Active once paid (spec §6.3, Fig. 8).
- **Leasing:** "automated monthly billing" (spec §2.1 Must #4).
- **Vaults:** a withdrawal "automatically cancels" after a strict 24-hour window (spec §2.2 Story 2 (c), §4.3, Fig. 3).

The spec never says what runs these. Our evaluation flags it: "Must #4 asks for 'automated monthly billing', and Must #3 implies monthly repayments, but no process, scheduler or flow describes either" (`Evaluation.pdf`, criterion 2, Weak). In `EuroGlobe Bank Implementation.pdf` we committed to "a daily background job" for mortgage delinquency and monthly leasing billing "as a background job".

Requirements on the solution:

1. **Demo-able time.** We must show that a mortgage becomes Delinquent, recovers, and is terminated after 90 days without waiting months. "Now" must therefore be controllable (see ADR-J002).
2. **Testable.** Jobs need fast unit tests with a fake clock. Coverage must be at least 70 % per PR.
3. **Simple for teammates.** Member 4 must be able to write a job against `EuroGlobe.Contracts` only.
4. **Small infrastructure.** We run one ASP.NET Core process with PostgreSQL (local Docker or Supabase). There is no Redis and no separate worker service.

## Decision

We build a small scheduler in-house:

- **Contracts** (`EuroGlobe.Contracts`, delivered in this ADR's PR):
  - `IRecurringJob` with `Name`, `Schedule` and `ExecuteAsync(JobContext, CancellationToken)`.
  - `JobSchedule` with `Every(TimeSpan)`, `DailyAt(TimeOnly utc)` and a pure `GetNextOccurrence(after)`.
  - `JobContext` with run id, job name, scheduled time, trigger, clock, and processed/failed counters.
  - `JobTrigger`, which is `Scheduled` or `Manual`.
  - `IClock`.
- **Schedule semantics.** All times are UTC.
  - `GetNextOccurrence` returns the first occurrence **strictly after** the given instant.
  - Interval schedules are aligned to fixed UTC boundaries, so "every 15 min" runs at :00, :15, :30 and :45. The result does not depend on when the process started.
- **Implementation** (`EuroGlobe.Jobs`, from Phase 2):
  - A `PeriodicTimer`-based hosted service runs due jobs. A missed daily run is caught up once.
  - Each run gets a new DI scope and is wrapped in try/catch. Its result (Succeeded, Failed or Skipped, plus counters) goes to a run store, which is the `jobs.job_runs` table from Phase 3.
  - A run is skipped if the same job is already running (ADR-J003).
  - Admin/demo endpoints can run a job now and list run history.
- **Rules for job authors**, documented on `IRecurringJob`:
  - Jobs are idempotent for a given "now".
  - Time comes only from `IClock`.
  - Exceptions propagate. Per-item failures are logged and counted.
  - Runs are short and honour the `CancellationToken`.

## Alternatives considered

| Option | Why not |
|---|---|
| **Hangfire** (+ Hangfire.PostgreSql) | Recurring jobs use real wall-clock time and cron, so there is no simple way to "advance the clock 90 days" for the demo. It adds its own schema (about 10 tables) and a dashboard we would have to secure. The job API is not expressible in `EuroGlobe.Contracts` without referencing Hangfire. |
| **Quartz.NET** | Powerful, but heavy for three or four jobs (triggers, calendars, job stores, misfire policies). It has the same real-time problem: its time comes from `SystemTime` and not our `IClock`. |
| **One `BackgroundService` per module** | Every member would re-implement timing, error handling, overlap protection and run history, inconsistently. It cannot be listed or triggered uniformly from the admin endpoints. |
| **`pg_cron` in PostgreSQL** | Puts business logic in SQL. It is not available in the local Docker image by default, and it is invisible to our tests and the simulated clock. |
| **Run jobs only on demand** (endpoint/cron outside the app) | It would not meet "automatic" (Story 2 (c), Must #4). |

## Consequences

- Good:
  - Few dependencies.
  - Jobs are plain classes testable with a fake clock.
  - The simulated clock drives the whole demo.
  - Member 4 depends only on Contracts.
  - Schedule logic is pure and unit-tested; this PR's tests cover it at 100 %.
- Bad / costs:
  - We own correctness: overlap guard (ADR-J003), catch-up and failure recording.
  - There are no retries with backoff, distributed queueing or a built-in dashboard; the admin endpoints replace the dashboard.
  - It is designed for one or a few app instances, not a worker fleet.
- Follow-ups:
  - Phase 2: hosted service, runner, registry, `AddRecurringJob<T>()`.
  - Phase 3: persistence of runs, the advisory-lock guard (ADR-J003) and the dev DB / `SchedulerEnabled` strategy (ADR-J005).
  - A "how to write a job" guide for member 4.
