# P1 Paper Broker Implementation Checkpoint

Date: 2026-08-27
Canonical task: `P1-T1 — Implement PaperBrokerAdapter with deterministic scenario emulation`

## Rationale

Continue parallel work stream `paper_broker_after_F0` while P0-T1 partner verification remains blocked. Paper broker implementation:
- Validates Klyvesta's internal financial state machines
- Tests all 20 scenarios from `contracts/broker/PAPER_BROKER_SCENARIOS_V1.yaml`
- Does NOT depend on pyPSX semantics or live broker credentials
- Enables AI shadow mode testing with non-production controls

## Entry Conditions Verified

- [x] F0 governance resolution merged
- [x] F1 .NET skeleton foundation merged  
- [x] F2 PostgreSQL persistence merged
- [x] No live broker credentials in development
- [ ] Issue #1 (main branch protection) remains OPEN - acceptable for P1 paper-only work
- [x] Repository governance allows parallel P1 work per `state.json`

## Zero-Tolerance Invariants

Implementation must enforce:
```
Ledger debits == ledger credits
No fill -> no executed position increase/decrease
Duplicate execution -> one financial effect only
Duplicate command/idempotency key -> one operation only
No valid mandate -> no auto order
Unauthorized resource -> no financial command
Rejected order -> no fill
Unknown broker result -> no blind duplicate submit
Stale critical market state -> no auto execution
AI proposal -> never authoritative balance/order/fill truth
```

Any violation fails acceptance regardless of test percentage.

## Implementation Scope

### Domain Layer (`Klyvesta.Domain`)

Deliverables:
1. **Value Objects**
   - `Money` (decimal-based, no floating point)
   - `Quantity` (decimal-based)
   - `Symbol` (instrument identifier)
   - `AccountId`, `OrderId`, `ExecutionId` (UUIDv7)

2. **Entities**
   - `OrderIntent` (customer request before broker submission)
   - `BrokerOrder` (submitted to PaperBroker)
   - `Execution` (fill record)
   - `Position` (projected holdings)

3. **Enumerations**
   - `OrderSide` (Buy, Sell)
   - `OrderType` (Market, Limit)
   - `OrderStatus` (Pending, Submitted, PartiallyFilled, Filled, Cancelled, Rejected, Unknown)
   - `TimeInForce` (Day, GTC, IOC, FOK)
   - `RejectionReason` (enumerated reasons)

4. **Services (Interfaces)**
   - `IBrokerAdapter` (normalized contract)
   - `ILedgerService` (double-entry postings)
   - `IPositionService` (projection management)
   - `IRiskGovernor` (deterministic limits)
   - `IComplianceGate` (regulatory checks)

### Application Layer (`Klyvesta.Application`)

Deliverables:
1. **Command Handlers**
   - `SubmitOrderIntentHandler`
   - `CancelOrderHandler`
   - `QueryOrderStatusHandler`

2. **Validators**
   - `OrderIntentValidator` (cash/position, session, instrument)
   - `AuthorizationValidator` (resource ownership)
   - `RiskGovernorValidator` (concentration, exposure, turnover)
   - `ComplianceGateValidator` (mandate, eligibility)

3. **State Machines**
   - `OrderStateMachine` (transitions, guards, effects)
   - `ReconciliationStateMachine` (mismatch handling)

4. **Event Handlers**
   - `ExecutionReceivedHandler` (ledger posting, position update)
   - `OrderStatusChangedHandler` (notifications, audit)

### Infrastructure Layer (`Klyvesta.Infrastructure`)

Deliverables:
1. **PaperBrokerAdapter Implementation**
   - Deterministic scenario simulation
   - Configurable latency/failure injection
   - Idempotent command processing
   - Event generation for outbox

2. **Persistence Records**
   - `OrderRecord` (intent + broker order state)
   - `ExecutionRecord` (fill details)
   - `PositionRecord` (projected holdings snapshot)
   - `LedgerJournalRecord` (immutable double-entry)
   - `LedgerPostingRecord` (individual debit/credit)

3. **Migrations**
   - P1 schema additions to F2 baseline

### API Layer (`Klyvesta.Api`)

Deliverables:
1. **Endpoints (Paper Mode Only)**
   - `POST /api/orders` (submit intent)
   - `GET /api/orders/{id}` (query status)
   - `DELETE /api/orders/{id}` (cancel)
   - `GET /api/portfolio` (projected positions/cash)
   - `GET /api/health/paper-broker` (simulator health)

