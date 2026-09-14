# P1-20 Customer Planning Submission Checkpoint

Date: 2026-09-15
Module: `customer-planning`
Branch: `parallel/customer-planning`
Submission PR: #117
Accepted integration baseline: `2588281bdab108ce1c8a25f8d60204bb448808ad`
Pre-submission verified head: `d9974117c78bc7fc2049f056a5255b09e9f1097a`

## Scope delivered

- Deterministic customer-owned goal planning with strict authenticated-customer scope checks.
- Zero-return calendar arithmetic only; no expected-return model or fabricated market assumption.
- Goal progress comes from `CustomerGoal.CurrentAmount`.
- Paper portfolio book value is exact cost-basis context only and is never silently allocated to a goal.
- Monthly contribution requirement rounds upward to exact cents so arithmetic does not understate the required amount.
- Additional monthly contribution gap is derived from the existing customer profile contribution amount.
- Existing `CustomerRiskBand` is retained as context only; it cannot change arithmetic or override Risk authority.
- Planning authority is explicitly informational-only and cannot recommend investments, place orders, move money, override risk, dispatch notifications or create broker/provider authority.

## Verification before submission stamp

Pre-submission head `d9974117c78bc7fc2049f056a5255b09e9f1097a` passed:

- agent-orchestration;
- formatting verification;
- API graph build with zero warnings/errors;
- all 20 auto-discovered verifier builds;
- all 20 verifier runtime executions;
- `Klyvesta.CustomerPlanningVerifier` 30/30 cases.

The checkpoint-bearing commit must independently pass exact-head `agent-orchestration` and `dotnet-foundation` before PR #117 can be marked ready or merged.

## Ownership and exclusions

Changed implementation paths are restricted to P1-20-owned Planning domain/application code, its verifier, work item and this checkpoint.

Explicitly excluded:

- `src/Klyvesta.Api/**`;
- database persistence, migrations or model snapshots;
- shared `contracts/**` changes;
- expected-return assumptions or security selection;
- personalized investment advice;
- live market subscriptions;
- notification provider dispatch;
- broker or pyPSX transport/credential behavior;
- order placement, trade execution or money movement;
- production/live or real-money authority.

Production/live/provider authority remains fail-closed.
