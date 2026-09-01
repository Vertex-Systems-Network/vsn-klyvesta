# P1-07 OMS Feature-Branch Checkpoint — 2026-09-01

Status: **NON-LIVE / STACKED IMPLEMENTATION CANDIDATE / MAIN MERGE BLOCKED**

Canonical work item: GitHub Issue #70.
Stack base: P1-06 draft PR #68 exact verified head `f1048043e7b47e2892f2ecfb72d26584aa06accb`.

## Implemented candidate scope

- explicit OrderIntent state transitions;
- validation evidence recording without giving OMS authorization/risk/compliance authority;
- approval-linked exact-decimal reservation state;
- internal BrokerOrder state machine including `PENDING_SUBMIT`, `SUBMITTING`, `UNKNOWN`, cancellation and terminal states;
- PaperBroker submission through the normalized `IBrokerAdapter` only;
- one internal broker order identity per intent;
- safe retry only for normalized `RETRYABLE_FAILURE` before side-effect semantics;
- ambiguous submit -> `UNKNOWN`, no blind replay, reconciliation work queued;
- query-based recovery from `UNKNOWN`;
- immutable execution-ID de-duplication at OMS boundary;
- exact cash/position reservation consumption from confirmed executions;
- partial-fill and fill/cancel race handling;
- out-of-order state non-regression;
- contradictory broker snapshot quarantine without consuming untrusted economics;
- deterministic in-memory reconciliation queue for the P1-07 slice;
- `Klyvesta.OmsVerifier` wired into `dotnet-foundation` CI.

## Deterministic verifier catalogue

- OMS-001 rejected validation never reaches broker;
- OMS-002 full fill consumes and releases reservation exactly;
- OMS-003 repeated execute cannot create a second broker order;
- OMS-004 ambiguous submit becomes `UNKNOWN` and recovers by query;
- OMS-005 partial fill consumes only confirmed economics;
- OMS-006 duplicate execution snapshot is idempotent;
- OMS-007 partial-fill cancel race preserves fills and releases remainder;
- OMS-008 out-of-order status cannot regress authoritative state;
- OMS-009 pre-side-effect retry keeps one internal broker order;
- OMS-010 contradictory snapshot quarantines to reconciliation.

## Explicitly not accepted by this checkpoint

This candidate does **not** establish:

- live pyPSX mapping or technical/commercial contract;
- real-money execution;
- production customer PII;
- persisted/transactional OMS durability;
- P1-04 double-entry ledger postings;
- P1-08 portfolio projection/full reconciliation;
- P1-03 authorization/resource ownership runtime proof;
- P1-09 market-data/risk policy acceptance;
- P1-10 compliance/mandate acceptance;
- P1-11 AI shadow acceptance;
- full 20-scenario P1 exit-gate acceptance;
- P1 phase acceptance.

## Validation state at checkpoint creation

Technical validation is intentionally **PENDING** until a draft PR to `main` produces exact-head GitHub Actions evidence for:

- repository governance;
- foundation formatting/build/architecture + PaperBroker + OMS verifier;
- PostgreSQL regression/migration checks;
- CodeQL.

No passing status is claimed by this checkpoint before those exact-head runs complete.

## Governance

GitHub Issue #1 remains the merge blocker because hosted `main` is unprotected. This stacked candidate may be reviewed and tested, but must not merge to `main` until protected-main enforcement is verified or a new explicit owner risk decision is durably recorded.

P0-T1 / Issue #20 remains independently OPEN. Owner-provided API timing is planning context only and does not authorize live integration.
