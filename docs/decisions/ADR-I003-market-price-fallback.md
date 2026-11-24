# ADR-I003: Market price with timeout and last-known-price fallback

- **Status:** Proposed
- **Date:** 2026-11-24
- **Area / owner:** Investments / member 5
- **Spec deviation:** Gap filled (the spec does not say where prices come from or what happens if they are unavailable)

## Context

- Spec (`first-lab-assignment.pdf`):
  - §4.4, Fig. 4 say round-ups are invested asynchronously.
  - §3.3 requires investment components to fail gracefully.
  - The spec does not name a market data source or define behaviour when prices are unavailable.
- Our commitment (`EuroGlobe Bank Implementation.pdf`): prices for Bitcoin and the S&P 500 ETF are fetched from public market data APIs, "with the last known price used if the API is unavailable".
- Free public APIs can be slow, rate-limited or down.

## Decision

- `IMarketPriceService.GetCurrentPriceAsync(symbol)` is the only way the module gets a price.
- It calls the matching `IPriceSource` with a **3-second timeout**.
- On success, the price is saved as a `PriceQuote` (with source and time from `IClock`) and returned with `IsStale = false`.
- On failure or timeout, it returns the **latest stored quote** with `IsStale = true`.
- If no quote has ever been stored, it throws `PriceUnavailableException`.
- If the **caller** cancels the request, the cancellation is passed on and no old price is returned.
- Assets are listed in `IAssetCatalog` (BTC, SPY).

## Alternatives considered

| Option | Why not |
|---|---|
| Fail when the API fails | A round-up investment or the dashboard would fail just because a free API is down. |
| Retry with backoff | Slower, and it still fails if the API is down. It may be added later inside a price source. |
| Only read prices cached by a background job | Prices could be minutes old even when the API works. The polling job will be added as well, to keep the stored price fresh. |

## Consequences

- Good:
  - Investing and the dashboard keep working when an API is down.
  - The UI can show the price time and a "stale" marker.
- Bad / costs:
  - A buy may use an older price. This is acceptable for a demo bank and visible through `IsStale`.
- Follow-ups:
  - CoinGecko and Stooq price sources, plus a USD→EUR rate.
  - A price polling job (`IRecurringJob`).
  - Move `InMemoryPriceQuoteStore` to `InvestmentsDbContext` (ADR-M001).