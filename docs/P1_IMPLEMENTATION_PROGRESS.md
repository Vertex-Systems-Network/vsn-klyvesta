# P1 Implementation Progress

Status: **In Progress** - Core domain models + PaperBrokerAdapter implemented, test suites pending.

## Completed

### Domain Foundation (Klyvesta.Domain) - 17 files, ~4500 lines

#### Common Types (`Common/`)
- ✅ `DomainTypes.cs` - Core enums and interfaces
  - `IEntity`, `IObserved` interfaces
  - `BrokerResultState` enum (Success, Rejected, RetryableFailure, Unknown)
  - `OrderState` enum (9 states including UNKNOWN)
  - `Side`, `TimeInForce`, `AccountStatus`, `InvestmentMode` enums

- ✅ `MoneyTypes.cs` - Exact financial types using decimal
  - `Money` struct with currency-aware arithmetic
  - `Quantity` struct for share/unit counts
  - `Price` struct for per-share prices
  - `Percentage` struct for concentrations/allocations
  - All operators prevent floating-point errors

- ✅ `Identifiers.cs` - Strongly-typed UUID-based identifiers
  - `CustomerId`, `AccountId`, `InstrumentId`, `OrderId`, `OrderIntentId`
  - `ExecutionId`, `LedgerAccountId`, `JournalId`, `PositionId`
  - `MandateId`, `ProposalId`, `CorrelationId`, `IdempotencyKey`

#### Broker Adapter (`Broker/`)
- ✅ `BrokerAdapter.cs` - Normalized broker boundary per BROKER_ADAPTER_V1.md
  - `BrokerCapabilities` record for capability discovery
  - `BrokerResultEnvelope` with normalized result states
  - `CashBalance`, `BrokerPosition` records
  - `BrokerOrderRequest` for order submission
  - `BrokerExecution` for fill tracking
  - `IBrokerAdapter` interface
  - Exception hierarchy: `BrokerRejectedException`, `BrokerRetryableException`, `BrokerAmbiguousException`
  - **Key safety feature**: UNKNOWN state forces reconciliation, not blind retry

- ✅ `Paper/PaperBrokerAdapter.cs` - Deterministic paper broker implementation (875 lines)
  - Implements all 20 scenarios from PAPER_BROKER_SCENARIOS_V1.yaml
  - `PaperBrokerConfig` for scenario control (full/partial/rejected fills, timeouts, duplicates, etc.)
  - `PaperOrderState` internal state machine for order lifecycle
  - `PaperExecution` for simulated fills
  - Idempotency handling (PB-005): same key returns existing order
  - Kill switch support (PB-019)
  - Stale data rejection (PB-012)
  - Market closed simulation (PB-013)
  - Broker unavailable simulation (PB-014)
  - Ambiguous timeout handling (PB-007) - throws BrokerAmbiguousException
  - Multiple fill de-duplication (PB-004)
  - Partial fill handling (PB-003)
  - Cancel race handling (PB-008/PB-009)
  - Event logging for audit trail
  - Thread-safe with semaphore-based locking
  - Portfolio/position updates on fills
  - Cash balance tracking
  - T+2 settlement simulation

#### Ledger System (`Ledger/`)
- ✅ `Ledger.cs` - Immutable double-entry ledger
  - `LedgerAccount` entity with account types (Asset, Liability, Equity, Revenue, Expense)
  - `Journal` entity with commit validation (debits == credits)
  - `Posting` for individual debit/credit entries
  - `BalanceValidationResult` for pre-commit checks
  - `ILedgerService` interface
  - `LedgerInvariantException` for constraint violations
  - **Core invariant**: Journal immutable after commit, corrections via reversal

#### Order Management (`Orders/`)
- ✅ `OrderIntent.cs` - Pre-execution order entity
  - `OrderIntent` entity tracking full lifecycle
  - State machine: PendingSubmit → Submitted → Open → PartiallyFilled/Filled/Cancelled/Rejected
  - Fill tracking with average price calculation
  - References to AI proposal, mandate, risk/compliance checks
  - `IOrderIntentService` interface
  - **Safety**: Cannot modify after terminal state

#### Risk Governor (`Risk/`)
- ✅ `RiskGovernor.cs` - Deterministic risk validation
  - `RiskDecision` with approve/deny (AI cannot override deny)
  - `RiskCheckResult` for individual check results
  - `RiskPolicy` record with versioned thresholds:
    - Concentration limits (single position, sector, industry)
    - Liquidity requirements (min ADV, min market cap)
    - Order limits (max value, max % of ADV)
    - Portfolio limits (max exposure, min cash buffer, max turnover)
    - Prohibited behaviors (leverage, margin, shorting, derivatives, penny stocks)
    - Market data freshness requirements
    - Kill switch control
  - `RiskEvaluationContext` with full portfolio/market snapshot
  - `IRiskGovernor` interface
  - `RiskDeniedException`, `StaleDataException`
  - **Key principle**: Deterministic code only, no LLM in risk decisions

