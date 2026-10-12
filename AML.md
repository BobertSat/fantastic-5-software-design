# AML System

Owner: Person 5. Feature IDs use the prefix AML.

The AML (Anti-Money Laundering) system checks customer transactions for signs of money laundering and terrorist financing. It helps compliance officers investigate suspicious cases and report them to the Financial Crime Investigation Service (FNTT), Lithuania's Financial Intelligence Unit. Banks are required by law to do this.

AML connects to the rest of the bank in two ways. Before a transaction is executed, the Payment system and Card management ask AML for a decision and wait for the answer. After a transaction is completed, AML receives the transaction event and looks for patterns across several transactions.

Module boundaries:

- Sanctions and PEP screening during onboarding belongs to KYC (Person 1). AML uses the customer's risk level that KYC sets, but does not repeat the check.
- AML does not send messages to customers itself. It publishes events, and the Notification system sends the messages.

FNTT is an external system. It is described as an interface in section 7, so it can be replaced with a mock during implementation.

## 1. Feature list

| ID | Feature | Actor | Priority |
|---|---|---|---|
| AML-01 | Before an outgoing payment or card transaction is executed, the system checks it against the active rules and returns ALLOW or HOLD. | Payment system, Card management | Must |
| AML-02 | The system returns HOLD when the risk score is 70 or more, or when a matched rule has the action HOLD. | System | Must |
| AML-03 | The system returns HOLD for every new outgoing transaction of a customer who has an open case. | System | Must |
| AML-04 | The system checks every completed transaction against rules that look at several transactions (structuring) within 5 minutes. | System | Must |
| AML-05 | The system creates an alert with a risk score for every transaction that matches at least one rule. | System | Must |
| AML-06 | The AML officer sees the alerts in the AML task queue, sorted by risk score and filtered by status, rule, score and date. | AML officer | Must |
| AML-07 | The AML officer takes an alert. After that, no other officer can take it. | AML officer | Must |
| AML-08 | The AML officer sees the case details: triggered rules, transaction, customer risk level, 90-day history and earlier cases. | AML officer | Must |
| AML-09 | The AML officer releases a held transaction, and the transaction is executed. A comment is required. | AML officer | Must |
| AML-10 | The AML officer reports a case to FNTT. The transaction is rejected, and the FNTT reference is saved. A comment is required. | AML officer | Must |
| AML-11 | The customer is never shown that a report to FNTT was made. | System | Must |
| AML-12 | The system records every action on cases (employee, time, action, values before and after). Records cannot be changed or deleted. | System | Must |

The rules are loaded from the default data in section 6. There is no screen for editing them.

## 2. Business flows

### F1. Screen a transaction

Actors: Payment system, Card management, AML system.

Preconditions: at least one active AmlRule exists, and the Customer has a KYC risk level (LOW, MEDIUM or HIGH).

Basic course:

1. The Payment system creates a Transaction with status PENDING and sends AML a screening request.
2. The AML system loads the Customer's risk level and all active AmlRules.
3. The AML system checks the Transaction against each AmlRule. The default rules are:
   - R1, large amount: the amount is more than 15,000 EUR.
   - R2, structuring: in the last 24 hours, the Customer made 5 or more outgoing transfers, each below 15,000 EUR, with a total above 15,000 EUR.
   - R3, high-risk country: the counterparty's country is on the high-risk country list. The list is stored as configuration. For testing, use KP and IR.
4. The AML system finds no matching rule and returns ALLOW.
5. The Payment system executes the Transaction.

Alternate courses:

- A1. A rule matches, but the risk score is below 70 and no matched rule has the action HOLD. The risk score is the sum of the weights of the matched rules multiplied by the customer risk factor (LOW 1.0, MEDIUM 1.5, HIGH 2.0), with a maximum of 100. The AML system creates an AmlAlert and returns ALLOW. The Payment system executes the Transaction. The AmlAlert appears in the AML task queue to be reviewed afterwards.
- A2. The risk score is 70 or more, or a matched rule has the action HOLD. The AML system creates an AmlAlert and returns HOLD. The Payment system sets the Transaction status to ON_HOLD. The investigation continues in F2.
- A3. The Customer already has an open AmlCase. The AML system returns HOLD, whatever the risk score, and links the new AmlAlert to that AmlCase.
- A4. The AML system does not answer within 2 seconds, or returns an error. If the amount is above 1,000 EUR, the Payment system sets the Transaction to ON_HOLD and repeats the request every minute, up to 5 times. After the fifth failure, it publishes AML_SCREENING_FAILED, and the AML system creates an AmlAlert for manual review. If the amount is 1,000 EUR or less, the Payment system executes the Transaction, and AML checks it later as in A5.
- A5. The Payment system publishes TRANSACTION_COMPLETED. The AML system checks the completed Transaction against the rules that look at several transactions. If R2 matches, the AML system creates an AmlAlert without holding anything.

