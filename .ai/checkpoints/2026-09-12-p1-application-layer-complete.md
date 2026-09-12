# P1-T2 Application Layer Complete

Date: 2026-09-12
Task: P1-T2 Application Foundation — Command handlers, validators, state machines, and domain events

## Summary

Completed the application layer foundation for the Paper Broker implementation. All command handlers, validators, state machines, and domain events are now in place to support order lifecycle management with proper separation of concerns.

## Implemented Components

### Command Handlers (`Klyvesta.Application/Handlers`)

1. **SubmitOrderIntentHandler** — Handles order submission commands
   - Idempotency check via `IIdempotencyService`
   - Validation delegation to `OrderIntentValidator`
   - Broker submission via `IBrokerAdapter`
   - Result types: Succeeded, Duplicate, RiskDenied, ComplianceDenied, Invalid, Rejected, Unknown

2. **CancelOrderHandler** — Handles order cancellation commands
   - Delegates to `IBrokerAdapter.CancelOrderAsync`
   - Returns success/failure result

3. **QueryOrderStatusHandler** — Handles order status queries
   - Delegates to `IBrokerAdapter.QueryOrderStatusAsync`
   - Returns full status with executions

4. **ApplyExecutionHandler** — Handles execution (fill) application
   - Updates position via `IPositionService`
   - Posts double-entry ledger transaction via `ILedgerService`
   - Creates balanced debit/credit entries based on buy/sell side
   - Idempotency via execution ID

5. **OrderStateMachine** — Enforces order lifecycle invariants
   - Valid state transitions only
   - Terminal state protection
   - Execution quantity validation
   - Methods: Submit, AddExecution, Cancel, Reject, MarkUnknown
   - State query: GetValidNextStates, CanTransitionTo

### Validators (`Klyvesta.Application/Validators`)

1. **OrderIntentValidator** — Validates order intent commands
   - Basic validation (quantity > 0, limit price for limit orders)
   - Risk governor check via `IRiskGovernor`
   - Compliance gate check via `IComplianceGate`
   - Result types: Valid, Invalid, RiskDenied, ComplianceDenied

### Domain Events (`Klyvesta.Application/Events`)

1. **OrderIntentSubmittedEvent** — Raised when order is submitted
2. **OrderStatusChangedEvent** — Raised when order status changes
3. **ExecutionReceivedEvent** — Raised when fill occurs
4. **PositionUpdatedEvent** — Raised when position is updated
5. **LedgerTransactionPostedEvent** — Raised when ledger posts
6. **LedgerPostingSummary** — Posting summary for events

### Persistence Records (`Klyvesta.Infrastructure/Persistence/Records`)

1. **OrderIntentRecord** — Order intent persistence
2. **ExecutionRecord** — Fill/execution persistence
3. **PositionRecord** — Customer position persistence
4. **LedgerJournalRecord** — Ledger transaction header
5. **LedgerPostingRecord** — Ledger transaction line items
6. **BrokerOrderRecord** — Broker-submitted order tracking

### DbContext Configuration (`Klyvesta.Infrastructure/Persistence/KlyvestaDbContext`)

Added DbSet properties and configuration methods for all P1 records:
- `ConfigureOrderIntents()` — trading.order_intent table
- `ConfigureExecutions()` — trading.execution table
- `ConfigurePositions()` — trading.position table
- `ConfigureLedgerJournals()` — accounting.ledger_journal table
- `ConfigureLedgerPostings()` — accounting.ledger_posting table
- `ConfigureBrokerOrders()` — trading.broker_order table

All configurations include:
- Proper column mappings with PostgreSQL types
- Precision settings for decimal fields (18,4 for money, 18,8 for quantities)
- Unique indexes for idempotency keys and natural keys
- Foreign key relationships via indexes
- Check constraints where applicable

## Database Schema (P1 Additions)

### trading.order_intent
- id (uuid, PK)
- customer_id (uuid, not null)
- symbol (varchar(20), not null)
- side (int, not null) — enum: OrderSide
- type (int, not null) — enum: OrderType
- quantity (numeric(18,8), not null)
- limit_price (numeric(18,4))
- currency (varchar(3), default 'PKR')
- time_in_force (int, not null) — enum: TimeInForce
- status (int, not null) — enum: OrderStatus
- idempotency_key (varchar(128), unique)
- broker_order_id (uuid)
- rejection_reason (int) — enum: RejectionReason
- rejection_details (varchar(500))
- created_at_utc (timestamptz, not null)
- updated_at_utc (timestamptz)
- submitted_at_utc (timestamptz)

