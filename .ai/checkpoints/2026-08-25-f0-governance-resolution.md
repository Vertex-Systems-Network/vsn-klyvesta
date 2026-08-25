# F0 Repository Governance Resolution

Date: 2026-08-25

Status: **MERGE GOVERNANCE DECISION RECORDED**

## Owner direction

The owner explicitly instructed the engineering session to resolve the current repository blockers and merge the accepted foundation work to `main`.

## Issue #1 — main branch protection

Current GitHub evidence still reports `main` as unprotected and no repository ruleset exists.

The connected GitHub application exposes branch-protection/ruleset reads but no write operation, so the technical GitHub setting cannot be enabled from this engineering session.

Owner direction is therefore recorded as temporary risk acceptance `F0-RISK-001` for the current, narrowly scoped merge sequence only:

- PR #9 into its F1 parent;
- F1/F2 generic foundation into `main` through PR #6;
- P0 due-diligence/state documentation through PR #7.

This is **not** a claim that branch protection is enabled. It does not permit failed checks, production secrets, proprietary strategy, live broker code, customer PII or real-money behavior.

Before substantive production/security-sensitive implementation beyond this foundation is merged, an organization/repository admin should enable the protected-main rules in `docs/REPOSITORY_GOVERNANCE.md`.

## Issue #2 — repository visibility/licensing

The owner-approved operating model is the conservative split model documented in `docs/REPOSITORY_LICENSING_MODEL.md`:

- this repository remains public GPL-3.0 for intentionally open/generic foundation, public contracts/examples and public due-diligence material;
- existing GPL-3.0 history is not relicensed;
- proprietary/confidential production implementation belongs in a separately approved private repository/security boundary;
- confidential broker material, customer data, production secrets and proprietary investment logic must not be committed to this public repository;
- dependency licences are reviewed before adoption, with AGPL/SSPL/non-commercial/custom restricted terms blocked by default absent explicit approval.

## Acceptance boundary

This F0 governance decision authorizes merging the already-reviewed generic foundation/P0 documentation only. It does not change `.ai/acceptance-gates.yaml`, does not mark P0 complete, and does not unlock real-money integration, personalized AI advice or Guarded Auto.