Card management uses the same flow with card transactions.

Swimlane diagram: to be added.

### F2. Investigate a suspicious case

Actors: AML officer (Employee with the role COMPLIANCE), AML system, Payment system, Notification system, FNTT.

Preconditions: an AmlAlert with status NEW exists, and the AML officer is logged in to the back-office.

Basic course (the activity turns out to be legitimate):

1. The AML officer clicks AML queue in the back-office menu. The system shows the AML task queue (W-AML-01), sorted by risk score.
2. The AML officer clicks Take on an AmlAlert.
3. The system creates an AmlCase with status IN_REVIEW, assigns it to the AML officer, and links this AmlAlert and any other NEW AmlAlerts of the same Customer to it. It opens Case details (W-AML-02).
4. The AML officer reviews the triggered rules, the Transaction, the Customer panel and the 90-day history.
5. The AML officer types a comment, clicks Release and confirms.
6. The system closes the AmlCase as CLOSED_FALSE_POSITIVE, publishes TRANSACTION_RELEASED, and returns the AML officer to the AML task queue.
7. The Payment system executes the Transaction.

Every action in this flow is written to the audit log.

Alternate courses:

- A1. The suspicion is confirmed. At step 5, the AML officer types a comment and clicks Report to FNTT. In the dialog, the AML officer types the reason, selects the related Transactions and clicks Submit. The system sends the report to FNTT, saves the returned reference, closes the AmlCase as CLOSED_REPORTED and publishes TRANSACTION_REJECTED. The Payment system sets the Transaction to REJECTED, and the Customer receives the message "Your payment could not be executed. Please contact the bank." Nothing the Customer can see mentions the report, because AML law prohibits this (tipping-off).
- A2. FNTT does not answer. At A1, the system shows "FNTT is unavailable. The report was not sent. Please try again." The AmlCase stays IN_REVIEW, and the Transaction stays ON_HOLD.
- A3. Another officer took the AmlAlert first. At step 2, the system shows "This alert has already been taken" and reloads the AML task queue.

Swimlane diagram: to be added.

## 3. Non-functional requirements

These apply to this module. The requirements common to the whole system are in the Non-functional requirements chapter.

| ID | Requirement |
|---|---|
| AML-NFR-01 | 95% of screening decisions are returned within 500 ms, and every decision within 2 seconds. |
| AML-NFR-02 | If AML does not answer, no outgoing transaction above 1,000 EUR is executed until it has been checked. |
| AML-NFR-03 | A new AmlAlert appears in the AML task queue within 10 seconds after it is created. |
| AML-NFR-04 | Cases, alerts, reports and audit records are kept for 8 years after the case is closed, as required by the Lithuanian AML law. |
| AML-NFR-05 | Only employees with the role COMPLIANCE can open AML screens and endpoints. Others receive 403 Access denied. |

## 4. High-level architecture

The module consists of these packages. The package and class diagrams for the whole system are in the Architecture chapter.

| Package | Responsibility |
|---|---|
| aml.screening | Handles the synchronous screening request and returns ALLOW or HOLD within 2 seconds. |
| aml.rules | Loads the rules and checks a transaction and the customer's recent history against them. |
| aml.monitoring | Listens to TRANSACTION_COMPLETED and CARD_TRANSACTION_AUTHORIZED and finds patterns across several transactions. |
| aml.cases | Manages alerts and cases and the back-office actions on them. |
| aml.fiu | Builds the FNTT report and sends it through the FNTT interface. |
| audit | Writes and reads the audit log. Other modules can also use it for their back-office actions. |

Package diagram: to be added.

## 5. UI wireframes

Both screens are back-office screens for employees with the role COMPLIANCE.

Wireframe images: to be added.

### W-AML-01 AML task queue

The officer's starting screen. It shows a table of alerts, filters, and a button for taking an alert.

| # | Element | Type | What happens when it's used |
|---|---|---|---|
| 1 | Tabs: New, In review | Tabs | Filters the table by status |
| 2 | Filters: rule, score range, date range | Dropdowns | Reloads the table with the selected filters |
| 3 | Search | Text input | Searches by customer name, IBAN or alert ID |
| 4 | Alert row: score, alert ID, customer, rule, amount, age, HOLD label | Table row | Opens W-AML-02 for the case, if the alert already belongs to one |
| 5 | Score | Label | Dark for 70 and above, outlined for 40 to 69, dashed for below 40. Clicking the column header sorts the table |
| 6 | Take | Primary button | Assigns the alert to the officer, creates a case and opens W-AML-02 |

### W-AML-02 Case details

