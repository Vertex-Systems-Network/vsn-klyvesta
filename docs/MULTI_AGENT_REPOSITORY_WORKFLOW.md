# Multi-Agent Repository Workflow

This document is the canonical human-readable execution protocol for Supervisor-led parallel development.

## 1. Roles

The Main-repo agent is the **Supervisor**. It allocates agents, reviews every submitted module PR, owns dependency-order integration, owns shared-file integration unless delegated, publishes baseline refreshes, and never treats technical integration as regulatory/production authorization.

Module agents work only on assigned canonical branches/paths and signal completion only when exact-head evidence is ready. Database Integration owns final EF migrations/model snapshots. Verification/Security provides independent evidence without redefining product authority.

## 2. Supervisor-first branch bootstrap

Before assignments or Supervisor implementation, create/reserve every module branch for the work wave plus `parallel/supervisor-platform` and `parallel/integration-staging`. Branch inventory/readiness/occupancy is canonical in `.ai/parallel-branch-registry.yaml`.

## 3. New agent onboarding

Every new agent must first arrive on **`main`**. The Supervisor immediately checks the AI-native plan for a free slot (`READY` + `OPEN` + READY work item).

If a slot exists, Supervisor deterministically assigns it, marks it `OCCUPIED`, records agent name/start status, marks the work item `ACTIVE`, and tells the agent to checkout the pre-created module branch. Before implementation that assigned branch must contain the current `parallel/integration-staging` baseline.

If no free slot exists, Supervisor stops the agent and says exactly:

**Go Home Come Back Next Time**

The unassigned agent starts no work and creates no substitute branch.

Detailed protocol: `docs/NEW_AGENT_ONBOARDING.md`.

## 4. AI-native plan/work items

Every active work item records module, branch, owner/slot, exact base/dependency heads, allowed/shared paths, current status and required acceptance evidence. Independent modules use the common accepted baseline. A dependency chain cannot be outrun merely because a later agent finished first.

## 5. Completion signal

A genuinely completed module submission posts this exact top-level PR comment:

**Work Done and Submitted**

It is invalid for partial work, known-red CI, experiments, or work that still needs an implementation pass.

## 6. Supervisor interrupt handling

On a valid signal the Supervisor checkpoints and pauses its own work, reviews ownership/dependencies/architecture/exact-head CI/security/instruction drift/review threads, returns insufficient work, and integrates approved work in dependency order. It advances `main` only when governance permits; otherwise it advances `parallel/integration-staging`. It then runs regression, records the accepted baseline, publishes the refresh alert, refreshes itself if affected, and resumes its checkpoint.

## 7. Required refresh alert

After every accepted integration the Supervisor sends exactly:

**New changes have been merged — please merge these changes into your branch first, then resume your own work.**

Issue #82 is the durable coordination feed and records source module/PR/branch, new accepted baseline SHA/target, changed shared contracts/paths and affected modules.

## 8. Agent response to refresh

Every active agent checkpoints, fetches the named accepted baseline, merges/rebases it into the assigned branch, escalates shared conflicts, updates base/dependency SHAs, reruns affected validation plus instruction-drift/ownership checks, then resumes. A stale accepted baseline blocks continued implementation.

## 9. Merge train / accepted baseline

`parallel/integration-staging` is the technical accepted-baseline branch while protected-main promotion is blocked. Accepted module work advances it **one reviewed item at a time**. Before every advance: exact-head CI, ownership/dependency freshness, architecture, security, migration ownership, instruction drift, review-thread status and full relevant regression must pass.

Machine baseline state: `.ai/integration-baseline.yaml`.

## 10. Dependency strategy

Independent READY modules may run concurrently. Default dependent chain is:

`brokerage -> orders -> portfolio -> risk -> compliance -> ai-shadow`

Existing draft implementations must be reconciled before parallel replacement work starts.

## 11. Migration integration

Feature/module agents do not own final EF migrations or `*ModelSnapshot.cs`. Those changes are accepted only on `parallel/database-integration` unless a Supervisor-recorded exception changes the canonical policy. Duplicate migration IDs and migration edits from other parallel branches fail orchestration CI.

## 12. Architecture/conflict automation

Orchestration CI validates unique canonical branches, unique explicit ownership patterns, known/acyclic dependency edges, registry/orchestration consistency, slot occupancy invariants, migration ownership, accepted-baseline ancestry and per-module changed-path ownership.

## 13. Concurrency capacity

Current planned active capacity is 6-10 independent specialized module agents, with scale toward 16 only after policy is updated and verified. CI simulates full-slot exhaustion and verifies that one extra agent receives exactly `Go Home Come Back Next Time`.

## 14. Main vs integration-staging

Protected `main` is the desired promotion target. If branch protection/governance is unresolved, technical integration may advance only `parallel/integration-staging`; this never grants live trading, PII, broker, regulatory or production authority.

## 15. Durable sources of truth

- `.ai/parallel-branch-registry.yaml`: branches/readiness/occupancy/agent names.
- `.ai/agent-orchestration.yaml`: process/ownership/dependency rules.
- `.ai/integration-baseline.yaml`: merge-train/baseline evidence.
- `.ai/work-items/**`: per-module state/evidence.
- Issue #82: accepted integration/refresh events.
- PR top-level comments: completion signal.
- checkpoints: resumable agent/Supervisor state.

## 16. Instruction synchronization

At every task start and after baseline refresh, agents re-check canonical instructions. Workflow/ownership/dependency/CI/security changes must be persisted in repository instructions in the same PR.