#### Compliance Gate (`Compliance/`)
- ✅ `ComplianceGate.cs` - Deterministic compliance validation
  - `ComplianceDecision` with approve/deny
  - `ComplianceCheckResult` with regulatory references
  - `CompliancePolicy` with versioned rules:
    - Regulatory feature gates (auto trading, market data access)
    - Instrument restrictions
    - Account state requirements
    - Mandate requirements
    - Manual review settings
  - `Mandate` entity for Guarded Auto authorization:
    - Customer acceptance tracking (method, IP, device fingerprint)
    - Allowed/prohibited instruments and sectors
    - Risk parameters (horizon, risk level, concentration limits)
    - Turnover and order frequency limits
    - Drawdown response policy
    - Lifecycle: Active → Suspended/Revoked
  - `IComplianceGate` interface
  - `ComplianceDeniedException`, `MandateRequiredException`
  - **Key principle**: No AI final authority on compliance

#### AI Agents (`Agents/`)
- ✅ `AiProposal.cs` - Structured AI output schema
  - `AiProposal` entity with full audit trail:
    - Model/prompt versions
    - Evidence references
    - Confidence score and uncertainty explanation
    - Data freshness timestamp
    - Expected portfolio impact metrics
    - Scenario analysis (downside risks)
    - Risk disclosures
  - `ProposedAction` for recommended trades
  - `EvidenceReference` for source attribution
  - `PortfolioImpact` metrics (return, volatility, Sharpe, drawdown, turnover)
  - `ScenarioAnalysis` with probability levels
  - `IAiProposalService` interface
  - `AgentType` enum (7 agent types from AI-PLAN.md)
  - `AgentInvocationRecord` for observability
  - **Key boundary**: AI produces proposals only, never executes

#### Portfolio Management (`Portfolio/`)
- ✅ `Portfolio.cs` - Position and cash tracking
  - `Position` entity with cost basis, P&L, settlement tracking
  - `CashAccount` entity with available/reserved/unsettled balances
  - Cash reservation system for order lifecycle
  - Settlement tracking (T+2 for equities)
  - `PortfolioSnapshot` for risk evaluation context
  - `SectorAllocation`, `IndustryAllocation` for concentration analysis
  - `IPortfolioService` interface
  - `InsufficientCashException` for cash constraint violations
  - **Key feature**: Projection from ledger + executions, not authoritative truth

