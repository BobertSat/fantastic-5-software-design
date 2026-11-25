# ADR-J003: PostgreSQL advisory lock as the cross-instance overlap guard

- **Status:** Proposed
- **Date:** 2026-11-23
- **Area / owner:** Background jobs / member 3
- **Spec deviation:** Gap filled (the spec describes no job infrastructure; see ADR-J001)

## Context

A job must never run twice at the same time:
- A repayment job running twice in parallel could debit an installment twice.
- A vault-expiry job could release reserved funds twice.

The spec's own risk table names "Double withdrawal → Locking + executed state" (§11.5) and "Simultaneous approvals? Database lock prevents double execution" (§11.1). It doesn't say how.

Within one process, the runner already holds a per-job `SemaphoreSlim`. A run that would overlap is recorded as **Skipped** (Phase 2). That doesn't help when **two app instances** share one database, for example two developers on Supabase (ADR-J005) or two replicas in a deployment.

Jobs are also written to be idempotent (rules for job authors). The lock is a second line of defence, not a replacement for idempotency.

## Decision

- When `ConnectionStrings:EuroGlobe` is set, every run first takes a **PostgreSQL session advisory lock**, `pg_try_advisory_lock(key)`. If the lock is busy, the run is recorded as **Skipped** with the reason "Another app instance is running this job." A manual run then returns **409** (`JOB_ALREADY_RUNNING`).
- **Key:** the first 8 bytes (big-endian, signed 64-bit) of SHA-256 of `euroglobe.jobs:{jobName}`.
  - It is stable across processes, machines and .NET versions.
  - `string.GetHashCode()` is never used because it is randomized per process.
  - The `euroglobe.jobs:` prefix keeps our keys apart from any other advisory-lock user.
- **Session scope.** Advisory locks belong to a database session.
  - The lock handle keeps **one dedicated connection** from an `NpgsqlDataSource` open for the whole run.
  - It calls `pg_advisory_unlock` in `DisposeAsync` (via `await using`) and then closes the connection.
  - If the process dies, the session ends and PostgreSQL releases the lock. A test proves this by terminating the holding backend.
  - If unlocking fails, the connection goes back to the pool. Npgsql resets returned connections (`DISCARD ALL`), which also releases leftover advisory locks.
- **No database:** a no-op lock provider is used. A single process with in-memory history has nothing to coordinate with.
- **Supabase:** use the **session pooler (port 5432) or a direct connection**, never the transaction pooler (6543). With transaction pooling, a session lock can land on a different server connection than the unlock.

## Alternatives considered

| Option | Why not |
|---|---|
| **Lock table row** (`SELECT … FOR UPDATE` / a `locked_until` column) | A row lock lasts only as long as a transaction, so we'd need either a transaction for the whole run (long transactions, blocking) or a lease with expiry and clean-up of crashed holders. Advisory locks give crash-safe release for free. |
| **Transaction-level advisory lock** (`pg_try_advisory_xact_lock`) | It is released at commit. A job does several of its own `SaveChanges` and calls other modules, so there is no single transaction spanning the run. |
| **`string.GetHashCode()` or a hand-rolled hash** | `GetHashCode` changes between processes, so two instances would compute different keys and the lock would do nothing. SHA-256 is standard and deterministic. |
| **Distributed lock library / Redis** | Adds infrastructure we don't run. PostgreSQL is already there. |
| **Rely on idempotency only** | Idempotent jobs can still race: both instances read "unpaid" before either writes "paid". |

## Consequences

- Good:
  - Correct across any number of instances.
  - No schema, no clean-up of stale locks, and release on crash.
  - Proven by Testcontainers tests:
    - two instances on one database: the second is refused,
    - different jobs don't block each other,
    - a killed holder releases its lock.
- Bad / costs:
  - One extra database connection per running job.
  - Depends on session-preserving connections (see the Supabase note).
  - A run that is skipped because another instance holds the lock still shows up in this instance's history as Skipped. This is intentional, since it is visible in the demo.
- Follow-ups: none planned. If we ever need "only one instance schedules at all" (leader election), it can reuse the same key scheme (ADR-J005).
