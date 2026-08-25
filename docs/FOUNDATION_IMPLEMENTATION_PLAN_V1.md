# Foundation Implementation Plan V1

Status: Planned prerequisite/foundation sequence. **Not active implementation authority.** Active canonical task remains `P0-T1` until state is deliberately changed after acceptance/owner direction.

## Goal

Prepare a safe, test-first implementation sequence for Klyvesta's paper/shadow foundation without prematurely enabling broker real-money or Guarded Auto production behavior.

## Repository shape when coding begins

```text
/
  apps/
    web/                 # Next.js customer web/BFF
    admin-web/           # separate admin plane
    android/             # Kotlin + Compose
    ios/                 # Swift + SwiftUI
    windows/             # C# + WinUI 3
  src/
    Klyvesta.Api/        # ASP.NET Core customer API/BFF-facing API
    Klyvesta.AdminApi/   # separate admin API
    Klyvesta.Domain/     # pure domain model/invariants
    Klyvesta.Application/# use-cases/commands/queries
    Klyvesta.Infrastructure/
    Klyvesta.Persistence/
    Klyvesta.Broker.Abstractions/
    Klyvesta.Broker.PyPsx/      # only after contract mapping
    Klyvesta.Risk/
    Klyvesta.Compliance/
    Klyvesta.Ledger/
    Klyvesta.Reconciliation/
    Klyvesta.Notifications/
  ai/
    service/             # Python API/agent/quant service
    research/            # notebooks/scripts; never production authority
    tests/
  contracts/
    openapi/
    broker/
    events/
  tests/
    unit/
    integration/
    architecture/
    contract/
    security/
    performance/
  docs/
  .ai/
```

Exact .NET solution/project count should remain minimal; split only where a security/ownership/test boundary justifies it.

## Implementation order

### F0 — Repository safety prerequisite

Before substantive code:
- resolve/accept Issue #1 branch protection;
- decide Issue #2 repository visibility/licensing before proprietary production code is added;
- establish CI skeleton and required checks;
- secret scanning + dependency scanning;
- lock tool/runtime versions.

Acceptance:
- `main` protected/ruleset documented;
- CI executes on PR;
- no production secrets in repo;
- licensing/visibility decision recorded.

### F1 — Solution skeleton / architecture tests

Create:
- .NET solution and core projects;
- strict compiler/analyzer settings;
- nullable enabled;
- warnings policy;
- formatting/linting;
- architecture dependency tests preventing Domain -> Infrastructure/API dependencies;
- test projects.

No broker SDK yet.

Acceptance:
- clean restore/build/test from fresh checkout;
- dependency direction tests pass;
- no critical/high dependency vulnerability without explicit risk acceptance.

### F2 — Database/migration foundation

Implement only foundation entities first:
- users/subjects;
- customers;
- devices/sessions security references;
- portfolios;
- broker account placeholder/reference;
- ledger accounts/journals/lines;
- idempotency records;
- outbox;
- audit events.

Then trading/withdrawal/mandate entities after invariant tests exist.

Acceptance:
- migrate fresh from zero;
- rollback/forward test where practical;
- constraints tested;
- UUIDv7/exact decimal behavior tested;
- ledger balanced-posting invariant automated;
- posted ledger mutation denied by domain/persistence controls.

### F3 — Identity integration abstraction

Do not choose/build custom auth cryptography in Domain.

Implement application interfaces for:
- current principal;
- session/device security state;
- step-up evidence;
- entitlement resolution;
- authorization policy evaluation.

Integrate selected IdP only after vendor decision criteria pass.

Acceptance:
- same-user vs other-user authorization tests;
- revoked/recovered session states;
- staff/customer plane separation tests;
- no long-lived browser bearer storage.

### F4 — Authorization policy engine

Implement policies from `AUTHORIZATION_MATRIX_V1.md`.

Rules are server-side and matrix tested. Customer subscription entitlements remain separate from roles.

Acceptance:
- matrix-generated tests;
- cross-customer/BOLA zero tolerance;
- maker cannot approve own protected change;
- machine principals default deny.

### F5 — Ledger + idempotency + outbox

Implement before real order execution:
- double-entry posting service;
- reversal/correction workflow;
- idempotency service;
- transactional outbox;
- audit correlation.

Acceptance:
- property/invariant tests;
- concurrent duplicate financial command creates one effect;
- outbox crash/retry tests;
- ledger reconstruction tests.

### F6 — Paper OMS/state machines

