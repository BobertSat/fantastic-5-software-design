# ADR-M002: camelCase JSON and enum values as names

- **Status:** Proposed
- **Date:** 2026-11-28
- **Area / owner:** Smart Mortgage API / member 3
- **Spec deviation:** Yes

## Context

The spec's API examples use snake_case property names and lowercase string values (§9.3):
- `"property_value"`, `"term_months"`, `"rate_type": "fixed|variable"`,
- `"status": "pending_review"`, `"monthly_payment": 1043.17`.

The state names disagree across the document: `PENDING_REVIEW` (§2.2), `UnderReview` (Fig. 8, §8.3), `pending_review` (§9.3) and `PENDING` (SQL). Our evaluation lists this under criterion 7: "State values are never listed, and they disagree across sections."

ASP.NET Core serializes property names in camelCase by default. Without a converter, it writes enums as **numbers**, which would make `state: 3` meaningless to API clients and the React screens.

## Decision

- **Property names are camelCase**, the ASP.NET Core default: `propertyValue`, `termMonths`, `monthlyPayment`.
- **Enum values are their C# names**, exactly as in Fig. 8: `"UnderReview"`, `"Approved"`, `"Rejected"`, `"Active"`, `"Delinquent"`, `"Closed"`. Likewise `"Fixed"`/`"Variable"`, `"Primary"`/`"BuyToLet"`, `"Repaid"`/`"Terminated"`, `"Scheduled"`/`"Paid"`/`"Overdue"`.
- Each mortgage enum carries `[JsonConverter(typeof(JsonStringEnumConverter<T>))]`, so its JSON form is fixed **wherever** it is serialized. This needs no change to the global JSON settings in `Program.cs`, which member 2 owns.
- The database also stores enums by name (`varchar(20)`), so data, API and spec Fig. 8 use the same words.
- **Recommendation to the team:** add `JsonStringEnumConverter` globally in `AddControllers().AddJsonOptions(...)` so every module behaves the same. My per-enum attributes stay correct either way.

## Alternatives considered

| Option | Why not |
|---|---|
| **snake_case as in §9.3** | Every module and the React app would need a custom naming policy. The spec itself isn't consistent, and the state values it shows (`pending_review`) don't match its own state diagram. |
| **Enums as numbers** (default) | Unreadable for clients, and fragile: reordering an enum silently changes the API. |
| **A global enum converter set from my module** | It would silently change every teammate's API output from a mortgage registration call. That is a team decision, so I only recommend it. |

## Consequences

- Good:
  - Readable, stable values that match Fig. 8 one-to-one.
  - No surprise for other modules.
  - The React client can use the values directly.
- Bad / costs:
  - It deviates from §9.3's snake_case. The spec's request examples must be read with camelCase names.
  - Every new mortgage enum must carry the attribute. A test checks the existing ones.