#### Persistence Layer (`Persistence/`) - NEW
- ✅ `KlyvestaDbContext.cs` - EF Core DbContext with financial precision requirements
  - UUID primary keys (UUIDv7 compatible)
  - Exact numeric/decimal fields for money (no floating point)
  - UTC timestamps only (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')
  - Optimistic concurrency via RowVersion
  - Append-only patterns for ledger entities
  - Automatic timestamp updates on SaveChanges
  
- ✅ `Entities/LedgerEntities.cs` - PostgreSQL entities for double-entry ledger
  - `LedgerAccountEntity` with AccountType enum (Asset, Liability, Equity, Revenue, Expense)
  - `JournalEntity` with JournalState (Draft → Committed → Reversed)
  - `PostingEntity` with EntryType (Debit/Credit)
  - Check constraint: debits must equal credits
  - Soft delete pattern with DeletedAtUtc
  - Hierarchical chart of accounts (self-referencing)
  
- ✅ `Entities/OrderEntities.cs` - PostgreSQL entities for order management
  - `OrderIntentEntity` with full lifecycle state tracking
  - Risk/compliance check flags with policy version references
  - Fill tracking (quantity, average price, executed value)
  - Idempotency key and correlation ID support
  - `OrderExecutionEntity` for individual fill records
  - Settlement date tracking (T+2)
  - Ledger posting reference for audit linkage
  
- ✅ `Entities/RiskComplianceEntities.cs` - PostgreSQL entities for risk/compliance
  - `RiskPolicyEntity` with versioned thresholds (basis points for percentages)
    - Concentration limits, liquidity requirements, order limits
    - Prohibited behaviors (leverage, margin, shorting, derivatives)
    - Kill switch control
  - `RiskDecisionEntity` for audit trail (every decision persisted)
  - `CompliancePolicyEntity` with regulatory feature gates
  - `MandateEntity` for customer auto-trading authorization
    - Acceptance tracking (method, IP, device fingerprint)
    - Allowed/prohibited instruments and sectors
    - Risk parameters and drawdown response policy
  - `ComplianceDecisionEntity` with manual review workflow

- ✅ `EntityConfigurations/LedgerEntityTypeConfigurations.cs` - EF Core model configurations
  - Schema separation (ledger.*)
  - Unique indexes on identifiers
  - Check constraints for financial invariants
  - Optimistic concurrency configuration
  - Composite indexes for query performance
  - Cascade/restrict delete behavior rules

## Remaining Work (P1 Backlog)

### Epic P1-01 — PostgreSQL / persistence baseline ⭐ PARTIALLY COMPLETE
- [x] EF Core entities for ledger, orders, risk/compliance
- [x] DbContext with financial precision configuration
- [x] Entity configurations with indexes and constraints
- [x] Package references (Npgsql.EntityFrameworkCore.PostgreSQL)
- [ ] PostgreSQL 18 development/test setup
- [ ] Schema/migration tooling (EF Core Migrations or DbUp)
- [ ] UUIDv7 identifier generation (database-side or application-side)
- [ ] Migration rollback procedures
- [ ] Integration tests with test database isolation

### Epic P1-02 — Identity/security abstraction
- [ ] Provider-neutral identity boundary
- [ ] Customer/account security state models
- [ ] Session/device tracking
- [ ] Passkey-ready interfaces
- [ ] Step-up authentication policy
- [ ] Account recovery restrictions

### Epic P1-03 — Authorization engine
- [ ] Decision engine with input schema
- [ ] BOLA/resource ownership tests
- [ ] Broken-function-level-authorization tests
- [ ] Package tier vs security role separation
- [ ] Staff privilege negative tests

### Epic P1-04 — Immutable double-entry ledger (implementation) ⭐ PARTIALLY COMPLETE
- [x] Domain model with Journal commit validation
- [x] EF Core entities for accounts/journals/postings
- [x] Check constraint: debits == credits
- [x] Entity configurations with proper indexes
- [ ] Reservation/hold system implementation
- [ ] Reversal/compensating entry logic
- [ ] Idempotent command processing
- [ ] Property-based invariant tests
- [ ] Concurrent duplicate command tests

### Epic P1-05 — Transactional outbox / event baseline
- [ ] Database-backed outbox table
- [ ] Event ID generation
- [ ] At-least-once consumer semantics
- [ ] De-duplication logic
- [ ] Replay tooling
- [ ] Poison/dead-letter handling
- [ ] Correlation/causation ID propagation

### Epic P1-06 — PaperBrokerAdapter ⭐ COMPLETED
- [x] Implement `IPaperBrokerAdapter : IBrokerAdapter`
- [x] Deterministic fill simulation (full/partial/rejected)
- [x] Timeout simulation (before/after side effect)
- [x] Duplicate/out-of-order event simulation
- [x] Outage/rate-limit simulation
- [x] Market-closed/stale-data behavior
- [x] Test against all 20 scenarios in PAPER_BROKER_SCENARIOS_V1.yaml
- [x] Property-based tests for financial invariants
- [x] Kill switch implementation
- [x] Idempotency handling

### Epic P1-07 — OMS state machine (implementation) ⭐ PARTIALLY COMPLETE
- [x] OrderIntent entity with full lifecycle tracking
- [x] OrderExecution entity for fill records
- [x] Risk/compliance check flags with policy versions
- [x] Idempotency key and correlation ID support
- [ ] OrderIntent service implementation
- [ ] Approval/rejection workflow
- [ ] Broker-order state synchronization
- [ ] Execution/fill de-duplication logic
- [ ] Cash/securities reservation system
- [ ] Cancel race handling
- [ ] UNKNOWN recovery workflow
- [ ] Reconciliation queue

### Epic P1-08 — Portfolio projection / reconciliation
- [ ] Positions read model projection
- [ ] Cash projection from ledger + executions
- [ ] Cost basis policy implementation (paper mode)
- [ ] Broker snapshot comparison
- [ ] Mismatch classification (critical/warning/info)
- [ ] Freeze/escalation behavior on critical mismatch

### Epic P1-09 — Deterministic Risk Governor (implementation) ⭐ PARTIALLY COMPLETE
- [x] RiskPolicy entity with versioned thresholds
- [x] RiskDecision entity for audit trail
- [x] Domain model with concentration/liquidity/order limits
- [x] Prohibited behaviors (leverage, margin, shorting, derivatives)
- [x] Kill switch flag in policy
- [ ] RiskGovernor service implementation
- [ ] Concentration check implementations
- [ ] Liquidity eligibility checks
- [ ] Stale-data rejection logic
- [ ] Portfolio exposure calculations
- [ ] Turnover/order rate tracking
- [ ] Policy version persistence with decisions

### Epic P1-10 — Compliance Gate (implementation) ⭐ PARTIALLY COMPLETE
- [x] CompliancePolicy entity with regulatory feature gates
- [x] MandateEntity for customer auto-trading authorization
- [x] ComplianceDecision entity with audit trail
- [x] Acceptance tracking (method, IP, device fingerprint)
- [x] Allowed/prohibited instruments and sectors
- [x] Manual review workflow fields
- [ ] ComplianceGate service implementation
- [ ] Account status checks
- [ ] Regulatory feature gate evaluation
- [ ] Mandate requirement enforcement
- [ ] Instrument restriction checks
- [ ] Manual hold/review state management

### Epic P1-11 — AI / Quant shadow boundary
- [ ] Proposal schema validation
- [ ] Portfolio optimizer integration point
- [ ] Risk/Compliance integration flow
- [ ] Shadow order plan generation
- [ ] PaperBroker-only execution path
- [ ] Prompt injection tests
- [ ] Fabricated balance/price/order tests
- [ ] Model outage isolation tests

### Epic P1-12 — Notifications / investment timeline
- [ ] Event-driven timeline service
- [ ] In-app notification storage
- [ ] Email delivery integration
- [ ] WhatsApp delivery integration
- [ ] SMS delivery for critical events
- [ ] Delivery state tracking
- [ ] Duplicate event spam prevention

### Epic P1-13 — Observability / incident baseline
- [ ] OpenTelemetry trace integration
- [ ] Structured logging configuration
- [ ] Request/correlation ID propagation
- [ ] Metrics collection
- [ ] Liveness/readiness endpoints
- [ ] Audit/security event logging
- [ ] Broker simulator health checks
- [ ] Reconciliation mismatch metrics
- [ ] Kill-switch telemetry

### Epic P1-14 — Security acceptance
- [ ] Automate P1_SECURITY_ACCEPTANCE_V1.yaml tests
- [ ] AuthN/AuthZ test suite
- [ ] BOLA/BFLA automated tests
- [ ] Idempotency/replay tests
- [ ] Injection/input validation tests
- [ ] Secrets management tests
- [ ] PII logging prevention tests
- [ ] Rate limiting tests
- [ ] Account recovery tests
- [ ] Admin privilege separation tests
- [ ] AI tool boundary tests
- [ ] Supply-chain/static analysis integration
- [ ] Dependency audit automation
- [ ] Backup/restore procedure tests
- [ ] Incident tabletop documentation

### Epic P1-15 — Performance/resilience
- [ ] Risk gate p95 <= 50 ms load tests
- [ ] Order-intent validation p95 <= 150 ms tests
- [ ] Cached portfolio p95 <= 500 ms tests
- [ ] Duplicate command constant-time tests
- [ ] Broker outage backpressure tests
- [ ] Runaway retry prevention tests
- [ ] 10x spike load tests

## Next Steps

Immediate priorities for continuation:

1. **Database Migration Tooling** - Set up EF Core Migrations or DbUp for schema deployment (Epic P1-01)
2. **Ledger Service Implementation** - ILedgerService with commit/reversal logic (Epic P1-04)
3. **OrderIntent Service** - State machine implementation with broker sync (Epic P1-07)
4. **Authorization Engine** - Decision engine with BOLA/BFLA tests (Epic P1-03)
5. **Risk Governor Service** - Deterministic policy evaluation (Epic P1-09)
6. **Compliance Gate Service** - Mandate enforcement and regulatory gates (Epic P1-10)
7. **Integration Tests** - Test database isolation, migration rollback testing
8. **Property-Based Testing** - FsCheck tests for financial invariants (ledger balance, idempotency)

## Architecture Notes

All implementations must maintain AI-PLAN.md principles:
- AI agents produce structured proposals only (never execute)
- Deterministic Risk Governor and Compliance Gate have veto authority
- No LLM directly calls broker execution endpoints
- Every decision has model/policy version, evidence references, audit trail
- UNKNOWN broker states trigger reconciliation, not blind retry
- Double-entry ledger is append-only with zero tolerance for imbalance
- Idempotency keys prevent duplicate financial effects
- All money math uses decimal, never float/double
