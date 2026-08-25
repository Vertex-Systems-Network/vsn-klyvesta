# Main Foundation + P0 Integration Checkpoint

Date: 2026-08-25

Status: **P0 ACTIVE / GENERIC FOUNDATION MERGED / REAL-MONEY GATES CLOSED**

## Main foundation evidence

F1 + F2 generic foundation was merged to `main` through PR #6.

Foundation merge commit: `f326e38bddb4e021954a966793f2bb86d2ab9922`

The merge includes:
- .NET 10/C# foundation and architecture verifier;
- CodeQL security-extended workflow;
- PostgreSQL 18.6 EF persistence baseline;
- idempotency/inbox/outbox primitives;
- migration drift/apply/constraint/idempotent-script/rollback/re-apply acceptance;
- repository governance and split licensing model.

Main-level Foundation/PostgreSQL/CodeQL workflows are required to complete green before this checkpoint is treated as fully validated.

## Repository governance

Issue #1 is handled by explicit owner risk acceptance `F0-RISK-001`, not by claiming GitHub protection is enabled. Current connected tooling cannot write branch protection/rulesets. The exception is limited to the current foundation/P0 merge sequence; an admin should still enable protected-main controls before substantive production/security-sensitive implementation beyond this foundation.

Issue #2 is resolved by the owner-approved split model:
- this repository remains public GPL-3.0 for intentionally open/generic foundation and public due diligence;
- existing GPL history is not relicensed;
- proprietary/confidential production components require a separately approved private repository/security boundary;
- proprietary strategy, confidential partner material, customer data and production secrets must not be committed here.

## Canonical active task

`P0-T1 — Confirm regulatory and pyPSX Broker API operating model` remains **ACTIVE**.

pyPSX private technical/commercial onboarding evidence has not been received/verified. No live adapter semantics are guessed.

## Preserved blocked capabilities

This checkpoint does not unlock:
- live pyPSX adapter;
- real-money funding/trading;
- live customer onboarding/PII;
- personalized production AI advice;
- regulated AI research outputs without accepted path;
- Guarded Auto;
- production launch.

## Remaining security/remediation work

Production/security issues #3, #4, #5 and #8 remain open. They are not falsely closed by merging the generic foundation.

## Exact continuation

Continue P0-T1: obtain pyPSX Broker API technical/commercial onboarding evidence and map it against `contracts/broker/BROKER_ADAPTER_V1.md`, P0 RACI, funding/custody/data-rights flows, threat model and acceptance gates. Do not implement live broker semantics from public marketing assumptions.