Implement normalized:
- OrderIntent;
- BrokerOrder (paper adapter only);
- Execution;
- Cash/position reservations;
- Cancel/partial fill/UNKNOWN states;
- projections.

Create `PaperBrokerAdapter` to exercise the real adapter contract without live broker access.

Acceptance:
- happy/reject/partial/cancel flows;
- duplicate fill;
- timeout -> UNKNOWN -> reconciliation;
- cancel/fill race;
- reservation overspend race;
- property/state-transition tests.

### F7 — Risk/Compliance deterministic baseline

Implement hard deterministic framework, initially with conservative mock/paper policies:
- eligibility;
- account/KYC status;
- stale data fail-closed;
- concentration/liquidity limits;
- mandate boundaries;
- kill switches.

No LLM authority.

Acceptance:
- denied trade cannot reach adapter;
- stale market state cannot trade;
- inactive mandate cannot Auto trade;
- global/per-account pause test.

### F8 — AI/quant paper boundary

Python service can:
- read sanitized portfolio/market features;
- produce typed recommendation/order-intent proposals;
- record model/prompt/evidence versions.

It cannot:
- access ledger DB directly;
- hold broker credentials;
- set authorization/risk/compliance pass flags;
- execute orders.

Acceptance:
- schema validation;
- prompt-injection/adversarial tests;
- provider timeout fallback;
- malformed proposal has zero financial side effect.

### F9 — Customer paper APIs + clients

Implement OpenAPI contract incrementally:
- profile/session views;
- paper portfolio;
- paper manual order intents;
- recommendation review;
- timeline.

Native clients begin after API/auth foundation, sharing generated contracts/test vectors rather than financial logic.

Acceptance:
- contract tests;
- own/other resource tests;
- OpenAPI lint/compatibility;
- accessibility/security baselines per platform.

### F10 — Notification/timeline

Implement event projection + transactional outbox to notification abstraction.

Providers for Email/WhatsApp/SMS remain replaceable. No provider can mutate financial state.

Acceptance:
- financial transaction commits even if notification provider is down;
- retry/de-duplication;
- sanitized payload tests;
- mandatory security alerts cannot be disabled.

### F11 — pyPSX mapping (blocked until partner docs/sandbox)

After P0 information is received:
- complete `BROKER_ADAPTER_V1` capability matrix;
- map auth, account, order, execution, funding, market data, webhook semantics;
- implement adapter behind feature flag;
- contract/sandbox tests;
- reconciliation tests;
- threat-model delta review.

No production credential or real-money command before P0/P2 gates permit it.

## CI quality gates

Initial PR checks once code exists:
- format/lint;
- .NET restore/build with warnings policy;
- unit tests;
- architecture tests;
- Python lint/type/test for AI service;
- OpenAPI validation/lint;
- secret scan;
- dependency/SCA scan;
- migration-from-zero test;
- test coverage report for risk visibility (not a sole pass criterion).

Later gates:
- integration/PostgreSQL;
- security tests;
- container scan/SBOM/provenance;
- Android/iOS/Windows builds;
- E2E;
- performance;
- DAST;
- broker sandbox contract tests.

## Test data policy

- synthetic fixtures only in repository/CI;
- no production CNIC/bank/broker/customer data copied into tests;
- deterministic test clocks/market sessions where required;
- property-based generators for money/state-machine boundaries;
- broker simulators can inject delay, duplicate, reorder, timeout and malformed responses.

## Observability foundation

Every financial command/event must have:
- request ID;
- correlation ID;
- actor/principal ID;
- resource IDs;
- idempotency ID where applicable;
- policy/model/mandate versions as applicable;
- structured reason code;
- trace span around external dependency;

Do not log secret/token/raw sensitive PII.

## Deployment sequencing

1. database migration;
2. compatible backend release;
3. workers/services;
4. web/admin;
5. native clients;
6. feature flags progressively enabled.

For additive/contract-compatible releases prefer expand-before-contract database migration.

## Rollback principle

Application rollback must not require deleting/reversing valid financial facts. Database schema changes supporting a release must preserve compatibility across the documented rollout window.

## Work explicitly blocked

Until external acceptance exists:
- live pyPSX order submission;
- real customer fund movement;
- production personalized AI advice;
- production Guarded Auto;
- final broker field/status assumptions.

## Next activation rule

This plan becomes an implementation task only when `.ai/state.json` deliberately names a foundation implementation task or the owner explicitly directs implementation while remaining within acceptance gates. No AI session may silently reinterpret this planning file as permission to skip P0.
