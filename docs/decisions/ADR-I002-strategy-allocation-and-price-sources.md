# ADR-I002: Strategy pattern for round-up allocation and price sources

- **Status:** Proposed
- **Date:** 2026-11-24
- **Area / owner:** Investments / member 5
- **Spec deviation:** Gap filled (the spec does not say what the Strategy pattern varies in investments)

## Context

- Spec (`first-lab-assignment.pdf`):
  - §6.1, Fig. 6 describe Strategy only for mortgage interest (fixed vs variable rate).
  - The §10 Traceability Matrix lists Strategy for the investment requirements, but no section explains what varies.
- In our implementation two things vary:
  1. **How a round-up is split between assets.** It can go 100% to one asset, or be divided by the user's percentages (`RoundUpSettings`).
  2. **Where an asset's price comes from.** Crypto and ETF prices come from different public APIs.

## Decision

- **Allocation:** `IAllocationStrategy.Allocate(amountEur)` returns a list of `AllocationResult`.
  - `SingleAssetAllocationStrategy` puts the whole amount into one asset.
  - `WeightedAllocationStrategy` splits by percent and rounds each part **down** to whole cents. The leftover cents go to the asset with the largest percent, so the parts always add up to the original amount, and zero amounts are skipped.
  - `AllocationStrategyFactory.For(RoundUpSettings)` picks the strategy: one allocation means single, more than one means weighted.
- **Price sources:** `IPriceSource` has `CanHandle(Asset)` and `GetPriceEurAsync(...)`. `MarketPriceService` uses the first source that can handle the asset (CoinGecko for crypto and Stooq for ETFs, in follow-up PRs).

## Alternatives considered

| Option | Why not |
|---|---|
| `if`/`switch` on the settings or asset type inside one service | Every new asset type or split rule would mean editing and re-testing the same class (breaks Open/Closed). |
| Strategy only for manual investments | In a manual purchase the user picks the asset, so nothing varies there and the pattern would be artificial. |

## Consequences

- Good:
  - A new split rule or price provider is a new class, and existing code is unchanged.
  - Each strategy is unit-tested on its own.
- Bad / costs: more small classes and a factory to understand.
- Follow-ups: price-source implementations come in later PRs and should be added to this ADR's Decision section.