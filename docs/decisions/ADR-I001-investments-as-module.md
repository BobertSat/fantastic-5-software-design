# ADR-I001: Investments as an isolated module, not a separate microservice

- **Status:** Proposed
- **Date:** 2026-11-24
- **Area / owner:** Investments + Dashboard / member 5
- **Spec deviation:** Yes (interprets an ambiguous point in the spec)

## Context

- Spec (`first-lab-assignment.pdf`):
  - §5, Fig. 5 and §5.1 name an **"InvestmentMicroservice"** that listens for payment events on the EventBus.
  - §6.9, Fig. 14 describe event-driven processing of round-ups.
  - §3.3 requires **fault isolation**: investment failures must not affect payments.
  - The spec never says whether this is a separately deployed service.
- Our evaluation (`Evaluation.pdf`, criterion 5, Weak): "It is unclear whether 'InvestmentMicroservice' is a separate deployable service or a module of a monolith."
- Our commitment (`EuroGlobe Bank Implementation.pdf`): round-ups are invested **asynchronously after the payment completes**, and "Investment failures will not affect payments, since investments run asynchronously."
- The team builds one ASP.NET Core application (`EuroGlobe.Api`). Modules talk only through `EuroGlobe.Contracts`, and the event bus is in-process (`InMemoryEventBus`, PR #11).

## Decision

- Investments is its own project, `EuroGlobe.Investments`, inside the modular monolith.
- It depends only on `EuroGlobe.Contracts`, never on another module's project.
- It receives work only through the `PaymentCompleted` event via `IEventBus`, which keeps the asynchronous, decoupled behaviour of the spec's microservice.
- It exposes its features to other modules only through Contracts interfaces (e.g. `IMarketPriceService`), and to clients through its own `/api/investments/...` endpoints.
- Its data lives in its own `investments` schema (ADR-M001), with no foreign keys to other modules.

## Alternatives considered

| Option | Why not |
|---|---|
| Separate deployable microservice with a message broker | Needs separate hosting, a broker (e.g. RabbitMQ), network failure handling and a second deploy pipeline. That's too much infrastructure for the course, and the team agreed on one application. |
| Investments code inside the Payments module | It couples payments to investing. A bug in investing could break payments, which violates §3.3 fault isolation. |

## Consequences

- Good:
  - Fault isolation is kept: payments only publish an event, and handler failures are caught by the event bus.
  - Because the boundary is already an event plus Contracts interfaces, the module can later be extracted into a real microservice with little change.
  - One build, one run, one deploy.
- Bad / costs:
  - It cannot be scaled or deployed independently (§3.2 horizontal elasticity is not demonstrated).
  - With an in-memory event bus, events published while the app is down are lost.
- Follow-ups:
  - `PaymentCompletedHandler` must be idempotent (dedupe by transaction id).
  - Confirm this decision with the team at the next call, then mark it Accepted.