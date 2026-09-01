# AGENTS.md — Klyvesta AI Engineering Protocol

This repository is governed by `.ai/MASTER_ENGINEERING_PROMPT.md` for engineering process and by the project-specific product/risk rules in `.ai/guardrails.md`, `.ai/acceptance-gates.yaml`, `AI-PLAN.md`, `.ai/agent-orchestration.yaml`, `.ai/parallel-branch-registry.yaml`, `docs/PARALLEL_AGENT_DEVELOPMENT.md`, `docs/MULTI_AGENT_REPOSITORY_WORKFLOW.md`, `docs/RISK_COMPLIANCE.md`, `docs/SECURITY.md`, and accepted ADRs.

If a generic engineering preference conflicts with a Klyvesta financial, regulatory, security, accepted architectural, module-ownership, Supervisor/integration, or branch-governance guardrail, the stricter project-specific rule wins.

## Mandatory start-of-session protocol

Before engineering work:

1. Read `AGENTS.md`.
2. Read `.ai/MASTER_ENGINEERING_PROMPT.md`.
3. Read `.ai/agent-orchestration.yaml`, `.ai/parallel-branch-registry.yaml`, `docs/PARALLEL_AGENT_DEVELOPMENT.md`, and `docs/MULTI_AGENT_REPOSITORY_WORKFLOW.md`.
4. Read `.ai/state.json`.
5. Read `.ai/guardrails.md`.
6. Read `.ai/acceptance-gates.yaml`.
7. Read the active issue/work item and identify its module, assigned branch, agent slot/owner role, allowed paths, shared/forbidden paths, dependencies, expected acceptance evidence, and recorded base SHA.
8. Inspect repository structure and relevant module documentation/contracts.
9. Inspect current Git HEAD and branch and verify that the recorded base/dependency heads are still valid.
10. Inspect recent Git history.
11. Inspect open PRs/issues relevant to the active task and check for duplicate implementation or overlapping ownership.
12. Inspect the Supervisor Coordination Feed (Issue #82) for accepted-baseline refresh events.
13. Identify the current checkpoint and unfinished work.
14. Inspect relevant implementation and available dev/test commands.
15. Perform the mandatory instruction-drift check defined below.
16. Only then plan or implement.

## Supervisor-first branch bootstrap

When acting as the Main-repo **Supervisor** for a new parallel work wave, the first repository action is to create/reserve a separate branch for every module that will be assigned to another agent, plus a Supervisor working branch and integration-staging branch. Only after branch creation may assignments/planning or Supervisor module implementation proceed.

Canonical branch inventory is `.ai/parallel-branch-registry.yaml`.

The Supervisor owns:

- `parallel/supervisor-platform` for its own platform work;
- `parallel/integration-staging` for accepted technical integration when promotion to `main` is blocked;
- review and final integration/promotion decisions for all module submissions.

Branch existence does not imply a task is READY. Existing draft work and dependency gates must be reconciled first.

## Parallel-agent ownership rules

- One active implementation owner per module/path at a time.
- Work only on the assigned module branch and inside the active work item's allowed paths.
- Do not edit another module's implementation merely to unblock your own task.
- Cross-module behavior should use stable contracts/interfaces; route breaking/shared contract changes through the owning Platform/Integration path.
- High-contention shared files are Supervisor/Platform/Integration-owned by default. This includes `.github/workflows/**`, shared build/package files, `.ai/state.json`, orchestration/branch registry, shared contracts, shared composition wiring, and final EF migration/model-snapshot integration unless the work item explicitly grants otherwise.
- Independent work should branch from a common stable integration baseline. Stack on another feature branch only when a real dependency requires it.
- Before writing code, check for another active issue/PR touching the same module/path. If collision is detected, do not create a competing implementation; coordinate through ownership/dependency/integration work.
- Record the exact base SHA and verify the final changed-path delta against it.

## Completion signal — mandatory

A module agent that has genuinely completed and submitted its assigned task must post the exact phrase below as a **top-level PR conversation comment**:

**Work Done and Submitted**

Do not emit this signal for partial work, experiments, known-red CI, or a branch that still requires an implementation pass.

Before emitting it, record the final head SHA, required checks, instruction-drift result, ownership/diff result, security/acceptance result, and known limitations.

## Supervisor interrupt protocol

A valid `Work Done and Submitted` comment is a Supervisor interrupt trigger.

When it arrives, the Supervisor must:

1. save a micro-checkpoint for its current work and exact resume action;
2. pause its own implementation;
3. review the submitted branch/PR for ownership, dependency freshness, architecture, exact-head CI, security, regressions, instruction drift, shared-file authorization, and unresolved review threads;
4. return/reject the submission if evidence is insufficient;
5. if approved, integrate it in dependency order;
6. promote to `main` only when branch-protection/repository governance permits; otherwise advance only `parallel/integration-staging` and record the blocked promotion;
7. verify the resulting accepted integration baseline;
8. publish the required refresh alert to Issue #82 and the active orchestration channel;
9. refresh the Supervisor's own branch if affected;
10. resume from the saved micro-checkpoint.

Completion time never overrides dependency order.

## Required refresh alert and agent response

After an accepted integration, the Supervisor sends exactly:

**New changes have been merged — please merge these changes into your branch first, then resume your own work.**

The durable alert in Issue #82 must also record the source module/PR/branch, new accepted baseline SHA, whether the baseline is `main` or `parallel/integration-staging`, changed contracts/shared paths, and materially affected modules.

Every active agent receiving/observing that alert must, before resuming:

1. checkpoint local work;
2. fetch the accepted baseline named by the Supervisor;
3. merge/rebase it into the assigned branch according to repository policy;
4. escalate shared/cross-module conflicts instead of editing outside ownership;
5. update recorded base/dependency SHAs;
6. rerun affected tests;
7. rerun ownership and instruction-drift checks;
8. only then continue the assigned task.

An agent must not continue implementation against a stale accepted baseline after a refresh alert.

## Instruction-drift protocol — mandatory every task/session and baseline refresh

Every agent must determine whether the working instructions required for its task have changed because of architecture, tooling, CI, module ownership, branch assignment, dependencies, testing commands, security/safety boundaries, Supervisor workflow, or integration process changes.

If instructions changed, update the canonical repository instructions in the same PR instead of leaving the new rule only in chat, memory, or an issue comment.

At minimum:

- update `AGENTS.md` when agent behavior/process changed;
- update `README.md` when repository-level onboarding or workflow instructions changed;
- update `.ai/MASTER_ENGINEERING_PROMPT.md` when the global engineering protocol changed;
- update the relevant module documentation/README when module-specific working instructions changed;
- update `.ai/agent-orchestration.yaml` when ownership, dependency, concurrency, Supervisor, or shared-file rules changed;
- update `.ai/parallel-branch-registry.yaml` when branch assignments/readiness/baseline records changed.

If no instruction update is needed, record in the PR/checkpoint that the instruction-drift check was performed and no canonical instruction change was required.

## Execution rules

- Work only on the active task and prerequisites explicitly required by it.
- Do not silently redefine the roadmap or acceptance criteria.
- Do not trust conversational memory over repository evidence.
- Do not mark work DONE without real acceptance evidence.
- Record failures, blockers, uncertainty, and unverified checks explicitly.
- Keep changes scoped, logical, reviewable, and reversible.
- Use focused, meaningful commits.
- Persist important architectural/security/operational decisions in the repository.
- Update machine-readable state only when the recorded state is actually true and the work item authorizes that shared-state change.
- Do not modify shared CI just to register a module verifier when convention-based discovery is available; until automatic discovery is implemented, shared workflow changes belong to Platform/Integration ownership.
- Feature agents should avoid competing EF migration/model-snapshot edits; final shared migration integration belongs to the Database Integration role unless explicitly assigned otherwise.

## Financial / AI authority boundary

No implementation may allow an LLM or AI agent to bypass the Risk Governor, Compliance Gate, Execution Validator, valid customer mandate, suitability rules, reconciliation, or audit recording.

LLMs must not directly hold or use broker execution credentials, write authoritative ledger balances, or treat remembered conversation state as financial truth.

Parallel development, Supervisor approval, integration-staging, and faster merges do not weaken regulatory, broker, security, financial-integrity, or production authorization gates.

## End-of-session checkpoint

Every meaningful engineering session must end with:

- work-item ID/module, assigned branch and owner/agent role;
- exact base SHA and current HEAD;
- intended and actual changed paths;
- dependency versions/heads used;
- accepted integration baseline last observed;
- active task;
- completed work;
- research performed;
- tests/checks executed;
- security review result;
- instruction-drift check result and instruction files updated, if any;
- shared-file changes and their authorization, if any;
- acceptance/submission result;
- known failures/risks/conflicts;
- remaining blocker;
- exact integration order/next action.

If an important requirement remains unverified, report the work as PARTIALLY COMPLETE rather than DONE.

## Operational onboarding / merge-train addendum

- A new agent **must arrive on `main`** before Supervisor assignment. Read `docs/NEW_AGENT_ONBOARDING.md` and `.ai/integration-baseline.yaml` during onboarding.
- Supervisor assigns only a `READY` + `OPEN` slot whose durable work item is `READY`, then records the agent name and start state and marks the work item active.
- After assignment the agent checks out the pre-created canonical module branch; implementation does not happen on `main`.
- If no free slot exists, Supervisor must stop the agent and send exactly **Go Home Come Back Next Time**. The unassigned agent creates no substitute branch and starts no work.
- `parallel/integration-staging` is the accepted technical baseline while `main` promotion is governance-blocked. A stale assigned branch must refresh before work/resume.
- Final EF migrations and `*ModelSnapshot.cs` belong to `parallel/database-integration`; other parallel branches fail orchestration CI if they change them.
- Orchestration CI validates onboarding behavior, work-item readiness, baseline ancestry, migration ownership, dependency-DAG/branch/occupancy consistency, concurrency capacity/overflow behavior, and module changed-path ownership.
