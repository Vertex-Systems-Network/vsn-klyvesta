# F4 Session & Device Security Checkpoint

Date: 2026-08-25
Status: implementation candidate on stacked branch
Parent: Issue #3
Slice: Issue #14
Base: validated F3 head `b5e59f93948975cad8e065ff8c813386dabe58d1`
Branch: `security/f4-session-device-state`

## Scope implemented

- provider-neutral authoritative security-session state;
- idle and absolute expiry evaluation;
- idempotent session revocation with preserved first reason/time;
- device trust, restriction, revocation and integrity-risk state;
- session/device ownership binding;
- fail-closed unknown-session and device mismatch handling;
- sign-out-all by principal;
- targeted revoke-by-device;
- recovery-completion session invalidation;
- staff privilege-change session invalidation;
- device integrity recorded as risk signal only, with no automatic trust elevation;
- focused security regression tests.

## Explicit non-scope

- IdP selection or integration;
- OAuth/OIDC token validation;
- passkey/WebAuthn implementation;
- browser cookies or native refresh tokens;
- production customer PII;
- broker/funding/withdrawal integration;
- real-money trading;
- protected-main remediation;
- self-hosted runner / runner infrastructure work.

## Acceptance required before slice can be called technically accepted

On the exact F4 head:
- formatting PASS;
- Release build PASS with warnings-as-errors;
- all security tests PASS;
- Foundation/architecture regression PASS;
- PostgreSQL regression PASS;
- CodeQL security-extended PASS.

Issue #1 remains the merge blocker. This checkpoint does not change P0/P1/P2/P3/P4 acceptance state.