| # | Element | Type | What happens when it's used |
|---|---|---|---|
| 1 | Header: case ID, status, assigned officer, how long the transaction has been held | Text | Read only |
| 2 | Triggered rules with weights and the total score | List | Read only |
| 3 | Transaction: amount, counterparty, type, time, status | Panel | Read only |
| 4 | Customer: name, type, KYC risk level, customer since, earlier cases | Panel | The "Open profile" link opens the CRM customer profile (Person 1) |
| 5 | Transaction history for 90 days | Bar chart | Hovering over a bar shows the daily total |
| 6 | Comment | Text area, required | The decision buttons are disabled until a comment is written |
| 7 | Release | Secondary button | Opens a confirmation dialog. After confirmation, the case closes as a false positive and the officer goes back to W-AML-01 |
| 8 | Report to FNTT | Destructive button | Opens the Report to FNTT dialog with a "Reason for suspicion" text area, checkboxes for related Transactions, and the buttons Cancel and Submit. After Submit, the case closes as reported and the officer goes back to W-AML-01. If FNTT does not answer, the dialog shows an error and stays open |
| 9 | Audit trail | Collapsible list | Shows every action on this case with time and employee |

### Screen transitions

| From | Action | To |
|---|---|---|
| Back-office menu | AML queue | W-AML-01 |
| W-AML-01 | Take, or click a row that has a case | W-AML-02 |
| W-AML-02 | Release or Report to FNTT completed | W-AML-01 |
| W-AML-02 | Open profile | CRM customer profile (Person 1) |

## 6. Data model

User, Customer and Transaction are shared entities owned by other modules and are only referenced here. Employee is a shared entity owned by this module.

### Employee (shared)

| Field | Type | Notes |
|---|---|---|
| id | UUID | Primary key |
| userId | UUID | References User |
| role | EmployeeRole | |

### AmlRule

| Field | Type | Notes |
|---|---|---|
| id | UUID | Primary key |
| code | String | Unique, for example R1 |
| name | String | |
| type | RuleType | |
| threshold | Decimal | Amount in EUR |
| periodHours | Integer | Only for VELOCITY rules, otherwise null |
| minCount | Integer | Only for VELOCITY rules, otherwise null |
| weight | Integer | 1 to 100 |
| action | RuleAction | |
| active | Boolean | |

### AmlAlert

| Field | Type | Notes |
|---|---|---|
| id | UUID | Primary key |
| transactionId | UUID | References Transaction |
| customerId | UUID | References Customer |
| caseId | UUID | References AmlCase. Null until the alert is taken |
| riskScore | Integer | 0 to 100 |
| status | AlertStatus | |
| createdAt | DateTime | |

### AmlAlertRule

This table links an alert to the rules that triggered it.

| Field | Type | Notes |
|---|---|---|
| alertId | UUID | References AmlAlert |
| ruleId | UUID | References AmlRule |

### AmlCase

| Field | Type | Notes |
|---|---|---|
| id | UUID | Primary key |
| customerId | UUID | References Customer |
| assignedTo | UUID | References Employee |
| status | CaseStatus | |
| comment | String | Required before a decision |
| fiuReportRef | String | Reference number from FNTT, null until reported |
| reportedAt | DateTime | Null until reported |
| openedAt | DateTime | |
| closedAt | DateTime | Null while the case is open |

### AuditLogEntry

Entries are only ever added, never changed or deleted. Other modules can use the same log for their back-office actions.

| Field | Type | Notes |
|---|---|---|
| id | UUID | Primary key |
| employeeId | UUID | References Employee. Null for system actions |
| action | String | For example CASE_TAKEN, CASE_RELEASED, CASE_REPORTED |
| entityType | String | For example AmlCase |
| entityId | UUID | |
| details | JSON | Values before and after the change |
| ipAddress | String | |
| createdAt | DateTime | |

### Relationships

| Relationship | Cardinality |
|---|---|
| User to Employee | 1 to 0..1 |
| Employee to AmlCase (assigned) | 1 to 0..* |
| Employee to AuditLogEntry | 1 to 0..* |
| Customer to AmlCase | 1 to 0..* |
| Transaction to AmlAlert | 1 to 0..* |
| AmlCase to AmlAlert | 0..1 to 1..* (an alert has no case until it is taken) |
| AmlAlert to AmlRule (through AmlAlertRule) | many to many, at least one rule per alert |

### Enums

| Enum | Values |
|---|---|
| EmployeeRole | SUPPORT, COMPLIANCE, LOAN_OFFICER, ACCOUNTANT, ADMIN |
| RuleType | AMOUNT_THRESHOLD, VELOCITY, HIGH_RISK_COUNTRY |
| RuleAction | ALERT, HOLD |
| AlertStatus | NEW, IN_CASE, CLOSED |
| CaseStatus | IN_REVIEW, CLOSED_FALSE_POSITIVE, CLOSED_REPORTED |
| ScreeningDecision | ALLOW, HOLD |

