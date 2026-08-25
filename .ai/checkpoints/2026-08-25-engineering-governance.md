# Checkpoint — Engineering Governance Baseline

Date: 2026-08-25

## Repository state at checkpoint

- Branch: `main`
- Product status: `PLANNING`
- Active product task: `P0-T1 — Confirm regulatory and pyPSX Broker API operating model`
- Product roadmap changed: **No**
- Acceptance criteria changed: **No**

## Completed work

- Added `.ai/MASTER_ENGINEERING_PROMPT.md` as the canonical production-engineering process policy.
- Updated `AGENTS.md` to require the master prompt during every start/resume session.
- Defined precedence: Klyvesta-specific financial, regulatory, security, risk and accepted ADR rules override generic engineering preferences when stricter.
- Preserved the existing active task and all regulatory/product acceptance gates.
- Linked the engineering governance entry points from `README.md`.
- Recorded discovered repository-governance risks as GitHub issues instead of silently changing high-impact settings.

## Research performed

No external technical research was required for this governance-only change. The source requirement was supplied directly by the project owner. Existing repository architecture, security, risk, QA, roadmap, guardrails, machine-readable state, branch, recent history, open-PR state, repository visibility, licence metadata, and branch-protection state were inspected before completion.

## Validation performed

- Verified current branch is `main`.
- Verified there were no open PRs before the governance change.
- Verified existing product state remained `PLANNING` with active task `P0-T1`.
- Verified existing risk/security/financial authority boundaries before binding the generic engineering policy.
- Verified the canonical master prompt and updated `AGENTS.md` are readable from `main`.
- Documentation/governance-only change; no application build/unit/integration/security runtime tests were applicable.

## Security review

The new engineering policy does not weaken any financial or AI security boundary. `AGENTS.md` explicitly preserves the rule that LLMs cannot bypass deterministic financial authorities or directly control broker execution credentials/authoritative ledger state.

## Known failures / risks

- **High — Issue #1:** `main` is currently unprotected. Protect it before substantive implementation is treated as production engineering.
- **High — Issue #2:** repository is currently public and GPL-3.0; confirm intended visibility/licensing before significant proprietary implementation or third-party contribution.
- No application code exists yet, so development commands and runtime quality gates cannot yet be verified.
- pyPSX Broker API production/commercial documentation is still outstanding.
- Regulatory treatment of personalized AI advice and discretionary Guarded Auto remains unresolved and therefore production Auto remains blocked.

## Acceptance result

**DONE — Engineering governance baseline only.**

This checkpoint does **not** mark Phase 0, any product feature, AI-assisted investing, or Guarded Auto as accepted.

## Exact next action

Continue `P0-T1`: obtain and record pyPSX Broker API technical/commercial onboarding information and the regulated operating model. Resolve/confirm Issues #1 and #2 before substantive production implementation. In parallel, only roadmap-permitted paper/shadow foundation work may be planned without representing real-money or autonomous trading as production-ready.
