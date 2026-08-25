# F6 Withdrawal Security Technical Acceptance Checkpoint

Date: 2026-08-26
Branch: `security/f6-beneficiary-withdrawal-lifecycle`
Tracker: Issue #18
Draft PR: #19
Parent remediation: Issue #3
Validated stack parent: `security/f5-session-device-persistence` at `e8043c10e9b93049d9bd5124d74a64ab487f4c29`

## Scope accepted

F6 remains provider-neutral and does not authorize live funds movement. The accepted slice implements:

- versioned withdrawal beneficiary persistence with verification evidence references and cooling-off timestamps;
- exact beneficiary version + customer + destination-hash binding;
- immutable significant withdrawal transaction data/hash;
- requester/principal/session/operation-bound authorization snapshots with time-limited validity;
- maker-checker protection for protected withdrawal approval;
- fail-closed normalized withdrawal lifecycle including `SECURITY_HOLD`, `REJECTED`, `UNKNOWN`, `COMPLETED`, and `FAILED`;
- PostgreSQL lifecycle/immutability/submission-time enforcement triggers;
- final submission-time revalidation of authorization, security session/device, and beneficiary availability;
- adversarial PostgreSQL constraint/trigger tests;
- exhaustive domain transition-command matrix coverage across every `WithdrawalLifecycleState`.

## Candidate code head acceptance evidence

Candidate code head: `868e69291220608dcd25226ab193a7bfbe12ab94`

All required gates passed on that exact code head:

- `security-baseline` run `32901400472` — PASS. Formatting, security test build, and security test execution all passed, including the exhaustive withdrawal transition matrix.
- `dotnet-foundation` run `32901400515` — PASS. API graph build and architecture-boundary verification passed.
- `f2-postgres` run `32901400503` — PASS. PostgreSQL 18.6 verification, EF no-pending-model-drift check, migration apply, F2/F5/F6 database constraint tests, idempotent migration script generation, rollback-to-zero, rollback verification, and re-apply all passed.
- `codeql` run `32901400480` — PASS. C# analysis completed successfully.

## Acceptance criteria mapping

- Every allowed/forbidden withdrawal transition: PASS via state × transition-command matrix; denied commands assert `InvalidStateTransition` and no state mutation.
- Changed transaction data cannot reuse prior authorization: PASS in domain tests and exact transaction-hash persistence binding.
- Beneficiary ownership/version/cool-off: PASS in domain and PostgreSQL enforcement.
- No self-approval for protected withdrawals: PASS in domain and PostgreSQL enforcement.
- PostgreSQL invalid state/evidence/ownership/chronology combinations: PASS through adversarial SQL suite.
- EF pending model drift: zero; PASS.
- Migration apply / constraints / rollback-to-zero / re-apply: PASS.
- Existing security/foundation regressions: PASS.
- CodeQL: PASS.

## Acceptance result

**TECHNICALLY ACCEPTED ON PR / NOT MERGE-AUTHORIZED.**

This checkpoint and the corresponding canonical state update are documentation-only acceptance records. The commit containing them must itself receive exact-head CI verification before Issue #18 is closed and PR #19 metadata is finalized; the final issue/PR evidence records are authoritative for that final documentation-head verification.

## Remaining blockers / non-scope

- Issue #1 still blocks merge to `main`; protected-main controls are not enabled.
- `P0-T1` remains ACTIVE and pyPSX technical/commercial evidence is still required before live integration.
- No live bank/provider verification, pyPSX withdrawal semantics, funds movement, production customer PII, IdP/OIDC/passkey integration, workflow/runner changes, or real-money capability is authorized by F6.

## Exact continuation

1. Verify exact-head CI for the acceptance-record commit.
2. Record final exact-head run IDs on Issue #18 and PR #19.
3. Retarget PR #19 to the validated F5 stack parent only after exact-head verification.
4. Close Issue #18 as technically accepted while leaving PR #19 draft and merge-blocked by Issue #1.
5. Resume canonical active task `P0-T1`; do not silently advance to a future implementation task.
