# Architecture Decision Records

We record every architecture/design decision here, including:

- deviations from the EuroGlobe spec (`first-lab-assignment.pdf`),
- gaps in the spec that we fill,
- parts of the spec we disagree with.

Each ADR cites the spec section/figure/screen and, where relevant, the point in our `Evaluation.pdf`.

## How to add one

1. Copy [`_template.md`](_template.md) to `ADR-<prefix><###>-<slug>.md`. Number them in order within the prefix.
2. Fill in Status, Context, Decision, Alternatives, Consequences and Spec deviation.
3. Add a row to the index below in the same PR as the code that introduces the decision.

Prefixes in use: **J** = background jobs (shared infrastructure), **M** = Smart Mortgage. Other modules can add their own letter.

## Index

| ADR | Title | Status | Spec deviation |
|---|---|---|---|
| [J001](ADR-J001-own-job-scheduler.md) | Our own lightweight recurring-job scheduler | Accepted | Gap filled |
| [J002](ADR-J002-iclock-and-simulated-clock.md) | `IClock` in Contracts (with a simulated clock), not `TimeProvider` | Accepted | Gap filled |
| [J004](ADR-J004-ci-coverage-gate.md) | CI coverage gate for Part 3 (jobs + mortgages) | Proposed | No |
| [J005](ADR-J005-dev-db-and-scheduler-default.md) | Scheduler off by default in Development; local Postgres per developer | Proposed | Gap filled |
