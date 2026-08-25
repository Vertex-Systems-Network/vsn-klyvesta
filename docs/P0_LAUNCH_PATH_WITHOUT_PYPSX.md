# P0 Launch Path While pyPSX Is Pending

Status: **Approved planning direction only. No real-money production capability is unlocked by this document.**

## Goal

Keep Klyvesta engineering moving while pyPSX partner/API terms remain unavailable, without guessing broker contracts or implementing regulated production behavior prematurely.

## Principle

Separate what can be built and proven independently from what requires an authoritative broker/regulatory contract.

```text
NO pyPSX RESPONSE
      !=
PROJECT BLOCKED
```

But:

```text
NO VERIFIED BROKER CONTRACT
      =
NO LIVE BROKER ADAPTER
NO REAL-MONEY TRADING
NO LIVE AI-ASSISTED ADVICE
NO LIVE DISCRETIONARY AUTO
```

## Track 1 — Foundation engineering

Can proceed independently after repository-governance approval:
- .NET financial-core scaffold;
- PostgreSQL schema/migrations;
- identity/session architecture;
- RBAC + ABAC + entitlements;
- idempotency;
- transactional outbox;
- immutable audit model;
- double-entry ledger engine;
- observability;
- secure configuration/secrets interfaces;
- API contracts;
- client application shells;
- CI/security gates.

Restrictions:
- no pyPSX-specific assumptions;
- no confidential partner data in public repository;
- no production secrets;
- no real-money feature flag enabled.

## Track 2 — Paper Broker / deterministic simulator

Build a `PaperBrokerAdapter` that implements Klyvesta's internal BrokerAdapter contract without pretending to emulate undocumented pyPSX semantics.

Must simulate at least:
- accepted order;
- rejected order;
- full fill;
- partial fill;
- multiple fills;
- cancel before fill;
- fill/cancel race;
- network timeout before side effect;
- ambiguous timeout after possible side effect;
- duplicate event;
- out-of-order event;
- stale quote;
- market closed;
- unavailable broker;
- rate-limit style response;
- reconciliation mismatch.

Purpose:
- prove OMS, ledger, idempotency, reconciliation and failure invariants before broker integration.

It must **not** claim to reproduce pyPSX until pyPSX contract tests exist.

## Track 3 — Paper portfolio / AI shadow system

Can proceed without live advice/execution if all outputs are explicitly non-production and no customer money is controlled.

Capabilities:
- historical market-data ingestion from legally usable sources;
- paper portfolios;
- model portfolios;
- portfolio optimization research;
- risk scoring;
- market-regime classification;
- backtesting;
- scenario/stress testing;
- AI explanation generation;
- shadow recommendations;
- shadow rebalance plans;
- AI evaluation and adversarial tests.

Hard boundary:

```text
AI/Quant
  -> proposal
  -> deterministic validators
  -> PAPER BROKER ONLY
```

No live customer financial side effect.

## Track 4 — Customer UX without live brokerage

Can design/build:
- registration/login shell;
- security center;
- KYC flow interface with provider abstraction/mock state;
- goal/risk questionnaire;
- portfolio dashboard using paper/demo data;
- AI explanation interface;
- investment timeline;
- notification preference center;
- Manual / AI Assisted / Auto mode UI with unavailable/eligibility states;
- deposit/withdrawal UI states as non-live mocks until funding contract exists;
- web, Android, iOS and Windows shells.

Production UX must never imply a feature is live merely because UI exists.

Required states:
- `AVAILABLE`;
- `PAPER_ONLY`;
- `WAITING_FOR_BROKER`;
- `REGULATORY_REVIEW`;
- `NOT_ELIGIBLE`;
- `TEMPORARILY_PAUSED`.

## Track 5 — Security / resilience

Can proceed fully:
- threat-model implementation tests;
- authentication/session controls;
- authorization matrix tests;
- rate-limit policies;
- secret-scanning/SAST/CodeQL/dependency audit;
- audit-log design;
- security event taxonomy;
- kill-switch interfaces;
- disaster-recovery runbooks;
- backup/restore strategy;
- abuse/fraud scenarios;
- device/client trust architecture;
- native application signing/release design.

## Track 6 — Regulatory evidence

Continue in parallel while waiting for pyPSX:
- current broker/adviser/research-analyst/securities-manager requirements;
- product-function classification;
- PSX IBTS/security obligations;
- customer money/custody responsibilities;
- market-data redistribution rights;
- complaint/audit/record-retention responsibilities;
- licensed-partner candidate analysis.

Do not treat regulatory research as final legal advice.

## Release lanes

### Lane A — Internal developer environment

Allowed:
- all generic foundation code;
- paper broker;
- synthetic/demo users;
- paper ledger;
- AI shadow system.

No production customer money.

### Lane B — Closed paper beta

Potentially allowed after security/privacy review:
- real users with paper portfolios only;
- AI education/explanation according to approved regulatory class;
- no live broker account/order side effects.

### Lane C — Manual real-money candidate

Locked until:
- P0 accepted;
- P1 paper/shadow accepted;
- broker sandbox E2E passed;
- reconciliation passed;
- security review passed;
- production certification/partner approval passed.

### Lane D — AI Assisted production

Locked behind Lane C plus accepted advisory classification/licensed path and recommendation/disclosure/evidence gates.

### Lane E — Guarded Auto production

Locked behind all prior lanes plus accepted discretionary-management path, mandate/IPS, eligibility, custodian arrangement, shadow-live validation, risk approval, controlled pilot and kill-switch testing.

## Engineering sequence while waiting

Recommended order:

```text
F0 repository governance
 -> F1 .NET foundation
 -> F2 PostgreSQL/migrations
 -> F3 identity/security
 -> F4 authorization/entitlements
 -> F5 ledger/idempotency/outbox
 -> F6 PaperBroker + OMS + reconciliation
 -> F7 deterministic risk/compliance paper engine
 -> F8 AI/quant shadow boundary
 -> F9 customer/client shells
 -> F10 notification system
 -> WAITING BOUNDARY
 -> pyPSX adapter mapping + sandbox contract tests
```

Do not implement live brokerage behavior past the waiting boundary from guesses.

## Product positioning while live integration is unavailable

Internal/pre-launch messaging may describe intended capability as roadmap, but public/customer-facing production claims must correspond to actually available and legally approved functionality.

Forbidden:
- `AI guarantees profit`;
- `you cannot lose`;
- `risk free`;
- `fully automated live investing` before authorization;
- implying custody or brokerage exists before contract/production acceptance.

## Exit condition

When pyPSX responds:
1. archive the received contract/documents in an approved private evidence store, not blindly in public Git;
2. map capabilities to `contracts/broker/BROKER_ADAPTER_V1.md`;
3. update P0 RACI and capability matrix;
4. identify every discrepancy from paper assumptions;
5. create provider-specific ADR(s);
6. obtain sandbox access;
7. implement `PyPsxBrokerAdapter` behind the stable internal interface;
8. run contract/failure/reconciliation certification tests;
9. only then consider P0/P2 acceptance changes.