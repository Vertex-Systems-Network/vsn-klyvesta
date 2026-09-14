# P1-10 Compliance Gate — Feature-Branch Checkpoint

Status: FEATURE-BRANCH IMPLEMENTATION / NON-LIVE / NOT P1 ACCEPTED / MAIN MERGE BLOCKED

Canonical issue: #76
Branch: `agent/20260902-vsn-klyvesta-p1-10-compliance-gate`
Stack base: P1-09 exact technically verified head `0cc52521ea02cebda1d2dfaf4ed7eeb17502d14c` from draft PR #75.

## Implemented slice

- provider-neutral deterministic paper compliance decision contract;
- versioned `PaperCompliancePolicy` evidence;
- account states: eligible, pending review, restricted, suspended, unknown;
- regulatory feature states: enabled, pending, disabled, unknown;
- manual review states: not required, approved, pending, rejected;
- Auto-simulation mandate states and effective-window checks;
- provider-neutral `IInstrumentRestrictionProvider` boundary;
- instrument outcomes: allowed, restricted, manual review, unknown;
- deterministic `ALLOW | DENY | HOLD` with ordered reason codes and policy version;
- broker-order keyed decision evidence;
- pre-side-effect `ComplianceGuardBrokerAdapter`;
- deterministic compliance verifier wired into dotnet-foundation CI.

## Fail-closed semantics

- definitive prohibited/restricted/suspended/disabled/rejected/revoked/expired evidence => DENY;
- unresolved/pending/unknown/manual-review-required critical evidence => HOLD;
- only complete compliant evidence => ALLOW;
- definitive DENY outranks simultaneous HOLD while secondary reasons remain observable;
- Auto simulation requires active in-window mandate when policy requires it;
- manual paper mode does not fabricate an Auto mandate requirement;
- no AI/caller override exists in the compliance contract.

## Explicit non-authority

This branch does not authorize or implement:

- live pyPSX mapping/API/auth/credentials;
- real-money execution;
- production customer PII;
- production KYC/AML provider integration;
- production legal/regulatory approval;
- personalized production advice;
- production mandate legal/signature workflow;
- authorization/BOLA ownership;
- production instrument-restriction data-source selection;
- ledger/accounting acceptance;
- AI execution authority;
- full P1 acceptance.

## Verification state

The implementation and verifier are candidates only until fresh exact-head PR checks pass. Required evidence includes repository-governance, dotnet-foundation, f2-postgres, CodeQL, zero unresolved review threads, and an exact-head non-authorizing self-review.

## Governance blockers

Issue #1 remains OPEN and hosted `main` remains unprotected. Do not merge this stack to `main` unless protected-main enforcement is verified or a new explicit owner risk decision is durably recorded.

Issue #20 / P0-T1 remains independently OPEN awaiting actual partner/API evidence.
