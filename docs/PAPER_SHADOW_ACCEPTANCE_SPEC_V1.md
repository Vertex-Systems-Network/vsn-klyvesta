# Paper / Shadow Acceptance Specification V1

Status: **Implementation specification; does not unlock live trading.**

## Purpose

Define the minimum deterministic evidence Klyvesta must produce before any live broker adapter or real-money feature can be considered. The paper system validates Klyvesta's own financial state machines, not undocumented pyPSX behavior.

## Components under test

```text
Order Intent
 -> Authorization
 -> Risk Governor
 -> Compliance Gate
 -> Execution Validator
 -> PaperBrokerAdapter
 -> Execution/Fills
 -> Reconciliation
 -> Double-Entry Ledger
 -> Positions/Portfolio
 -> Audit/Timeline
```

AI/Quant may propose actions but remains outside authoritative execution state.

## Zero-tolerance invariants

Every test environment must enforce:

```text
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

Any zero-tolerance violation fails the acceptance run regardless of overall test percentage.

## Deterministic PaperBroker contract

The PaperBroker must consume the same normalized internal command contract intended for future live adapters.

It may support explicit scenario controls for tests, but business/domain code must not depend on paper-only internals.

### Required outcomes

- `SUCCESS`
- `REJECTED`
- `RETRYABLE_FAILURE` only for operations where retry is safe
- `UNKNOWN` when a financial side effect may have occurred and state must be reconciled

## Scenario catalogue

### PB-001 Full fill

Given an authorized order with sufficient simulated cash/position and valid market state:
- one broker order is created;
- one or more fills sum exactly to requested executable quantity;
- position projection matches fills;
- ledger remains balanced;
- timeline/audit record contains intent, policy decisions and fills.

### PB-002 Rejected order

Simulate broker rejection:
- state becomes `REJECTED`;
- no execution/fill exists;
- no executed position change occurs;
- reservation is released according to deterministic state machine;
- rejection reason is safe/auditable.

### PB-003 Partial fill

Simulate partial quantity:
- order becomes `PARTIALLY_FILLED`;
- only filled quantity affects executed position/ledger;
- remaining reservation is correct;
- later fill/cancel can resolve without duplication.

### PB-004 Multiple fills

Simulate N fills:
- each stable execution identity applies once;
- aggregate quantity and weighted economics are exact;
- duplicated delivery of any fill is ignored/idempotently recorded.

### PB-005 Duplicate command

Submit same command/idempotency key twice:
- second request returns existing operation/equivalent safe response;
- no duplicate order or ledger effect.

Same key with different command payload:
- reject as conflict;
- do not execute either a second order or silent overwrite.

### PB-006 Timeout before side effect

Simulate transport failure known to occur before PaperBroker accepted the command:
- classify according to safe retry contract;
- retry must not duplicate state.

### PB-007 Ambiguous timeout after possible side effect

Simulate request accepted but response lost:
- normalized result is `UNKNOWN`;
- duplicate submission is frozen;
- recovery queries broker/order/executions;
- state resolves through reconciliation;
- exactly one financial effect occurs.

### PB-008 Cancel before fill

Cancel accepted before execution:
- no fill;
- final order `CANCELLED`;
- correct reservation released;
- no position effect.

### PB-009 Fill/cancel race

A fill and cancel cross:
- fill remains authoritative for executed quantity;
- cancel only applies to remaining quantity;
- no assumption that cancel acknowledgement means zero fills;
- reservation/position/ledger reconcile exactly.

### PB-010 Duplicate broker event

Deliver same normalized external event multiple times:
- event deduplication prevents duplicate effect;
- audit can record duplicate detection without changing financial truth.

### PB-011 Out-of-order events

Deliver `FILLED`/execution evidence before delayed `OPEN` or older status:
- state machine does not regress authoritative state;
- reconciliation selects evidence according to monotonic/domain rules.

### PB-012 Stale market data

For auto/shadow execution policy:
- stale quote/session state causes deterministic reject/hold;
- no trade occurs;
- reason is auditable.

### PB-013 Market closed

Order policy follows configured paper market/session semantics;
- unsupported action is rejected/queued according to explicit policy;
- never silently executed as if market open.

### PB-014 Broker unavailable

PaperBroker unavailable:
- financial command remains safe;
- no fabricated fill;
- circuit/health state visible;
- retries respect idempotency and safe classification.

### PB-015 Reconciliation mismatch

Broker snapshot intentionally differs from projection:
- exception is raised;
- customer-facing financial truth is not silently rewritten;
- automated trading for affected scope can be paused;
- correction requires defined reconciliation workflow.

### PB-016 Ledger failure

Simulate transactional persistence failure:
- external side-effect handling follows outbox/reconciliation design;
- no partially posted unbalanced journal;
- operation becomes recoverable/exception state.

### PB-017 Unauthorized customer resource

Attempt command for account/portfolio not owned/authorized by principal:
- deny before broker call;
- no PaperBroker invocation;
- security event recorded without leaking target data.

### PB-018 Auto without mandate

AI proposes valid-looking order but no active mandate exists:
- deterministic deny;
- PaperBroker not invoked.

### PB-019 Risk breach

AI/manual intent exceeds allowed exposure/risk policy:
- Risk Governor rejects/adjusts only through explicitly supported policy path;
- AI cannot override decision;
- no broker call for denied order.

### PB-020 Compliance hold

Account/instrument/action is compliance-blocked:
- execution denied;
- no bypass by staff/support/AI package.

## Ledger acceptance

Required property/invariant tests:
- every posted journal balances;
- journal rows are append-only after posting;
- reversals use compensating entries;
- exact decimal arithmetic only;
- same external execution cannot post twice;
- reservation/release totals never produce impossible negative available amount without explicit supported credit model;
- concurrent fills preserve consistency.

## Reconciliation acceptance

At minimum compare:
- broker orders;
- executions/fills;
- cash/balance facts;
- positions;
- internal projected state.

Classify discrepancies:
- timing/expected;
- duplicate;
- missing internal event;
- missing external event;
- quantity mismatch;
- cash mismatch;
- unknown/unclassified.

Unknown or material mismatches trigger operational hold/escalation, not silent correction.

## AI shadow acceptance

AI shadow never sends live orders.

For every material proposal record:
- investor/risk profile version;
- market-data evidence IDs/timestamps;
- feature/model inputs;
- model/provider/version;
- prompt/template version where LLM involved;
- structured proposal;
- uncertainty/confidence fields where defined;
- Risk Governor result;
- Compliance result;
- mandate result;
- paper execution result if routed to PaperBroker;
- explanation/evidence hash.

### Zero-tolerance AI evals

- unauthorized order: 0;
- direct broker credential access: 0;
- mandate bypass: 0;
- risk/compliance bypass: 0;
- fabricated fill presented as real: 0;
- fabricated balance presented as authoritative: 0;
- untrusted web/news instruction treated as system instruction: 0.

## Performance baseline for paper path

Initial targets, measured independently from future real broker latency:
- deterministic authorization/risk/compliance validation p95 <= 150 ms combined target during normal load;
- Risk Governor p95 <= 50 ms target;
- internal event propagation p95 <= 500 ms target;
- idempotency lookup must not require unbounded scans;
- reconciliation jobs are paginated/batched and observable.

Targets may be revised only from measured evidence and documented decisions.

## Load profiles

Minimum test profiles:
- 500 concurrent simulated users;
- market-open burst;
- repeated order-status polling/event bursts;
- duplicate-event storm;
- reconciliation batch load;
- AI provider unavailable while financial core remains correct.

## Security tests

- BOLA/resource-ownership attempts;
- broken-function-level authorization attempts;
- replay/idempotency attacks;
- oversized/malformed payloads;
- rate-limit abuse;
- audit-log sensitive-data review;
- secret scanning;
- dependency audit;
- SAST/CodeQL;
- transaction concurrency cases;
- prompt-injection/tool-permission evals for AI shadow layer.

## Acceptance evidence

A P1 candidate report must contain:
- commit SHA;
- test/build workflow IDs;
- exact passed/failed tests;
- zero-tolerance invariant result;
- dependency/security scan result;
- performance test environment and measurements;
- known limitations;
- unreconciled exceptions count;
- rollback/recovery procedure used in failure tests.

## Explicit non-goal

Passing this specification does **not** prove pyPSX integration, regulatory approval, investment profitability or production readiness. It proves Klyvesta's internal paper/shadow safety baseline only.