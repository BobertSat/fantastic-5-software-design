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
* **Actors:** Customer (Legal Entity Representative), CBS / Account Core Service
* **Preconditions:** Customer onboarding and KYC verification completed successfully (`kycStatus = APPROVED`).
* **Main Success Scenario:**
  1. Customer opens the account module in the desktop application, selects account type (`CURRENT` or `SAVINGS`) and target currency (`EUR`, `USD`, `GBP`).
  2. CBS creates an account record with status `OPEN` and generates a unique IBAN.
  3. For legal entities, the primary user opens the **Mandate Assignment** tab within the desktop application window.
  4. Legal entity user defines corporate roles and assigns specific permissions (`VIEW`, `CREATE_PAYMENTS`, `APPROVE_PAYMENTS`) to authorized employee user IDs.
  5. CBS creates corresponding `AccountMandate` records binding users, account, and permissions.
  6. Desktop application displays confirmation toast and updates the active account sidebar/grid.
* **Alternative Path 1A (Mandate Assignment Failure):**
  * Step 4a: Assigned user ID is not fully KYC-verified or is inactive (`status != ACTIVE`).
  * Step 4b: Desktop application displays inline validation error banner: *"Assigned user must have an active and verified KYC status."*
  * Step 4c: Flow returns to Step 4.

---

#### Flow F2: Transfer Execution (Internal, SEPA, SWIFT)
* **Actors:** Customer, CBS, AML System, Notification System, Accounting Module
* **Preconditions:** Source account status is `OPEN`; user has required transfer permissions.
* **Main Success Scenario:**
  1. Customer selects source account from the desktop side panel, opens Transfer Form, enters recipient details (IBAN/SWIFT), transfer amount, currency, and payment purpose.
  2. CBS checks account status, transfer limits, and verifies available balance (`balance >= amount`).
  3. CBS routes transfer request to the AML module via event for automated risk and sanctions check. AML approves.
  4. If transfer amount > €500, desktop application prompts a modal dialog for 2FA / Hardware Security key verification. User completes 2FA.
  5. CBS deducts amount from source account balance, creates `Transaction` (status `PENDING`), and routes payment:
     * *Internal:* Direct ledger movement within CBS.
     * *SEPA/SWIFT:* Formats and dispatches message to external payment gateway/network.
  6. CBS marks `Transaction` status as `COMPLETED`.
  7. CBS emits `TRANSACTION_COMPLETED` event (consumed by Accounting for general ledger entry and Notification System to send desktop/email notification).
* **Alternative Path 2A (Insufficient Funds / Limit Exceeded):**
  * Step 2a: Balance check fails or daily limit is exceeded.
  * Step 2b: Desktop application displays error dialog: *"Insufficient funds or daily limit exceeded."* Transaction terminates.
* **Alternative Path 2B (AML Flag / Hold):**
  * Step 3a: AML system flags transfer for manual compliance review.
  * Step 3b: CBS sets `Transaction` status to `FROZEN` / `PENDING_REVIEW` and desktop application updates table badge status.

---

#### Flow F3: Legal Entity Payment with Two Signatures
* **Actors:** Accountant (Creator), Director (Approver), CBS
* **Preconditions:** Corporate account setup with dual-signature rule enabled via `AccountMandate`.
* **Main Success Scenario:**
  1. Accountant logs into desktop client, navigates to Transfer Form, completes payment details, and submits.
  2. CBS identifies account requires dual signature; payment is created with status `PENDING_APPROVAL` (stored in `Payment`).
  3. CBS emits `PAYMENT_PENDING_APPROVAL` event to notify the Director.
  4. Director logs into desktop application, views **Payments Pending Approval** sidebar view, and reviews transaction details.
  5. Director clicks Approve and completes desktop 2FA dialog verification.
  6. CBS updates `Payment` status to `APPROVED` and automatically initiates Flow **F2 (Transfer Execution)**.
