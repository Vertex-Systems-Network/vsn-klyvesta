# P1-08 Portfolio Projection / Reconciliation Feature-Branch Checkpoint — 2026-09-01

Status: **NON-LIVE / STACKED IMPLEMENTATION CANDIDATE / MAIN MERGE BLOCKED**

Canonical work item: GitHub Issue #72.
Stack base: P1-07 draft PR #71 exact technically verified head `5f0718fab6e8ba8678d4ecc2aa3772532a6d7933`.

## Implemented candidate scope

- rebuildable paper cash/position projection from explicit source events;
- exact-decimal arithmetic only;
- weighted-average paper cost-basis policy;
- source-event and execution-id de-duplication;
- normalized broker balance/position snapshot comparison;
- explicit mismatch classification for unavailable/ambiguous broker evidence, cash mismatch, missing/unexpected position and quantity mismatch;
- reconciliation comparison never mutates projection truth;
- critical mismatch activates an observable account execution hold;
- provider-neutral `IOrderExecutionHoldProvider` boundary;
- `ExecutionHoldBrokerAdapter` blocks a new paper submit before the inner PaperBroker when a critical hold is active;
- held OMS order remains retryable with its existing internal broker-order identity and reservation intact;
- a later matching reconciliation can explicitly clear the hold;
- deterministic `Klyvesta.PortfolioVerifier` wired into `dotnet-foundation` CI.

## Deterministic verifier catalogue

- PORT-001 projection rebuild is deterministic;
- PORT-002 duplicate source/execution evidence is idempotent;
- PORT-003 weighted-average paper cost basis is exact;
- PORT-004 matching broker snapshot reconciles cleanly;
- PORT-005 cash mismatch is classified without silent correction;
- PORT-006 missing broker position is classified;
- PORT-007 unexpected broker position is classified;
- PORT-008 position quantity mismatch is classified;
- PORT-009 critical mismatch blocks OMS submit before PaperBroker;
- PORT-010 matching subsequent evidence explicitly clears hold and allows the same pending OMS identity to continue.

## Authority boundary

Portfolio projection remains a **read model**. This candidate does not replace or claim the future authoritative double-entry ledger, and reconciliation does not overwrite local projection state from broker comparison results.

## Explicitly not accepted by this checkpoint

- live pyPSX mapping, API contract, auth or credentials;
- real-money execution;
- production customer PII;
- P1-04 double-entry ledger implementation/acceptance;
- production cost-basis/tax/accounting policy;
- P1-03 authorization acceptance;
- P1-09 deterministic Risk Governor acceptance;
- P1-10 compliance/mandate acceptance;
- P1-11 AI shadow acceptance;
- full P1 exit-gate acceptance;
- production readiness.

## Validation state at checkpoint creation

Technical validation is intentionally **PENDING** until a draft PR to `main` produces fresh exact-head GitHub Actions evidence for:

- repository governance;
- foundation formatting/build/architecture plus PaperBroker, OMS and portfolio verifiers;
- PostgreSQL regression/migration checks;
- CodeQL.

No passing status is claimed by this checkpoint before those exact-head runs complete. Any failed candidate head is non-authoritative and must be replaced by fresh exact-head evidence after correction.

## Known separate repository debt

The pre-existing EF Core Relational assembly-version warning is tracked independently by Issue #69 and is not being mixed into this P1-08 slice unless it becomes a direct blocker.

## Governance

GitHub Issue #1 remains the merge blocker because hosted `main` is unprotected. This stacked candidate may be reviewed and tested, but must not merge to `main` until protected-main enforcement is verified or a new explicit owner risk decision is durably recorded.

P0-T1 / Issue #20 remains independently OPEN. Owner-provided API timing is planning context only and does not authorize live integration.
