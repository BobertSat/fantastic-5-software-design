# Module: Core Banking System (CBS) + Payment System
**Target File:** `modules/cbs-payments.md`  
**Shared Chapter:** Non-Functional Requirements  

---

## Part 1: CBS + Payment System Specification

### 1. Feature Table

| Feature ID | Feature Name | Actor | Priority |
| :--- | :--- | :--- | :--- |
| **CBS-01** | Account Opening & Management | Individual, Legal Entity Authorized User | Must |
| **CBS-02** | Corporate Mandate & Permission Management | Legal Entity Admin / Director | Must |
| **CBS-03** | Account Balance & Transaction History View | Customer, Corporate User | Must |
| **PAY-01** | Internal Money Transfer | Customer, Corporate User | Must |
| **PAY-02** | SEPA (Single Euro Payments Area) Transfer Processing | Customer, Corporate User | Must |
| **PAY-03** | SWIFT Transfer Processing | Customer, Corporate User | Should |
| **PAY-04** | Dual-Signature Payment Workflow | Corporate User (Accountant, Director) | Must |
| **PAY-05** | Beneficiary / Saved Recipient Management | Customer, Corporate User | Could |

---

### 2. Business Flows

#### Flow F1: Account Opening and Mandate Assignment
- **Actors:** Customer (Legal Entity Representative), CBS / Account Core Service
- **Preconditions:** Customer onboarding and KYC verification completed successfully (`kycStatus = APPROVED`).
- **Main Success Scenario:**
  1. Customer initiates an account creation request, selecting account type (`CURRENT` or `SAVINGS`) and target currency (`EUR`, `USD`, `GBP`).
  2. CBS creates an account record with status `OPEN` and generates a unique IBAN.
  3. For legal entities, the authorized administrator accesses the **Mandate Assignment** configuration context.
  4. Legal entity user defines corporate roles and assigns specific permissions (`VIEW`, `CREATE_PAYMENTS`, `APPROVE_PAYMENTS`) to authorized employee user IDs.
  5. CBS creates corresponding `AccountMandate` records binding users, account, and permissions.
  6. The client interface receives confirmation and displays the active account summary.
- **Alternative Path 1A (Mandate Assignment Failure):**
  - Step 4a: Assigned user ID is not fully KYC-verified or is inactive (`status != ACTIVE`).
  - Step 4b: CBS rejects assignment and returns an validation error: *"Assigned user must have an active and verified KYC status."*
  - Step 4c: Flow returns to Step 4.

---

#### Flow F2: Transfer Execution (Internal, SEPA, SWIFT)
- **Actors:** Customer, CBS, AML System, Notification System, Accounting Module
- **Preconditions:** Source account status is `OPEN`; user has required transfer permissions.
- **Main Success Scenario:**
  1. Customer provides source account details, recipient information (IBAN/SWIFT), transfer amount, currency, and payment purpose.
  2. CBS checks account status, transfer limits, and verifies available funds by calculating ledger balance minus active holds (`availableBalance = balance - sum(activeHolds) >= amount`).
  3. CBS creates an `AccountHold` record to reserve the transfer amount, preventing concurrent race conditions.
  4. CBS routes the transfer request to the AML module via event for automated risk and sanctions check. AML approves.
  5. If transfer amount > €500, CBS triggers Strong Customer Authentication (2FA challenge). User completes verification.
  6. CBS converts the hold into a completed ledger deduction, creates a `Transaction` (status `PENDING`), and routes payment:
     - *Internal:* Direct ledger movement within CBS.
     - *SEPA/SWIFT:* Formats and dispatches message to external payment gateway/network.
  7. CBS marks `Transaction` status as `COMPLETED` and releases the `AccountHold`.
  8. CBS emits `TRANSACTION_COMPLETED` event (consumed by Accounting for general ledger entry and Notification System to notify recipient/sender).
- **Alternative Path 2A (Insufficient Available Balance / Limit Exceeded):**
  - Step 2a: Available balance check fails (due to active holds or insufficient funds) or daily limit is exceeded.
  - Step 2b: CBS terminates execution and returns error: *"Insufficient available balance or daily limit exceeded."*
- **Alternative Path 2B (AML Flag / Hold):**
  - Step 4a: AML system flags transfer for manual compliance review.
  - Step 4b: CBS sets `Transaction` status to `FROZEN` / `PENDING_REVIEW`, keeps the `AccountHold` active, and notifies the compliance desk.

---

