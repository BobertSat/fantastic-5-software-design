# ADR-M001: One DbContext and one PostgreSQL schema per module

- **Status:** Proposed (applies to my modules; offered to the team as the pattern)
- **Date:** 2026-11-23
- **Area / owner:** Persistence for jobs and mortgages / member 3
- **Spec deviation:** Yes. The spec's data model is one shared schema (Fig. 15), and `euroglobe.sql` is a single MySQL database.

## Context

The spec draws one relational model in which `Mortgage.account_id` is a foreign key to `Account` (Fig. 15). `euroglobe.sql` is a single MySQL schema with different tables and keys again. Our evaluation calls this out: "Data model 3/10 … contradicts the SQL file" (`Evaluation.pdf`, criterion 7).

Our team works as five module owners in one ASP.NET Core application (spec §5.1 lists the modules). We agreed that modules talk **only through `EuroGlobe.Contracts` interfaces**. The database is PostgreSQL: a local Docker container for development, and Supabase as the shared database (ADR-J005).

If all modules shared one DbContext or one schema:
- every migration would touch everyone,
- cross-module foreign keys and joins would bypass the Contracts boundary,
- merge conflicts in one model snapshot would be constant.

## Decision

- Each module owns **one DbContext** in its own project and **its own PostgreSQL schema**. Mine are `jobs` (`JobsDbContext`, this PR) and `mortgages` (`MortgagesDbContext`, Phase 4).
- Each context keeps **its own migrations history table** inside its schema: `MigrationsHistoryTable("__EFMigrationsHistory", "<schema>")`. Each module's migrations are applied independently:
  ```
  dotnet ef database update --project src/EuroGlobe.<Module> --startup-project src/EuroGlobe.Api --context <Module>DbContext
  ```
- **No cross-schema foreign keys or joins.**
  - A reference to another module's data is a plain id column, e.g. `mortgages.mortgages.account_id uuid`, which has no FK to the accounts schema.
  - The data is read through that module's Contracts interface (e.g. `IAccountService`).
- Naming conventions:
  - snake_case via `EFCore.NamingConventions`.
  - Money is `numeric(19,2)` and rates `numeric(9,6)`.
  - Times are `timestamptz`, stored in UTC.
  - Ids are `uuid`.
- Connection: one connection string `ConnectionStrings:EuroGlobe` for all modules (one database, many schemas). The code is provider-neutral, so switching between local Docker and Supabase changes only that string.

## Alternatives considered

| Option | Why not |
|---|---|
| **One shared DbContext / schema** (the spec's Fig. 15) | It couples every module's migrations and lets modules join each other's tables, which breaks our Contracts-only rule. Every PR would conflict in one model snapshot. |
| **A separate database per module** | It gives the strongest isolation, but Supabase gives us one database, and it adds connection strings and cross-database plumbing for no benefit in a monolith. |
| **Schemas, but shared migrations** | Shared migrations still serialize all modules' schema changes through one history. |
| **Follow `euroglobe.sql`** | It is MySQL, uses INT keys, and contradicts the spec (evaluation criterion 7). |

## Consequences

- Good:
  - Modules evolve their tables independently, migrations don't conflict, and the schema boundary mirrors the Contracts boundary.
  - Teammates can see at a glance which tables belong to whom.
- Bad / costs:
  - No referential integrity across modules: a mortgage can point to an account id that doesn't exist. This is mitigated by validating through `IAccountService` at the time of use.
  - Reports that span modules must go through Contracts, or a read model, instead of SQL joins.
  - Each module's migrations must be applied separately (one `dotnet ef database update` per context).
- Follow-ups: `MortgagesDbContext` follows this in Phase 4. Teammates may adopt the pattern; it is not imposed on them.
