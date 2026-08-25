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

## Research performed

No external technical research was required for this governance-only change. The source requirement was supplied directly by the project owner. Existing repository architecture, security, risk, QA, roadmap, guardrails, machine-readable state, branch, recent history, and open-PR state were inspected before the change.

## Validation performed

- Verified current branch is `main`.
- Verified there were no open PRs before the governance change.
- Verified existing product state remained `PLANNING` with active task `P0-T1`.
- Verified existing risk/security/financial authority boundaries before binding the generic engineering policy.
- Documentation-only change; no application build/unit/integration/security runtime tests were applicable.

## Security review

The new engineering policy does not weaken any financial or AI security boundary. `AGENTS.md` explicitly preserves the rule that LLMs cannot bypass deterministic financial authorities or directly control broker execution credentials/authoritative ledger state.

## Known failures / risks

- No application code exists yet, so development commands and runtime quality gates cannot yet be verified.
- pyPSX Broker API production/commercial documentation is still outstanding.
- Regulatory treatment of personalized AI advice and discretionary Guarded Auto remains unresolved and therefore production Auto remains blocked.

## Acceptance result

**DONE — Engineering governance baseline only.**

This checkpoint does **not** mark Phase 0, any product feature, AI-assisted investing, or Guarded Auto as accepted.

## Exact next action

Continue `P0-T1`: obtain and record pyPSX Broker API technical/commercial onboarding information and the regulated operating model. In parallel, only roadmap-permitted paper/shadow foundation work may be planned without representing real-money or autonomous trading as production-ready.
