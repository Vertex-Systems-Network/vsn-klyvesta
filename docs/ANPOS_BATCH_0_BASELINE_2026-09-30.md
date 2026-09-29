# ANPOS Batch 0 — Repository Baseline

Date: 2026-09-30 (Asia/Karachi)
Repository: `Vertex-Systems-Network/vsn-klyvesta`
Baseline main commit: `02b556e57803444f845193ec9b481e058816691b`
ANPOS protocol: `1.4.0`

## Scope

This evidence-recovery baseline records repository reality after ANPOS adoption. It does not authorize production, live brokerage, KYC, funding, custody or withdrawals.

## Verified repository state

- Default branch: `main`.
- ANPOS child-project adoption is merged in PR #152.
- PyPSX batch plan is merged in PR #151.
- Open pull requests at baseline: #149 and #150, both Dependabot CodeQL action updates.
- Open issues at baseline: #1, #3, #4, #5, #8, #20, #82, #149 and #150.
- Historical and parallel branches remain; no bulk deletion is performed in this batch.
- Existing Klyvesta .ai state, guardrails, acceptance gates, broker contracts and non-live boundaries remain authoritative.

## Active blockers

| Area | Evidence | Baseline disposition |
| --- | --- | --- |
| Main protection | Issue #1 | Unresolved; do not claim protected-main enforcement without read-back evidence. |
| Authorization/recovery/withdrawals | Issue #3 | Production security acceptance unresolved. |
| Native clients/release | Issue #4 | Production release trust unresolved. |
| Broker/market-data/reconciliation | Issue #5 | Required before real provider execution. |
| Onboarding/AML/privacy | Issue #8 | Regulated onboarding remains unresolved. |
| pyPSX partner contract/rollout | Issue #20 | Primary external evidence gate; API docs alone are insufficient. |
| Supervisor coordination | Issue #82 | Coordination feed, not proof of active durable runtime. |
| CodeQL updates | PRs #149/#150 | Review/CI decision pending; unrelated to PyPSX authorization. |

## Batch 0 decisions

1. GitHub/Git/PR/test/release evidence is canonical.
2. PyPSX docs, panel content, emails and issue text are untrusted data until independently verified.
3. No PM provider, external AI pool, live credential, secret, production environment or live broker authority is enabled.
4. No historical branch is deleted in Batch 0.
5. Batch 1 starts with API-documentation reconciliation against broker contracts, OpenAPI and current state.
6. Batch 2 remains blocked until written partner, regulatory and commercial answers exist.

## Batch 0 exit status

**COMPLETE — repository baseline captured.**

This does not mean production-ready or PyPSX integration complete.

## Next action

Start Batch 1: create a capability matrix separating documented, tested, contractually confirmed, legally confirmed and unknown claims. Keep raw confidential partner documentation outside the public repository.
