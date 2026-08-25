# Checkpoint — Foundation Specification V1

Date: 2026-08-25

## Repository state / scope

- Active canonical product task remains `P0-T1 — Confirm regulatory and pyPSX Broker API operating model`.
- No roadmap phase was accepted or skipped.
- No live broker/real-money implementation was started.
- This session produced implementation-ready prerequisite specifications only.

## Completed work

Added:
- `docs/DOMAIN_DATABASE_MODEL_V1.md`
- `docs/AUTH_SESSION_ARCHITECTURE_V1.md`
- `docs/AUTHORIZATION_MATRIX_V1.md`
- `docs/ORDER_LEDGER_STATE_MACHINES_V1.md`
- `docs/API_CONTRACT_BASELINE_V1.md`
- `docs/FOUNDATION_IMPLEMENTATION_PLAN_V1.md`
- `contracts/openapi/klyvesta.v1.yaml`
- `contracts/broker/BROKER_ADAPTER_V1.md`
- `docs/adr/ADR-003-AUTHENTICATION-AND-FINANCIAL-API-SECURITY.md`
- `docs/adr/ADR-004-FINANCIAL-DATA-INTEGRITY-AND-STATE-MACHINES.md`

## Key decisions

### Identity/security
- standards-based central IdP; no custom auth protocol;
- passkeys/WebAuthn preferred;
- Web BFF + protected HttpOnly session cookie;
- native apps use system-browser Authorization Code + PKCE;
- FAPI 2.0 security profile is the high-value API target where supported/conformance-tested;
- account recovery creates explicit restricted financial state/cool-off;
- staff/admin plane remains separate.

### Data/integrity
- PostgreSQL 18.x supported patched release;
- UUIDv7 domain IDs;
- exact decimal financial values;
- immutable double-entry posted journals;
- compensating/reversal entries rather than historical edits;
- idempotency + transactional outbox;
- projections are rebuildable/traceable.

### Financial state machines
- explicit OrderIntent/BrokerOrder/Execution/reservation/withdrawal/mandate/reconciliation state machines;
- external ambiguity maps to `UNKNOWN`;
- no blind retry after possibly-successful broker call;
- duplicate execution has exactly one financial effect;
- reservation is retained while ambiguous state is reconciled.

### API/contracts
- schema-first versioned APIs;
- decimal financial fields encoded as strings;
- financial POST commands require `Idempotency-Key`;
- stable safe reason/error codes;
- object/resource authorization enforced server-side;
- pyPSX integration isolated behind Broker Adapter contract.

## Research performed

Verified current primary/official guidance:
- OpenID Foundation FAPI 2.0 Security Profile is Final (2025) and conformance tests/certification are available;
- ASP.NET Core 10 includes WebAuthn/passkey authentication support;
- ASP.NET Core Data Protection key persistence/encryption and rate-limiting guidance;
- PostgreSQL 18 exact `numeric`, constraints, transaction isolation/Serializable behavior and UUIDv7 support;
- .NET supports UUIDv7 generation through `Guid.CreateVersion7()`.

## Validation/checks

- Existing `AGENTS.md`, Master Engineering Prompt, guardrails, acceptance gates and latest security checkpoint were re-read before work.
- Existing authorization/security architecture was inspected and preserved.
- Open PR search returned none before/through the specification work.
- No application runtime exists, so compile/unit/integration/E2E tests are not applicable yet.
- OpenAPI contract was authored as a planning contract; automated OpenAPI lint/validation is a required F1/F9 CI gate once repository tooling exists.

## Security review result

The new specification closes several architecture gaps but does not constitute implementation security evidence.

Improvements specified:
- phishing-resistant/passkey-first auth;
- BFF/no long-lived browser bearer-token design;
- recovery/withdrawal step-up and cool-off;
- executable RBAC/ABAC/entitlement matrix;
- financial exactness and append-only accounting;
- ambiguous broker state fail-safe;
- broker capability/timeout/idempotency contract;
- API object-level authorization and decimal encoding.

## Known risks / blockers

Still unresolved:
- pyPSX Broker API technical/commercial docs and sandbox;
- exact pyPSX authentication/idempotency/status/webhook semantics;
- underlying broker/regulatory role and AI advice/discretionary permissions;
- Issue #1 main protection;
- Issue #2 visibility/licensing;
- Issues #3–#5 security remediation remain implementation work;
- final IdP/vendor selection and FAPI capability/conformance;
- no runtime/CI/security-test evidence yet.

## Acceptance result

**DONE — Foundation specification V1 only.**

**NOT DONE — product implementation, Phase 0, real-money integration, production security, AI Assisted or Guarded Auto.**

## Exact next action

Continue `P0-T1`: obtain pyPSX partner technical/commercial documentation. When received, map it against `contracts/broker/BROKER_ADAPTER_V1.md`, record gaps/unsupported capabilities, update threat/data-flow/state mappings, and only then decide whether the paper/shadow implementation task can be activated without violating acceptance gates.
