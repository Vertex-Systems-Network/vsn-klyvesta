# P1 Paper / Shadow Implementation Backlog

Status: **Implementation-ready backlog, but substantive code remains blocked by repository-governance Issues #1/#2.**

## Goal

Build and prove Klyvesta's internal financial invariants, security boundaries, paper brokerage, deterministic risk/compliance and AI shadow behavior before any live broker integration.

P1 must not depend on guessed pyPSX semantics.

## Entry conditions

Before substantive implementation is merged:

- protected `main` or explicit owner risk acceptance;
- repository visibility/licensing decision;
- F1 generic foundation accepted/merged;
- no live broker credentials in development;
- paper/reference market-data use rights understood for the selected datasets.

## Epic P1-01 — PostgreSQL / persistence baseline

Deliver:

- PostgreSQL 18 development/test setup;
- schema/migration tooling;
- UUIDv7 identifiers;
- exact numeric/decimal financial fields;
- UTC timestamps;
- optimistic/concurrency controls where appropriate;
- transaction helper boundary;
- migration rollback/test procedure.

Acceptance:

- clean database can migrate from zero;
- migration can be reapplied in CI;
- constraints reject invalid financial records;
- no floating-point authoritative money fields;
- test database isolation exists.

## Epic P1-02 — Identity/security abstraction

Deliver:

- provider-neutral identity boundary;
- customer/account security state;
- sessions/devices model;
- passkey-ready interfaces;
- step-up authentication policy;
- account recovery restrictions;
- privileged actor identity/audit context.

Do not lock to a commercial IdP before provider research/ADR.

Acceptance:

- financial commands require authenticated principal;
- account recovery can force `RESTRICTED` state;
- security state is server-authoritative;
- no security entitlement originates from client flags.

## Epic P1-03 — Authorization engine

Deliver decision input:

```text
principal
role
resource ownership
feature entitlement
account state
security state
regulatory feature gate
mandate state
risk/compliance state
```

Decision:

```text
ALLOW | DENY | STEP_UP_REQUIRED | HOLD
```

Acceptance:

- BOLA/resource ownership tests;
- broken-function-level-authorization tests;
- package tier never grants security role;
- staff privileges negative-tested;
- default deny for unknown critical attributes.

## Epic P1-04 — Immutable double-entry ledger

Deliver:

- accounts;
- journals;
- postings;
- reservations/holds;
- reversal/compensating entries;
- idempotent command processing;
- append-only audit linkage;
- invariant checker.

Core invariants:

```text
journal debits == journal credits
posted journal immutable
same idempotency key cannot duplicate financial effect
no fill -> no position/cash execution posting
```

Acceptance:

- property-based/invariant tests;
- concurrent duplicate command tests;
- rollback/failure injection;
- reversal preserves original entry;
- ledger imbalance zero tolerance.

## Epic P1-05 — Transactional outbox / event baseline

Deliver:

- database-backed outbox;
- event IDs;
- at-least-once safe consumer semantics;
- de-duplication;
- replay tooling;
- poison/dead-letter operating path;
- correlation/causation IDs.

Acceptance:

- DB commit with event cannot split-brain;
- duplicate delivery creates no duplicate financial side effect;
- replay is observable/audited.

## Epic P1-06 — PaperBrokerAdapter

Implement only Klyvesta-normalized broker contract.

Capabilities:

- balances;
- positions;
- submit/query/cancel orders;
- deterministic full/partial/rejected fills;
- ambiguous timeout simulation;
- duplicate/out-of-order events;
- outage/rate-limit simulation;
- market-closed/stale-data behavior.

Acceptance uses `contracts/broker/PAPER_BROKER_SCENARIOS_V1.yaml`.

## Epic P1-07 — OMS state machine

Deliver:

- OrderIntent;
- validation;
- approval/rejection;
- broker-order state;
- execution/fill de-duplication;
- reservations;
- cancel race handling;
- `UNKNOWN` recovery;
- reconciliation queue.

Acceptance:

- never blind-retry ambiguous side effect;
- partial fill math exact;
- fill/cancel races deterministic;
- duplicate event no duplicate ledger effect.

## Epic P1-08 — Portfolio projection / reconciliation