2. **Feature Gates**
   - `PaperModeEnabled` (always true for P1)
   - `LiveTradingEnabled` (always false until P2+)

## Scenario Coverage Matrix

| Scenario ID | Name | Status | Test File |
|-------------|------|--------|-----------|
| PB-001 | full_fill | PENDING | PaperBrokerScenariosTests.cs |
| PB-002 | rejected_order | PENDING | PaperBrokerScenariosTests.cs |
| PB-003 | partial_fill | PENDING | PaperBrokerScenariosTests.cs |
| PB-004 | multiple_fills | PENDING | PaperBrokerScenariosTests.cs |
| PB-005 | duplicate_command | PENDING | PaperBrokerScenariosTests.cs |
| PB-006 | timeout_before_side_effect | PENDING | PaperBrokerScenariosTests.cs |
| PB-007 | ambiguous_timeout_after_possible_side_effect | PENDING | PaperBrokerScenariosTests.cs |
| PB-008 | cancel_before_fill | PENDING | PaperBrokerScenariosTests.cs |
| PB-009 | fill_cancel_race | PENDING | PaperBrokerScenariosTests.cs |
| PB-010 | duplicate_broker_event | PENDING | PaperBrokerScenariosTests.cs |
| PB-011 | out_of_order_events | PENDING | PaperBrokerScenariosTests.cs |
| PB-012 | stale_market_data | PENDING | PaperBrokerScenariosTests.cs |
| PB-013 | market_closed | PENDING | PaperBrokerScenariosTests.cs |
| PB-014 | broker_unavailable | PENDING | PaperBrokerScenariosTests.cs |
| PB-015 | reconciliation_mismatch | PENDING | PaperBrokerScenariosTests.cs |
| PB-016 | ledger_persistence_failure | PENDING | PaperBrokerScenariosTests.cs |
| PB-017 | unauthorized_customer_resource | PENDING | PaperBrokerScenariosTests.cs |
| PB-018 | auto_without_mandate | PENDING | PaperBrokerScenariosTests.cs |
| PB-019 | risk_breach | PENDING | PaperBrokerScenariosTests.cs |
| PB-020 | compliance_hold | PENDING | PaperBrokerScenariosTests.cs |

## Acceptance Criteria

P1-T1 complete when:
- [ ] All 20 scenarios pass with zero-tolerance invariants enforced
- [ ] Ledger imbalance tests pass (debits == credits always)
- [ ] Duplicate command tests pass (exactly-once financial effect)
- [ ] Duplicate execution tests pass (no duplicate postings)
- [ ] Authorization tests pass (BOLA/BFLA prevention)
- [ ] Risk governor tests pass (AI cannot override denial)
- [ ] Compliance gate tests pass (mandate required for auto)
- [ ] Reconciliation tests pass (mismatch creates exception, not silent correction)
- [ ] Kill switch tested (system-wide pause works)
- [ ] No live broker credentials or real-money involved

## Security Boundaries

- PaperBrokerAdapter implements `IBrokerAdapter` but NEVER has access to live credentials
- AI agents have NO tool permissions for broker execution in P1
- All financial commands require authenticated principal
- Audit log captures every state transition with correlation IDs
- No PII or secrets in logs/metrics

## Next Actions

1. Create domain value objects and entities
2. Define IBrokerAdapter interface contract
3. Implement PaperBrokerAdapter with scenario configuration
4. Build OrderIntent state machine
5. Implement double-entry ledger service
6. Create position projection service
7. Add Risk Governor deterministic rules
8. Add Compliance Gate checks
9. Wire up API endpoints (paper mode only)
10. Write comprehensive scenario tests
11. Document P1 security acceptance evidence

## Dependencies

- Does NOT require: pyPSX contract, sandbox access, live credentials
- Requires: F0/F1/F2 foundation (already merged)
- Blocks: AI shadow mode integration (P1-11), portfolio projection tests (P1-08)

## Exit Gate

P1-T1 exit enables:
- P1-T2 through P1-T15 completion
- Full P1 acceptance gate satisfaction
- Manual real-money candidate planning (still requires P0 for actual production)

---

**Note:** This work proceeds in parallel with P0-T1 partner verification. Neither depends on the other. P1 proves Klyvesta's internal correctness; P0 proves external partner viability.