#### Flow F3: Legal Entity Payment with Two Signatures
- **Actors:** Accountant (Creator), Director (Approver), CBS
- **Preconditions:** Corporate account setup with dual-signature rule enabled via `AccountMandate`.
- **Main Success Scenario:**
  1. Accountant submits a transfer request through the payment interface.
  2. CBS identifies account requires dual signature; payment is created with status `PENDING_APPROVAL` (stored in `Payment`) and an initial `AccountHold` is placed to reserve funds.
  3. CBS emits `PAYMENT_PENDING_APPROVAL` event to notify authorized directors.
  4. Director reviews pending payment details in the approvals queue.
  5. Director approves and completes 2FA verification.
  6. CBS updates `Payment` status to `APPROVED` and automatically triggers Flow **F2 (Transfer Execution)**.
- **Alternative Path 3A (Director Rejection):**
  - Step 5a: Director rejects payment request and provides rejection reason.
  - Step 5b: CBS updates `Payment` status to `REJECTED`, releases the `AccountHold`, and notifies the accountant.

---

### 3. Wireframes Specification

#### Screen Overview
1. **Account Overview Dashboard:** Navigation panel, account summary list, balance indicators, and quick action toolbar.
2. **Account Details & Transaction Ledger View:** Transaction ledger table, filter toolbar, and statement export controls.
3. **Transfer Form:** Transaction creation form supporting Internal, SEPA, and SWIFT parameters with beneficiary selection.
4. **Transfer Confirmation View:** Summary view displaying breakdown of fees, net amount, and 2FA authentication prompt.
5. **Corporate Approvals Queue:** Managerial task list for inspecting, approving, or rejecting multi-signature pending payments.

#### UI Elements Matrix

| Screen Name | UI Element | Element Type | Interaction / Behavior |
| :--- | :--- | :--- | :--- |
| **Account Dashboard** | `+ Open New Account` | Action Button | Opens modal to select currency and account type. |
| **Account Details** | `Filter Ledger` | Date Range & Search Input | Filters transaction ledger rows by date, type, or amount. |
| **Transfer Form** | `Payment Protocol` | Tab Control (`Internal` \| `SEPA` \| `SWIFT`) | Switches active form layout according to the selected clearing protocol. |
| **Transfer Form** | `Beneficiary Selector` | Autocomplete Dropdown | Populates recipient name and destination IBAN from saved records. |
| **Confirmation View** | `Authorize Payment` | Primary Action Button | Triggers 2FA challenge and dispatches transfer execution flow. |
| **Approvals Queue** | `Approve` | Success Action Button | Requests 2FA authorization; on success converts payment to execution. |
| **Approvals Queue** | `Reject` | Danger Action Button | Opens input modal to capture rejection reason and cancels payment. |

---

### 4. Data Model (Class Specifications)

#### Entity Fields & Types

- **Account** *(Shared Entity)*
  * `id`: UUID (PK)
  * `customerId`: UUID (FK -> Customer)
  * `iban`: String (Unique)
  * `currency`: Enum (`EUR`, `USD`, `GBP`)
  * `balance`: Decimal(18, 2)
  * `type`: Enum (`CURRENT`, `SAVINGS`)
  * `status`: Enum (`OPEN`, `FROZEN`, `CLOSED`)
  * `openedAt`: DateTime

- **AccountHold**
  * `id`: UUID (PK)
  * `accountId`: UUID (FK -> Account)
  * `amount`: Decimal(18, 2)
  * `status`: Enum (`ACTIVE`, `RELEASED`, `CONSUMED`)
  * `referenceId`: UUID (Links to Transaction or Payment)
  * `createdAt`: DateTime

- **AccountMandate**
  * `id`: UUID (PK)
  * `accountId`: UUID (FK -> Account)
  * `userId`: UUID (FK -> User)
  * `role`: Enum (`ACCOUNTANT`, `DIRECTOR`, `ADMIN`)
  * `permissions`: List<Enum> (`VIEW`, `CREATE_PAYMENTS`, `APPROVE_PAYMENTS`)
  * `createdAt`: DateTime

- **Transaction** *(Shared Entity)*
  * `id`: UUID (PK)
  * `accountId`: UUID (FK -> Account)
  * `amount`: Decimal(18, 2)
  * `currency`: Enum (`EUR`, `USD`, `GBP`)
  * `type`: Enum (`INTERNAL`, `SEPA`, `SWIFT`, `CARD`, `LOAN`)
  * `status`: Enum (`PENDING`, `COMPLETED`, `FAILED`, `REJECTED`, `FROZEN`)
  * `createdAt`: DateTime