* **Alternative Path 3A (Director Rejection):**
  * Step 5a: Director clicks Reject button in desktop window and provides rejection reason in prompt modal.
  * Step 5b: CBS updates `Payment` status to `REJECTED` and notifies Accountant.

---

### 3. Wireframes Specification (Desktop Client Layout)

#### Screen Overview
1. **Account Overview Dashboard (Main Window):** Left navigation panel, account data grid, dynamic balance widgets, and top toolbar quick actions.
2. **Account Details & Transaction Ledger View:** Full-width transaction table, multi-column sorting/filtering toolbar, and export buttons (CSV/PDF).
3. **Transfer Form (Modal / Main View):** Multi-tab transfer window (Internal/SEPA/SWIFT), saved beneficiary auto-complete dropdown, fee display panel.
4. **Transfer Confirmation Modal Dialog:** Floating confirmation overlay displaying fee summary, beneficiary details, and 2FA passcode input.
5. **Corporate Approvals Desk (Desktop View):** Grid queue for legal entity approvers/directors to inspect, bulk-select, sign, or reject pending payments.

#### UI Elements Matrix

| Window / Screen Name | UI Element | Element Type | Interaction / Behavior |
| :--- | :--- | :--- | :--- |
| **Account Dashboard** | `+ Open New Account` | Header Toolbar Button | Opens modal window to select currency and account type. |
| **Account Details** | `Filter Ledger` | Data Grid Search Bar & Date Picker | Filters desktop data grid by date range, type, or amount. |
| **Transfer Window** | `Payment Protocol` | Desktop Tab Bar (`Internal` \| `SEPA` \| `SWIFT`) | Dynamic tab control that adjusts visible form fields per protocol. |
| **Transfer Window** | `Beneficiary Selector` | Searchable Combo Box | Autocompletes recipient name and IBAN from saved contacts. |
| **Confirmation Modal** | `Execute Payment` | Primary Modal Action Button | Triggers 2FA challenge modal and submits payment flow F2. |
| **Approvals Desk** | `Approve Selected` | Success Toolbar Button | Requests 2FA input; on success updates state and triggers transfer. |
| **Approvals Desk** | `Reject Selected` | Danger Toolbar Button | Opens dialog window to capture rejection reason and declines payment. |

---

### 4. Data Model (Class Specifications)

#### Entity Fields & Types

* **Account** *(Shared Entity)*
  * `id`: UUID (PK)
  * `customerId`: UUID (FK -> Customer)
  * `iban`: String (Unique)
  * `currency`: Enum (`EUR`, `USD`, `GBP`)
  * `balance`: Decimal(18, 2)
  * `type`: Enum (`CURRENT`, `SAVINGS`)
  * `status`: Enum (`OPEN`, `FROZEN`, `CLOSED`)
  * `openedAt`: DateTime

* **AccountMandate**
  * `id`: UUID (PK)
  * `accountId`: UUID (FK -> Account)
  * `userId`: UUID (FK -> User)
  * `role`: Enum (`ACCOUNTANT`, `DIRECTOR`, `ADMIN`)
  * `permissions`: List<Enum> (`VIEW`, `CREATE_PAYMENTS`, `APPROVE_PAYMENTS`)
  * `createdAt`: DateTime

* **Transaction** *(Shared Entity)*
  * `id`: UUID (PK)
  * `accountId`: UUID (FK -> Account)
  * `amount`: Decimal(18, 2)
  * `currency`: Enum (`EUR`, `USD`, `GBP`)
  * `type`: Enum (`INTERNAL`, `SEPA`, `SWIFT`, `CARD`, `LOAN`)
  * `status`: Enum (`PENDING`, `COMPLETED`, `FAILED`, `REJECTED`, `FROZEN`)
  * `createdAt`: DateTime

* **Payment**
  * `id`: UUID (PK)
  * `sourceAccountId`: UUID (FK -> Account)
  * `beneficiaryIban`: String
  * `beneficiaryName`: String
  * `amount`: Decimal(18, 2)
  * `currency`: Enum (`EUR`, `USD`, `GBP`)
  * `status`: Enum (`DRAFT`, `PENDING_APPROVAL`, `APPROVED`, `REJECTED`, `EXPIRED`)
  * `createdById`: UUID (FK -> User)
  * `approvedById`: UUID (FK -> User, Nullable)
  * `createdAt`: DateTime
  * `purpose` : String

