# How to write a background job

The shared job infrastructure (owner: member 3) runs recurring work such as leasing billing, vault-withdrawal expiry and mortgage repayments. This page is everything you need to add a job. The design reasons are in [ADR-J001](../decisions/ADR-J001-own-job-scheduler.md) to [ADR-J006](../decisions/ADR-J006-demo-admin-endpoints.md).

## 1. Write the job (Contracts only)

```csharp
using EuroGlobe.Contracts.Jobs;

internal sealed class LeasingBillingJob(ILeasingBilling billing, ILogger<LeasingBillingJob> logger) : IRecurringJob
{
    public string Name => "leasing-billing";                         // unique, kebab-case
    public JobSchedule Schedule => JobSchedule.DailyAt(new TimeOnly(2, 0)); // UTC

    public async Task ExecuteAsync(JobContext context, CancellationToken cancellationToken)
    {
        var today = context.Clock.Today;                              // never DateTime.Now
        foreach (var contract in await billing.GetDueAsync(today, cancellationToken))
        {
            try
            {
                await billing.ChargeAsync(contract, today, cancellationToken);
                context.ReportProcessed();
            }
            catch (InsufficientFundsException ex)                     // an expected per-item failure
            {
                logger.LogWarning(ex, "Could not bill contract {ContractId}", contract.Id);
                context.ReportFailed();
            }
        }
    }
}
```

Schedules:
- `JobSchedule.DailyAt(new TimeOnly(2, 0))` runs once a day at 02:00 UTC.
- `JobSchedule.Every(TimeSpan.FromMinutes(1))` runs on every whole minute. Intervals line up with round UTC times: every 15 min runs at :00, :15, :30 and :45.

## 2. Register it

```csharp
services.AddRecurringJob<LeasingBillingJob>();   // from EuroGlobe.Jobs
```

Call it from your module's registration method (your project then references `EuroGlobe.Jobs`), or from `Program.cs`. The job is resolved from a **new DI scope for every run**, so it can inject scoped services such as your DbContext.

At startup the app fails with a clear message if two jobs share a name, or if a name is not kebab-case or is longer than 100 characters.

## 3. Rules

1. **Idempotent.** Running the job twice for the same "now" must do nothing new. Process what is due as of `context.Clock` and mark it handled, e.g. set `billed_for_period`.
2. **Time comes only from `context.Clock`** (or an injected `IClock`). The demo moves this clock forward. `DateTime.Now` would ignore that.
3. **Don't swallow exceptions.** An exception that escapes `ExecuteAsync` marks the run **Failed** and is logged; the scheduler keeps going. For an expected per-item failure, log it, call `context.ReportFailed()` and continue.
4. **Keep runs short**, work in batches, and pass the `CancellationToken` on. It is cancelled on shutdown.

## 4. How it runs

- Every `Jobs:TickSeconds` (default 15 s), the scheduler starts each job that is due according to the clock.
- **Missed runs** are caught up with a **single** run for the latest missed occurrence. Missed runs come from the app being down, or from the simulated clock jumping forward. Thanks to rule 1, one run is enough.
- **Never twice at once.** A run that would overlap with a running one (in this process, or in another app instance on the same database) is recorded as **Skipped**.
- **Every run is recorded** with its trigger, times, status and your processed/failed counts. With `ConnectionStrings:EuroGlobe` set, the record goes to `jobs.job_runs`; without it, it is kept in memory.

## 5. Configuration

| Key | Default | Meaning |
|---|---|---|
| `Jobs:SchedulerEnabled` | off in Development, on elsewhere | Run jobs automatically. Manual runs always work. |
| `Jobs:TickSeconds` | 15 | How often to check for due jobs (1 to 3600). |
| `Jobs:UseSimulatedClock` | false | Development only. The clock can be moved with the admin endpoints. |
| `Demo:EnableAdminEndpoints` | false | Development only. Turns on the endpoints below. |

To see your job run on its own locally, put this in your `appsettings.Development.json` (with a local database, see ADR-J005):

```json
"Jobs": { "SchedulerEnabled": true, "UseSimulatedClock": true },
"Demo": { "EnableAdminEndpoints": true }
```

Apply the jobs migration once per database:

```
dotnet ef database update --project src/EuroGlobe.Jobs --startup-project src/EuroGlobe.Api --context JobsDbContext
```

## 6. Demo / admin endpoints (Development + `Demo:EnableAdminEndpoints=true`)

| Request | What it does |
|---|---|
| `GET /api/admin/jobs` | Jobs, schedules, next occurrence, last 10 runs |
| `POST /api/admin/jobs/leasing-billing/run` | Run now. Returns 200 with the run (even if it Failed), 404 for an unknown job, 409 if it is already running. |
| `GET /api/admin/clock` | Current (possibly simulated) time and offset |
| `POST /api/admin/clock/advance` `{ "days": 30 }` | Move the simulated clock forward (`days`, `hours`, `minutes`) |
| `POST /api/admin/clock/reset` | Back to real time |

A typical demo: advance the clock 30 days, then run the job now, then check your data and the run in `GET /api/admin/jobs`.

## 7. Testing your job

Test the job class directly: construct it with fakes and call `ExecuteAsync` with a `JobContext` that uses a fake `IClock`.

```csharp
var clock = new FakeClock(new DateTimeOffset(2026, 12, 1, 2, 0, 0, TimeSpan.Zero));
var context = new JobContext(Guid.NewGuid(), "leasing-billing", clock.UtcNow, JobTrigger.Manual, clock);

await job.ExecuteAsync(context, CancellationToken.None);

Assert.Equal(3, context.ItemsProcessed);
```

Also test **idempotency**: run it twice with the same clock and assert that nothing changed the second time.