Transaction.status (owned by Person 2) must include ON_HOLD and REJECTED for this module to work.

### Default data

Every installation starts with these three rules:

| Code | Name | Type | Threshold | Period | Min count | Weight | Action |
|---|---|---|---|---|---|---|---|
| R1 | Large amount | AMOUNT_THRESHOLD | 15000 | | | 40 | ALERT |
| R2 | Structuring | VELOCITY | 15000 | 24 | 5 | 50 | ALERT |
| R3 | High-risk country | HIGH_RISK_COUNTRY | | | | 70 | HOLD |

## 7. API contract

All endpoints use JSON. Amounts are decimal strings with two decimal places, for example "15000.00". Times are in ISO 8601 UTC, for example "2026-10-11T10:42:00Z".

Every error has the same body:

```json
{ "error": { "code": "COMMENT_REQUIRED", "message": "A comment is required before a decision." } }
```

Back-office endpoints need an employee token with the role COMPLIANCE. Internal endpoints are only reachable between services.

### Screening (internal)

POST /internal/aml/screenings

Request:

```json
{
  "transactionId": "uuid",
  "customerId": "uuid",
  "accountId": "uuid",
  "source": "PAYMENT",
  "direction": "OUTGOING",
  "amount": "15200.00",
  "currency": "EUR",
  "counterpartyIban": "DE89370400440532013000",
  "counterpartyCountry": "DE",
  "createdAt": "2026-10-11T10:42:00Z"
}
```

The source field is PAYMENT or CARD, and direction is OUTGOING or INCOMING. For card transactions, counterpartyIban is null and counterpartyCountry is the merchant's country.

Response 200:

```json
{ "decision": "HOLD", "alertId": "uuid", "riskScore": 80 }
```

If decision is ALLOW and no rule matched, alertId is null and riskScore is 0.

Errors: 400 VALIDATION_ERROR when a required field is missing. The caller treats a timeout or a 5xx response as described in F1, alternate course A4.

### Back-office endpoints

| Method and path | Purpose | Request body | Success response | Errors |
|---|---|---|---|---|
| GET /api/v1/aml/alerts | List alerts for the queue | Query: status, ruleCode, minScore, maxScore, from, to, q, page, size | 200, page of alerts | |
| POST /api/v1/aml/alerts/{alertId}/take | Take an alert and create a case | none | 201, the case | 409 ALERT_ALREADY_TAKEN |
| GET /api/v1/aml/cases/{caseId} | Case details | none | 200, case details | 404 NOT_FOUND |
| POST /api/v1/aml/cases/{caseId}/release | Release the transaction | comment | 200, the case | 422 COMMENT_REQUIRED, 409 CASE_CLOSED |
| POST /api/v1/aml/cases/{caseId}/fiu-reports | Report to FNTT | comment, reason, relatedTransactionIds | 201, fiuReportRef and reportedAt | 422 COMMENT_REQUIRED, 409 CASE_CLOSED, 502 FIU_UNAVAILABLE |

An alert in the list has the fields id, riskScore, status, customerId, customerName, ruleCodes, amount, currency, held (true if the transaction is ON_HOLD), createdAt and caseId.

Case details contain the case fields from section 6, plus:

- alerts, each with its rules;
- transaction: id, amount, currency, counterparty, type, status, createdAt;
- customer: id, name, type, kycRiskLevel, customerSince, previousCaseCount;
- history: a list of date and total for the last 90 days;
- audit: a list of time, employee name and action.

If a case is already closed, every action returns 409 CASE_CLOSED, and the screen shows the message "This case has already been closed".

### Events

Events use the common envelope with eventId, eventType, occurredAt, source and payload. If the same eventId is received twice, the receiver ignores the second one.

Events AML receives:

| Event | From | Payload fields used | Used for |
|---|---|---|---|
| TRANSACTION_COMPLETED | Payments | transactionId, customerId, accountId, amount, currency, direction, counterpartyCountry | Monitoring (F1 A5) |
| CARD_TRANSACTION_AUTHORIZED | Cards | transactionId, customerId, amount, currency, merchantCountry | Monitoring (F1 A5) |
| AML_SCREENING_FAILED | Payments | transactionId, customerId | Alert for manual review (F1 A4) |

Events AML publishes:

| Event | Payload | Who reacts |
|---|---|---|
| TRANSACTION_RELEASED | transactionId, caseId | Payments executes the transaction |
| TRANSACTION_REJECTED | transactionId, caseId, userId | Payments sets REJECTED, Notification system sends the neutral message |

TRANSACTION_REJECTED does not contain the reason, so no other module can show the customer that a report was made.

### FNTT interface

This can be a mock during implementation.

POST {FNTT_URL}/reports with caseId, customer (name, personal or company code), transactions (id, amount, date, counterparty) and reason. The response contains reference. The mock returns a random reference such as FNTT-2026-000123.