* **Beneficiary**
  * `id`: UUID (PK)
  * `customerId`: UUID (FK -> Customer)
  * `name`: String
  * `iban`: String
  * `swiftBic`: String (Nullable)
  * `createdAt`: DateTime

#### Relationships & Cardinalities
* `Customer` **1 --- 0..*** `Account` (A customer can hold zero or multiple accounts)
* `Account` **1 --- 0..*** `AccountMandate` (An account can have multiple user permission mandates)
* `Account` **1 --- 0..*** `Transaction` (An account records zero or many transactions)
* `Account` **1 --- 0..*** `Payment` (An account acts as source for zero or many payments)
* `Customer` **1 --- 0..*** `Beneficiary` (A customer can save zero or many beneficiaries)

---

### 5. Components & Architecture Responsibilities

* **Account Core Service:** Manages balance states, account creation, account status modifications, and mandate authorizations.
* **Payment Processing Engine:** Handles domestic/SEPA/SWIFT routing logic, dual-signature workflows, and 2FA validation triggers.
* **Beneficiary Registry:** Stores, retrieves, and updates saved recipient details for fast payment creation.
* **Payment Gateway Interface:** Interacts with external financial clearing networks (SEPA, SWIFT) for external transaction execution.

---

## Part 2: Shared Chapter – Non-Functional Requirements (NFR)

### NFR-1. Security
* **Session Timeout:** Automatic desktop application lock / session termination after **15 minutes** of local user inactivity (keyboard/mouse idle).
* **Encryption Standards:** Full transport encryption using **TLS 1.3** for desktop-to-server communication; local storage and database cache encrypted at rest using **AES-256**.
* **Authentication & SCA:** Strong Customer Authentication (PSD2 SCA compliant 2FA) required for app login, transfer execution over **€500**, and corporate mandate modifications.

### NFR-2. Availability & Reliability
* **System Uptime:** Core API and backend payment server uptime guaranteed at **99.9%** annually (maximum scheduled maintenance downtime < 9 hours per year).
* **Offline Resilience:** Desktop application gracefully handles network disconnection with local cached read-only view state; automatic reconnect mechanism when internet connection recovers.

### NFR-3. Performance & Scalability
* **Desktop App Responsiveness:** Main window UI load time **< 1.0 second**; data grid rendering for up to **10,000 transaction rows** in under **200ms**.
* **API Response Time:** Backend payment execution completed within **< 2.0 seconds** under peak operational loads (up to **1,000 TPS** server capacity).

### NFR-4. Supported Desktop Operating Systems
* **Windows:** Native Windows desktop application supporting **Windows 10 / 11 (64-bit)**.

### NFR-5. Localization & Languages
* **Languages Supported:** Full desktop UI internationalization (i18n) for Lithuanian (**LT**) and English (**EN**).
* **Desktop Regional Settings:** Respects OS system locale settings for date/time formatting (ISO 8601) and currency symbol rendering.

### NFR-6. Regulatory & Compliance
* **GDPR Compliance:** Full compliance with GDPR requirements, including client data retention controls, audit trail logging, and local data purge mechanisms upon account closure.
* **PSD2 Compliance:** Full adherence to Payment Services Directive 2 standards, enforcing Secure Customer Authentication (SCA) for high-value operations.

### NFR-7. Monetization & Pricing Model
* **Corporate Account Subscription:** Tiered monthly subscription fees for legal entities based on user seats and active account mandates (€15/month base, €5/additional user).
* **Payment Transaction Fees:** Fee model applied to outgoing international transfers (€0.20 per SEPA Instant, €5.00 + FX markup for SWIFT transfers).
* **Corporate Premium Features:** Flat fee charged for corporate dual-signature approval workflows and batch payment processing services.