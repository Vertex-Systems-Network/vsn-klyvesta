# F3 Deterministic Authorization & Withdrawal Security Core — Draft Checkpoint

Date: 2026-08-25

Status: **IMPLEMENTATION CANDIDATE / ISSUE #3 PARTIAL / P0-T1 ACTIVE / NOT MERGE-AUTHORIZED**

## Scope

This checkpoint covers the first deterministic security implementation slice tracked by Issue #10 under Critical Issue #3.

Implemented candidate boundaries:
- deny-by-default server-side authorization decisions;
- explicit customer resource ownership checks;
- roles separated from commercial entitlements;
- stable denial reasons;
- recovery high-risk state machine;
- action/principal/session-bound expiring step-up grants;
- verified withdrawal beneficiary and cool-off enforcement;
- recovery/new-device withdrawal restriction;
- maker-checker protected approval primitive;
- automated security policy tests;
- dedicated `security-baseline` CI workflow.

## Explicit non-scope

This slice does not implement or authorize:
- an identity provider;
- live passkey/WebAuthn enrollment or authentication;
- production customer sessions or PII;
- actual beneficiary/bank provider integration;
- actual withdrawals/funding;
- pyPSX/broker execution;
- real-money trading;
- personalized production AI advice;
- Guarded Auto;
- production launch.

## Security invariants targeted

- customer financial resources are owner-scoped server-side;
- UI/client-supplied ownership never grants access;
- subscription entitlement cannot elevate a denied security role;
- support/admin roles do not obtain customer withdrawal authority;
- AI/rebalance principals cannot submit broker orders;
- recovery/restricted-device states block withdrawal;
- withdrawal requires a verified beneficiary outside its cool-off window;
- step-up is scoped to the same principal, session and action and expires;
- maker cannot approve the same protected proposal;
- invalid recovery transitions fail closed.

## External engineering references reviewed

Primary guidance reviewed on 2026-08-25:
- Microsoft ASP.NET Core authorization and resource-based authorization guidance;
- Microsoft ASP.NET Core 10 passkey/WebAuthn support guidance;
- OWASP Authorization Cheat Sheet and authentication/session guidance;
- NIST SP 800-63B account recovery guidance.

The implementation remains provider-neutral and does not invent a custom WebAuthn/OAuth protocol.

## Governance blocker

`main` is still technically unprotected. The previous `F0-RISK-001` exception was explicitly limited to the completed foundation/P0 merge sequence and expires before this substantive security implementation.

Issue #1 has therefore been reopened. This F3 branch/PR must not merge to `main` until protected-main rules/required checks are actually enabled or a new explicit owner risk decision is durably recorded.

## Acceptance evidence required before this slice is complete

On the exact branch head:
- formatting PASS;
- Release build PASS;
- `Security policy tests` PASS;
- existing `Build and architecture verify` PASS on PR;
- CodeQL C# `security-extended` PASS on PR;
- PostgreSQL regression remains green if triggered;
- review confirms no live/real-money capability was introduced.

Until those checks exist, this checkpoint remains **NOT VERIFIED**.

## Canonical task preservation

`P0-T1 — Confirm regulatory and pyPSX Broker API operating model` remains ACTIVE. No P0/P1/P2/P3/P4 acceptance state is changed by this security slice.