Indexes: ix_order_intent_customer_id, ix_order_intent_status, ux_order_intent_idempotency_key

### trading.execution
- id (uuid, PK)
- order_intent_id (uuid, not null)
- customer_id (uuid, not null)
- execution_id (varchar(128), not null) — stable ID from broker
- quantity (numeric(18,8), not null)
- price_per_unit (numeric(18,4), not null)
- currency (varchar(3), not null)
- executed_at_utc (timestamptz, not null)
- broker_reference (varchar(256))
- recorded_at_utc (timestamptz, not null)

Indexes: ix_execution_order_intent_id, ux_execution_customer_execution_id (unique)

### trading.position
- id (uuid, PK)
- customer_id (uuid, not null)
- symbol (varchar(20), not null)
- quantity (numeric(18,8), not null)
- average_cost_basis (numeric(18,4), not null)
- currency (varchar(3), not null)
- last_updated_at_utc (timestamptz, not null)

Indexes: ux_position_customer_symbol (unique)

### accounting.ledger_journal
- id (uuid, PK)
- description (varchar(500), not null)
- correlation_id (varchar(128))
- idempotency_key (varchar(128), unique)
- created_at_utc (timestamptz, not null)
- status (int, not null) — 0=Pending, 1=Posted, 2=Failed

Indexes: ix_ledger_journal_correlation_id, ux_ledger_journal_idempotency_key

### accounting.ledger_posting
- id (uuid, PK)
- journal_id (uuid, not null)
- account_id (uuid, not null)
- amount (numeric(18,4), not null)
- currency (varchar(3), not null)
- is_debit (boolean, not null)
- reference (varchar(256))
- posted_at_utc (timestamptz, not null)

Indexes: ix_ledger_posting_journal_id, ix_ledger_posting_account_id

### trading.broker_order
- id (uuid, PK)
- order_intent_id (uuid, not null)
- customer_id (uuid, not null)
- symbol (varchar(20), not null)
- side (int, not null)
- type (int, not null)
- quantity (numeric(18,8), not null)
- limit_price (numeric(18,4))
- currency (varchar(3))
- time_in_force (int, not null)
- status (int, not null)
- broker_order_id (varchar(128))
- rejection_reason (int)
- rejection_details (varchar(500))
- submitted_at_utc (timestamptz, not null)
- updated_at_utc (timestamptz)

Indexes: ix_broker_order_order_intent_id, ix_broker_order_broker_order_id

## Zero-Tolerance Invariants Enforced

Application layer enforces these through handlers and state machines:

✓ Duplicate command → one operation (idempotency key check before processing)
✓ No valid mandate → no auto order (ComplianceGate check in validator)
✓ Risk limit breach → no order (RiskGovernor check in validator)
✓ Invalid parameters → rejected early (validator basic checks)
✓ Terminal state → no modification (OrderStateMachine guards)
✓ Over-fill → prevented (OrderStateMachine execution quantity check)
✓ Unbalanced ledger → constructor validation in LedgerTransaction
✓ Unknown broker outcome → marked as Unknown state (not blind retry)

## Next Steps (Remaining P1 Implementation)

1. **Infrastructure Implementations**
   - PositionService implementation (IPositionService)
   - LedgerService implementation (ILedgerService)
   - RiskGovernor implementation (IRiskGovernor)
   - ComplianceGate implementation (IComplianceGate)
   - IdempotencyService implementation (IIdempotencyService)

2. **API Layer Enhancements**
   - Wire up handlers to OrdersController
   - Implement GET /api/orders/{id} endpoint
   - Implement DELETE /api/orders/{id} endpoint
   - Add GET /api/portfolio endpoint
   - Add GET /api/health/paper-broker endpoint

3. **Migration Generation**
   - Create EF Core migration for P1 schema additions
   - Verify schema matches specification

4. **Testing**
   - Unit tests for all handlers
   - Unit tests for OrderStateMachine
   - Integration tests for all 20 paper broker scenarios
   - Zero-tolerance invariant tests

## Status

**Application Layer: COMPLETE** ✓
**Persistence Configuration: COMPLETE** ✓

Ready to proceed with Infrastructure service implementations and API endpoint wiring.

---

**Note:** This work continues in parallel with P0-T1 partner verification. The P1 paper broker implementation proves Klyvesta's internal correctness independent of external partner viability.
