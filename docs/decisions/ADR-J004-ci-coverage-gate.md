# ADR-J004: CI coverage gate for Part 3 (jobs + mortgages)

- **Status:** Proposed
- **Date:** 2026-11-23
- **Area / owner:** Background jobs + Smart Mortgage (team quality rule) / member 3
- **Spec deviation:** No (process decision; the spec says nothing about testing)

## Context

For my part I committed to **at least 70 % line and branch coverage per PR**. The gate is already configured in each test project with `coverlet.msbuild`. Locally it is one command (`dotnet test EuroGlobe.slnx /p:CollectCoverage=true`), which fails below the threshold.

A local-only gate depends on everyone remembering to run it. Reviewers also can't see whether it passed. The repository had no CI at all before this PR.

The spec (`first-lab-assignment.pdf`) and our `Evaluation.pdf` don't cover testing. This is an internal quality decision. It also gives evidence for the course's "implementable and tested" expectations.

## Decision

- Add the GitHub Actions workflow `.github/workflows/mortgages-coverage.yml`. It runs on an `ubuntu-latest` runner with .NET `10.0.x`.
- Steps: restore, Release build, then `dotnet test EuroGlobe.slnx --no-build -c Release /p:CollectCoverage=true`. The check fails when any gated module is below 70 % line or branch coverage. The Cobertura reports are uploaded as a build artifact.
- **Triggers:**
  - PRs that touch Part 3 code: `EuroGlobe.Jobs`, `EuroGlobe.Mortgages`, `EuroGlobe.Contracts`, the `Controllers/Jobs` and `Controllers/Mortgages` folders in the API, my test projects, and the workflow file itself.
  - Manual runs (`workflow_dispatch`).
- **What is gated** is defined in each test project's `<Include>` filter:
  - `EuroGlobe.Jobs.Tests`: the Jobs project, `Contracts.Jobs.*`, `Contracts.Time.*` and the jobs controllers.
  - `EuroGlobe.Mortgages.Tests` (later): the Mortgages project and the mortgage controllers.
- Teammates' test projects run in the same command but are **not** gated, because they don't reference `coverlet.msbuild`.
- The threshold is never lowered, and real logic is never excluded to pass. Only generated EF migrations are excluded.

## Alternatives considered

| Option | Why not |
|---|---|
| **Local gate only** | Invisible to reviewers and easy to forget. |
| **Codecov / SonarCloud** | Needs an external account, repository tokens and the owner's approval. It adds dashboards we don't need for one threshold. |
| **`coverlet.collector` + ReportGenerator thresholds in CI** | Splits the configuration between test projects and the workflow. With `coverlet.msbuild`, the same `.csproj` settings drive both the local and the CI gate. |
| **One team-wide workflow that gates every project** | Each member owns their own quality bar, and other parts have no tests yet. I can't impose a threshold on teammates' code. The team can extend or merge this workflow later. |
| **Run on every PR (no path filter)** | It would spend CI minutes on PRs that can't affect Part 3. |

## Consequences

- Good:
  - Every PR touching Part 3 shows a pass/fail coverage check.
  - The same numbers can be pasted into the PR description.
  - The report artifact helps find uncovered lines.
- Bad / costs:
  - Because of the path filter, the check doesn't appear on unrelated PRs. If the repo owner makes it a **required** check in branch protection, unrelated PRs will wait for it forever. So keep it **not required**, or drop the path filter if it must be required.
  - A Contracts-only PR from a teammate also triggers it (cheap, about a minute).
- Follow-ups:
  - Phase 3+ adds Testcontainers tests. GitHub's Ubuntu runners include Docker, so no workflow change is expected.
  - `EuroGlobe.Mortgages.Tests` joins the gate automatically when it is added to the solution (Phase 4).
