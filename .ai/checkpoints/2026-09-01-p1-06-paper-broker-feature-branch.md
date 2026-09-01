# 2026-09-01 — P1-06 PaperBrokerAdapter Feature-Branch Checkpoint

Status: **FEATURE-BRANCH IMPLEMENTATION / NON-LIVE / NOT P1 ACCEPTED / MAIN MERGE BLOCKED**

- Canonical work item: GitHub Issue #59 — `P1-06: Implement deterministic PaperBrokerAdapter (non-live)`
- Draft PR: #68
- Branch: `agent/20260901-vsn-klyvesta-p1-06-paper-broker`
- Base main at branch creation: `9ee91496e9e606a050de14142cf225640a94406c`
- Production-authorizing: `false`
- Real-money-authorizing: `false`
- pyPSX mapping verified: `false`

## Why this lane is admissible while P0-T1 waits

The owner confirmed on 2026-09-01 that the direct pyPSX/Broker API package has not yet been received and is expected in roughly 2–3 weeks. That timing is not partner evidence and does not change P0 acceptance. Issue #20 remains OPEN/fail-closed.

P1 Paper/Shadow work is explicitly non-live and may be developed independently from P0 provided it does not infer undocumented broker semantics. This implementation therefore uses only Klyvesta's normalized internal broker contract and explicit paper-only controls.

## Implemented scope

### Normalized application boundary

`src/Klyvesta.Application/Brokerage/BrokerContracts.cs` defines:

- broker result/environment/order-state enums;
- exact-decimal submit-order command;
- normalized operation envelope;
- capabilities and health snapshots;
- order/execution/balance/position snapshots;
- `IBrokerAdapter` operations for capabilities, health, balances, positions, submit, query and cancel.

No provider-specific DTO, pyPSX endpoint, credential or undocumented external status is embedded in the application contract.

### Deterministic paper adapter

`src/Klyvesta.Infrastructure/Brokerage/Paper/` implements:

- deterministic in-memory paper order state;
- exact-decimal fill/cash/position arithmetic;
- full, partial, multiple and rejected fill outcomes;
- stable execution identities;
- duplicate execution and duplicate event de-duplication;
- command idempotency, replay and conflict detection;
- known pre-side-effect failures as `RETRYABLE_FAILURE`;
- ambiguous response loss after possible/actual side effect as `UNKNOWN` with no blind duplicate submit;
- query recovery after `UNKNOWN`;
- cancel-before-fill and partial-fill/cancel race preservation;
- monotonic handling of delayed/out-of-order status events;
- market-closed and unavailable fail-closed behavior;
- explicit paper-only profile/event controls outside the application/domain contract.

The adapter simulates broker-side paper facts only. It does not become Klyvesta's authoritative double-entry ledger and does not absorb authorization, risk, compliance or mandate policy.

## Deterministic verifier scope

`tools/Klyvesta.PaperBrokerVerifier` is dependency-free beyond repository project references and is wired into `dotnet-foundation` CI.

Declared adapter-slice assertions:

- PB-001 full fill;
- PB-002 rejected order;
- PB-003 partial fill;
- PB-004 multiple fills + duplicate execution financial-effect prevention;
- PB-005 duplicate command / idempotency conflict;
- PB-006 known pre-side-effect timeout classification;
- PB-007 ambiguous post-accept timeout -> `UNKNOWN` + query recovery + no duplicate order;
- PB-008 cancel before fill;
- PB-009 fill/cancel race with executed quantity preserved;
- PB-010 duplicate broker event;
- PB-011 out-of-order status must not regress terminal state;
- PB-013 market closed;
- PB-014 broker unavailable and observable health failure.

These are **adapter-slice assertions**, not full end-to-end acceptance of those catalogue scenarios where ledger/reconciliation/policy evidence is also required.

## Explicitly not accepted in this lane

The verifier deliberately reports full P1 as NOT ACCEPTED. Wider integration remains required for:

- PB-012 stale market data — execution policy / market-data freshness gate;
- PB-015 reconciliation mismatch — reconciliation engine and operational hold;
- PB-016 ledger persistence failure — transactional double-entry/outbox/recovery evidence;
- PB-017 unauthorized customer resource — resource authorization/BOLA boundary before broker call;
- PB-018 auto without mandate — mandate gate before broker call;
- PB-019 risk breach — Risk Governor before broker call;
- PB-020 compliance hold — Compliance Gate before broker call.

Full PB-001/PB-003/etc. P1 scenario acceptance also still requires their non-adapter ledger, portfolio, audit and reconciliation evidence where specified by `docs/PAPER_SHADOW_ACCEPTANCE_SPEC_V1.md`.

## Verification history on this branch

An initial PR-head run reached formatting successfully but failed the API build because warnings-as-errors promoted analyzer rule `CA1822` for a state-transition helper. The helper was correctly made `static`; no analyzer suppression or quality-gate bypass was introduced.

This checkpoint itself changes the exact branch head. Therefore final technical evidence must come from fresh PR checks on the exact head containing this checkpoint. Required lanes remain:

- `repository-governance`;
- `dotnet-foundation` including the PaperBroker verifier;
- `f2-postgres` regression lane;
- `CodeQL`.

A later PR self-review/comment may record the exact successful run numbers for this checkpoint head. Until those exact-head checks pass, technical verification remains pending; earlier-head checks are not substituted.

## Governance blockers

GitHub Issue #1 remains OPEN. Hosted `main` has been read back as unprotected, and the prior F0 risk acceptance is expired for substantive implementation. Per repository governance, this branch/PR may be developed and reviewed but **must not merge to `main`** absent either:

1. verified effective protected-main/ruleset enforcement satisfying Issue #1; or
2. a new explicit owner risk decision covering this substantive implementation.

Issue #20/P0-T1 also remains OPEN for direct pyPSX technical/commercial evidence, but it is not being bypassed or fabricated by this paper implementation.

## Next safe step

Obtain fresh exact-head CI/security results for PR #68, perform SELF REVIEW against the normalized broker contract and paper/shadow acceptance spec, and leave the PR draft/merge-blocked by Issue #1. After that, continue another explicitly independent P1 paper/shadow prerequisite only if it does not create live/provider/regulatory authority.
