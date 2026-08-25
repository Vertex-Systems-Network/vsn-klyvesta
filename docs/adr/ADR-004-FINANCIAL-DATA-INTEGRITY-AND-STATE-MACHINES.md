# ADR-004 — Financial Data Integrity & State Machines

Status: Accepted for implementation planning.
Date: 2026-08-25

## Context

Klyvesta must remain correct when requests are duplicated, workers crash, broker calls time out, webhook events arrive out of order, fills are partial, users cancel during execution, or reconciliation finds disagreement.

A mutable-balance/simple-CRUD architecture is not sufficient for financial correctness.

## Decision

1. PostgreSQL 18.x is the transactional source of truth.
2. Use UUIDv7 identifiers for primary domain resources.
3. Use exact decimal arithmetic (`C# decimal` and PostgreSQL `numeric`) for financial values; never binary floating point for authoritative money/settlement math.
4. Use immutable double-entry journal entries for financial accounting.
5. Posted ledger/execution/audit history is append-only; corrections use reversal/compensating records.
6. Every retryable external financial command uses explicit idempotency identity and request-hash validation.
7. Orders, reservations, withdrawals, mandates, recommendations and reconciliation breaks use explicit state machines.
8. External ambiguity has an explicit `UNKNOWN` state. Unknown financial commands are reconciled before any potentially duplicating retry.
9. Positions/balances/performance views are projections with traceable source/reconciliation versions, not independent facts.
10. Use PostgreSQL transactional outbox before introducing a distributed event bus. Extract Kafka/Redpanda/NATS only when measured scaling/decoupling needs justify it.
11. Use selective locking/transaction isolation based on invariant risk rather than globally forcing SERIALIZABLE.

## Alternatives considered

### Mutable `users.balance` / direct balance updates
Rejected because it loses accounting lineage, complicates reconciliation and makes partial/retry failure dangerous.

### Event sourcing every domain aggregate from day one
Rejected as unnecessary complexity. Financial ledger/audit facts are append-only, while ordinary domain state can use conventional transactional models plus domain events/outbox.

### Globally SERIALIZABLE transactions
Rejected as a default due to avoidable contention/retry complexity. Use explicit invariants/locks and selective SERIALIZABLE where it materially improves correctness.

### Blind retry on broker timeout
Rejected. A timeout can occur after the broker executed the request. Ambiguity maps to `UNKNOWN` and reconciliation.

## Consequences

Positive:
- deterministic financial history;
- safe duplicate/retry handling;
- stronger reconciliation and incident recovery;
- explicit partial/ambiguous states;
- easier audit and invariant testing.

Costs:
- more domain modeling than CRUD;
- reservation and reconciliation workers are mandatory;
- projection rebuilding/versioning must be supported;
- broker contract quality/idempotency semantics materially affect implementation.

## Verification

Implementation must eventually prove:
- balanced journals;
- immutability of posted entries;
- duplicate command/fill has one financial effect;
- unknown broker state never triggers unsafe resubmission;
- cash/position reservation races cannot overspend/oversell;
- forbidden state transitions fail;
- projections reconcile to external broker facts.

See:
- `docs/DOMAIN_DATABASE_MODEL_V1.md`
- `docs/ORDER_LEDGER_STATE_MACHINES_V1.md`
- `contracts/broker/BROKER_ADAPTER_V1.md`

## References verified at decision time

- PostgreSQL 18 exact `numeric` types and database constraints.
- PostgreSQL 18 transaction isolation/Serializable behavior.
- PostgreSQL 18 UUIDv7 generation support.
- .NET 10 `Guid.CreateVersion7()` support.
