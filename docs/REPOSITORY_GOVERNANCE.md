# Repository Governance

Status: Required merge/release governance baseline. This document does not resolve repository visibility/licensing; Issue #2 remains an owner/legal decision.

## Protected branch

`main` is the production history branch and must be protected before substantive implementation is merged.

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

Once repository rules are configured, require these stable check names:

- `Build and architecture verify`
- `Analyze C#`

The workflows intentionally run on **every pull request head targeting `main`** so documentation-only commits cannot leave a required check permanently absent/pending.

Additional checks may become required as the platform grows, including database migration validation, unit/integration tests, API contract validation, secret/dependency review, client builds, and signed-artifact verification.

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

Configure `main` to:
- require pull requests;
- require required status checks;
- require branch to be current before merge where operationally practical;
- block force pushes;
- block branch deletion;
- require resolved review conversations;
- restrict bypass privileges to explicitly authorized emergency roles;
- retain auditability of administrative bypasses.

Signed commits may be required later if the organization can operate them reliably; signed release artifacts/provenance remain mandatory regardless.

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
- New runtime/build dependencies require maintenance, security, compatibility and license review.
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

Current public/GPL-3.0 repository status is tracked by Issue #2. Do not add proprietary investment algorithms, broker secrets, production credentials, customer data, or confidential partner documentation while that decision is unresolved.

Visibility/licence changes require explicit owner/legal direction. They are not an automatic engineering refactor.

## Current F1 draft

Draft PR #6 is intentionally not merge-authorized until:
1. required checks are green on the current PR head;
2. Issue #1 (`main` protection) is resolved or explicitly risk-accepted;
3. Issue #2 (visibility/licensing) is resolved or explicit owner/legal direction permits the intended model;
4. review confirms the PR remains within the approved generic foundation scope.