- **Payment**
  * `id`: UUID (PK)
  * `sourceAccountId`: UUID (FK -> Account)
  * `beneficiaryIban`: String
  * `beneficiaryName`: String
  * `amount`: Decimal(18, 2)
  * `currency`: Enum (`EUR`, `USD`, `GBP`)
  * `status`: Enum (`DRAFT`, `PENDING_APPROVAL`, `APPROVED`, `REJECTED`, `EXPIRED`)
  * `createdById`: UUID (FK -> User)
  * `approvedById`: UUID (FK -> User, Nullable)
  * `purpose`: String
  * `createdAt`: DateTime

- **Beneficiary**
  * `id`: UUID (PK)
  * `customerId`: UUID (FK -> Customer)
  * `name`: String
  * `iban`: String
  * `swiftBic`: String (Nullable)
  * `createdAt`: DateTime

#### Relationships & Cardinalities
- `Customer` **1 --- 0..*** `Account` (A customer can hold zero or multiple accounts)
- `Account` **1 --- 0..*** `AccountHold` (An account can have zero or multiple active balance holds)
- `Account` **1 --- 0..*** `AccountMandate` (An account can have multiple user permission mandates)
- `Account` **1 --- 0..*** `Transaction` (An account records zero or many transactions)
- `Account` **1 --- 0..*** `Payment` (An account acts as source for zero or many payments)
- `Customer` **1 --- 0..*** `Beneficiary` (A customer can save zero or many beneficiaries)

---

### 5. Components & Architecture Responsibilities

- **Account Core Service:** Manages balance states, ledger postings, account lifecycle rules, and mandate authorizations.
- **Payment Processing Engine:** Handles domestic/SEPA/SWIFT transaction lifecycles, dual-signature state orchestration, and 2FA integration.
- **Hold Management Service:** Manages temporary balance reservations (`AccountHold`) to prevent race conditions during concurrent transaction requests.
- **Beneficiary Registry:** Stores, retrieves, and validates saved recipient profiles for quick payment initiation.
- **Payment Gateway Interface:** Communicates with external clearing systems (SEPA, SWIFT networks) for cross-institution settlement.

---

## Part 2: Shared Chapter – Non-Functional Requirements (NFR)

### NFR-1. Security
- **Session Timeout:** Automatic user session termination after **15 minutes** of continuous client inactivity.
- **Encryption Standards:** Full transport security via **TLS 1.3** for all system communications; AES-256 encryption applied to sensitive financial data at rest.
- **Authentication & SCA:** Strong Customer Authentication (PSD2 SCA compliant 2FA) enforced for user access, transactions exceeding **€500**, and corporate mandate updates.

### NFR-2. Availability & Reliability
- **System Uptime:** Core backend services and payment processing engines guaranteed at **99.9%** annual availability (maximum scheduled downtime < 9 hours per year).
- **Fault Tolerance:** Distributed high-availability clustering across redundant cloud zones with a zero data loss recovery point objective ($RPO = 0$) for transactional ledgers.

### NFR-3. Performance & Scalability
- **UI Responsiveness:** Client interface initial view load time under **1.0 second**; table data grid rendering for up to **10,000 transaction rows** in under **200ms**.
- **API Response Time:** Payment processing and validation logic completed within **< 2.0 seconds** under heavy concurrency (tested up to **1,000 TPS**).

### NFR-4. Supported Operating Environments
- **Client Platforms:** Cross-platform support including responsive web browsers, mobile operating systems (iOS/Android), and native desktop environments.

### NFR-5. Localization & Languages
- **Languages Supported:** Internationalization support for Lithuanian (**LT**) and English (**EN**).
- **Formatting Standards:** Strict adherence to ISO 8601 date/time formatting and localized currency/IBAN presentation.

### NFR-6. Regulatory & Compliance
- **GDPR Compliance:** Full compliance with data privacy laws, supporting data minimization, audit trails, and automatic record purging upon account termination.
- **PSD2 Compliance:** Full alignment with Payment Services Directive 2 standards, providing secure open banking APIs and SCA enforcement mechanisms.

### NFR-7. Monetization & Pricing Model
- **Corporate Subscriptions:** Monthly recurring SaaS fees for legal entities structured by user seats and active permission mandates (€15/month base fee, €5/additional user).
- **Transaction Fees:** Per-item fee model applied to outgoing payment execution (€0.20 per SEPA Instant transfer, €5.00 base + FX markup for SWIFT transfers).
- **Enterprise Add-ons:** Premium flat-rate fees billed for advanced corporate features like multi-signature approval workflows and automated batch clearing services.