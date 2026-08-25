# F5 Session / Device Persistence Checkpoint

Date: 2026-08-25
Slice: F5 — Persist authoritative session and device security state
Tracker: #16
Parent: #3
Stack parent: PR #15 / validated F4 head `050a3a0d6e731da466f3faa2713698773203ee06`

## Scope implemented

- PostgreSQL `identity.security_device` record/model.
- PostgreSQL `identity.security_session` record/model.
- Composite session-to-device ownership foreign key over device id + principal id + principal type.
- State, authentication-strength, chronology, restriction and revocation constraints.
- Device/session lifecycle lookup indexes.
- EF Core migration, migration designer and model snapshot.
- Existing F2 PostgreSQL constraint gate now includes F5 SQL acceptance checks through `\\ir`; workflow/runner configuration is unchanged.
- F5 SQL covers valid persistence round-trip, preserved restriction/revocation audit evidence, cross-principal binding rejection, unknown-device rejection, invalid state/evidence rejection, idle timeout, expiry chronology and authentication-strength rejection, plus required index presence.

## Explicit non-scope

- Identity provider selection/integration.
- OIDC/OAuth token validation.
- Passkey/WebAuthn implementation.
- Browser cookie/native refresh-token implementation.
- Production PII.
- Live broker/funding/withdrawal integration.
- Real-money trading.
- Protected-main remediation.
- Self-hosted runner / runner infrastructure work.

Runner-related work remains deferred until the end per owner direction.

## Governance

Canonical active task remains `P0-T1 — Confirm regulatory and pyPSX Broker API operating model`.
F5 is allowed parallel `security_and_resilience` work only.
No P0/P1/P2/P3/P4 acceptance is changed by this slice.
No live or real-money feature is authorized.
Issue #1 remains the merge blocker for substantive security-sensitive work reaching `main`.

## Acceptance status

PENDING exact-head CI validation.

Required on final F5 head:
- EF `has-pending-model-changes` PASS.
- PostgreSQL migration apply PASS.
- F2 + F5 database constraint tests PASS.
- idempotent migration script PASS.
- rollback to zero PASS and re-apply PASS.
- Release build PASS.
- existing security tests PASS.
- Foundation / architecture regression PASS.
- CodeQL PASS.

## Exact continuation

Run full existing PR validation on the exact F5 head. Fix real failures without weakening constraints or changing acceptance criteria. After all checks pass, retarget the draft PR back to validated F4 branch, record evidence on #16 and #3, and leave merge blocked by #1.
