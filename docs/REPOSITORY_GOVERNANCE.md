# Repository Governance

Status: Required merge/release governance baseline.

Repository distribution/licensing is governed by `docs/REPOSITORY_LICENSING_MODEL.md`.

## Protected branch

`main` is the production history branch and should be protected before substantive production implementation is merged.

Normal changes must follow:

```text
feature/fix branch
  -> pull request
  -> required CI/security checks
  -> review
  -> resolved conversations
  -> merge
```

Direct day-to-day pushes to `main` are not an approved development workflow.

## Required pull-request checks

Required baseline check names:

- `Build and architecture verify`
- `Analyze C#`

Persistence-bearing changes additionally require:

- `PostgreSQL migration and constraints`

The workflows must run on pull requests targeting `main`, and the foundation/security/persistence workflows must also validate the merged `main` commit where applicable.

Additional checks become required as the platform grows, including unit/integration tests, API contract validation, secret/dependency review, client builds, signed-artifact verification and broker/reconciliation acceptance.

## Review policy

Baseline:
- at least one approving review when team membership supports independent review;
- author must not treat their own approval as independent review;
- unresolved review conversations block merge;
- Critical/High security, financial-integrity, regulatory or architectural findings block merge until fixed or formally risk-accepted by the correct owner;
- tests must not be deleted/weakened merely to obtain green CI.

High-impact financial/security changes should use maker-checker review. Examples:
- authorization policy changes;
- ledger/accounting invariants;
- OMS/execution behavior;
- Risk Governor / Compliance Gate logic;
- model promotion/execution permissions;
- withdrawal/security recovery behavior;
- broker credentials/integration trust controls;
- production secrets/KMS/IAM policy;
- release-signing configuration.

## Required branch protections / ruleset

Target `main` configuration:
- require pull requests;
- require required status checks;
- require branch to be current before merge where operationally practical;
- block force pushes;
- block branch deletion;
- require resolved review conversations;
- restrict bypass privileges to explicitly authorized emergency roles;
- retain auditability of administrative bypasses.

Signed commits may be required later if the organization can operate them reliably; signed release artifacts/provenance remain mandatory regardless.

## Temporary owner-approved governance exception — F0-RISK-001

As of 2026-08-25, GitHub reports `main` as unprotected and the connected GitHub application does not expose a branch-protection/ruleset write operation.

The owner has explicitly instructed the engineering session to resolve the current blockers and merge the accepted foundation work to `main`. That instruction authorizes a **one-time, narrowly scoped risk acceptance** for the current generic F1/F2 foundation and P0 due-diligence merge sequence.

This exception:
- does **not** claim that branch protection is technically enabled;
- does not weaken or waive CI/security acceptance;
- requires all relevant checks to be green on the integration heads and on the resulting `main` commits where workflows support it;
- permits only the already-reviewed generic foundation, persistence primitives and P0 documentation currently in PRs #6, #9 and #7;
- does not authorize proprietary strategy, live broker connectivity, customer PII, live personalized AI advice, Guarded Auto or real-money execution;
- must not be treated as a permanent substitute for GitHub branch protection.

Before substantive production/security-sensitive implementation is merged beyond this accepted foundation, an organization/repository admin should enable the target branch protections above. Until then, direct pushes remain prohibited by policy even though GitHub is not technically enforcing the rule.

## Emergency / break-glass changes

Emergency bypass is exceptional, not normal workflow.

Every emergency change must have:
- incident/ticket reference;
- named accountable actor;
- minimal scope;
- explicit reason normal PR flow cannot be used;
- post-change validation;
- after-action review;
- follow-up PR/documentation restoring normal state if applicable.

Never use emergency bypass merely to avoid a failing quality/security gate.

## Merge strategy

Prefer a merge method that preserves a clear, reviewable engineering history. Squash is acceptable for a PR whose commits are exploratory/noisy if the final commit message accurately captures intent. Preserve separate commits when their history is independently meaningful.

Never rewrite already shared `main` history merely for aesthetics.

## Dependency and workflow trust

- Third-party GitHub Actions must be pinned to immutable commit SHAs.
- Dependabot or controlled equivalent maintains GitHub Action references.
- New runtime/build dependencies require maintenance, security, compatibility and licence review under `docs/REPOSITORY_LICENSING_MODEL.md`.
- Lockfiles/SBOM/provenance become release gates as package dependencies and distributable artifacts are introduced.
- Workflow token permissions follow least privilege.
- CI must not expose production secrets to untrusted pull requests.

## Release governance

A production release must be built from reviewed repository history by an approved pipeline, not rebuilt ad hoc on an engineer workstation.

Release evidence should eventually contain:
- source commit SHA;
- CI results;
- dependency/SBOM metadata;
- build provenance;
- signed artifacts;
- migration/deployment plan;
- rollback plan;
- release notes;
- approvals required by change risk.

## Repository visibility and licence

The approved model is **split**:

- this repository remains public and GPL-3.0 for intentionally open/generic foundation, public contracts/examples and public due-diligence material;
- proprietary/confidential production components require a separately approved private repository/security boundary;
- existing GPL-3.0 history is not silently relicensed;
- confidential partner material, customer data, secrets and proprietary investment logic must not be committed here.

See `docs/REPOSITORY_LICENSING_MODEL.md` for the dependency and change-control policy.

## Current foundation merge authorization

The generic F1/F2 foundation merge is authorized only when:
1. required checks are green on the current integration head;
2. F0-RISK-001 is recorded for the current unprotected-`main` limitation;
3. the split public-GPL/proprietary-private licensing model remains in force;
4. review confirms the merge remains within the approved generic foundation/P0 scope;
5. no Phase 0 broker/regulatory gate is falsely marked accepted.