Authoritative financial history remains ledger + broker evidence; portfolio read models are projections.

Deliver:

- positions projection;
- cash projection;
- cost basis policy for paper mode;
- broker snapshot comparison;
- mismatch classification;
- freeze/escalation behavior.

Acceptance:

- projection rebuildable from source events;
- mismatch does not silently self-correct financial truth;
- critical mismatch can stop new orders.

## Epic P1-09 — Deterministic Risk Governor

Initial paper-only rules, config/version controlled:

- allowed instrument universe;
- max position concentration;
- sector concentration;
- liquidity eligibility;
- stale-data rejection;
- order value limits;
- portfolio exposure limits;
- max turnover/order rate;
- prohibited leverage/margin/shorting/derivatives;
- system-wide kill switch.

Do not hardcode production thresholds as business truth. Use reviewed policy versions and safe paper defaults.

Acceptance:

- AI cannot override denial;
- stale/unknown data fails closed;
- policy version persisted with every decision;
- kill switch tested.

## Epic P1-10 — Compliance Gate (paper mode)

Deliver:

- account/compliance status;
- regulatory feature gates;
- restricted/suspended states;
- mandate requirement for Auto simulation;
- instrument restriction interface;
- manual hold/review state.

No AI final authority.

## Epic P1-11 — AI / Quant shadow boundary

AI may produce only structured proposals.

Schema includes:

- proposal ID;
- portfolio/customer context reference;
- target allocation/actions;
- evidence references;
- uncertainty/confidence representation;
- model/prompt version;
- generated timestamp;
- data freshness;
- explanation data.

Flow:

```text
AI Proposal
 -> schema validation
 -> portfolio optimizer
 -> risk
 -> compliance
 -> shadow order plan
 -> PaperBroker only
```

Acceptance:

- no broker credential/tool available to AI runtime;
- prompt injection tests;
- fabricated balance/price/order tests;
- model outage cannot corrupt financial state;
- every material output auditable.

## Epic P1-12 — Notifications / investment timeline

Deliver event-driven timeline independent from WhatsApp/email/SMS delivery.

Channels:

- in-app authoritative timeline;
- email;
- WhatsApp;
- SMS for selected critical events.

Acceptance:

- notification failure does not lose financial event;
- duplicate event does not spam indefinitely;
- delivery state tracked separately from financial state;
- messages distinguish order submitted vs filled vs settled.

## Epic P1-13 — Observability / incident baseline

Deliver:

- OpenTelemetry traces;
- structured logs;
- request/correlation IDs;
- metrics;
- liveness/readiness;
- audit/security events;
- broker simulator health;
- reconciliation mismatch metrics;
- kill-switch telemetry.

Forbidden: secrets/raw PII/full financial payload dumping.

## Epic P1-14 — Security acceptance

Automate the baseline in `contracts/security/P1_SECURITY_ACCEPTANCE_V1.yaml`.

Required classes:

- authN/authZ;
- BOLA/BFLA;
- idempotency/replay;
- injection/input validation;
- secrets;
- PII logging;
- rate limiting;
- account recovery;
- admin privilege separation;
- AI tool boundary;
- supply-chain/static analysis;
- dependency audit;
- backup/restore;
- incident tabletop.

## Epic P1-15 — Performance/resilience

Initial validation targets:

- internal risk gate p95 <= 50 ms under test profile;
- order-intent validation p95 <= 150 ms;
- cached/read portfolio p95 <= 500 ms;
- duplicate command remains constant-time/efficient;
- broker outage produces bounded queues/backpressure;
- no runaway retries.

Load test market-open bursts and 10x expected order-intent spikes in paper mode.

## P1 exit gate

P1 can be accepted only when:

- ledger invariants pass;
- PaperBroker 20-scenario catalogue passes;
- authorization/security acceptance passes;
- deterministic risk/compliance tests pass;
- AI shadow evals pass;
- reconciliation recovery tests pass;
- security baseline/SAST/dependency checks pass;
- operational runbooks exist;
- no live broker or customer money is involved.

Passing P1 **does not authorize real-money trading**. P2 still depends on accepted P0 plus broker sandbox/production evidence.
