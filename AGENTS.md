# AGENTS.md — Klyvesta AI Engineering Protocol

This repository is governed by `.ai/MASTER_ENGINEERING_PROMPT.md` for engineering process and by the project-specific product/risk rules in `.ai/guardrails.md`, `.ai/acceptance-gates.yaml`, `AI-PLAN.md`, `.ai/agent-orchestration.yaml`, `docs/PARALLEL_AGENT_DEVELOPMENT.md`, `docs/RISK_COMPLIANCE.md`, `docs/SECURITY.md`, and accepted ADRs.

If a generic engineering preference conflicts with a Klyvesta financial, regulatory, security, accepted architectural, module-ownership, or integration guardrail, the stricter project-specific rule wins.

## Mandatory start-of-session protocol

Before engineering work:

1. Read `AGENTS.md`.
2. Read `.ai/MASTER_ENGINEERING_PROMPT.md`.
3. Read `.ai/agent-orchestration.yaml` and `docs/PARALLEL_AGENT_DEVELOPMENT.md`.
4. Read `.ai/state.json`.
5. Read `.ai/guardrails.md`.
6. Read `.ai/acceptance-gates.yaml`.
7. Read the active issue/work item and identify its module, owner role, allowed paths, shared/forbidden paths, dependencies, expected acceptance evidence, and recorded base SHA.
8. Inspect repository structure and relevant module documentation/contracts.
9. Inspect current Git HEAD and branch and verify that the recorded base/dependency heads are still valid.
10. Inspect recent Git history.
11. Inspect open PRs/issues relevant to the active task and check for duplicate implementation or overlapping ownership.
12. Identify the current checkpoint and unfinished work.
13. Inspect relevant implementation and available dev/test commands.
14. Perform the mandatory instruction-drift check defined below.
15. Only then plan or implement.

## Parallel-agent ownership rules

- One active implementation owner per module/path at a time.
- Work only inside the active work item's allowed paths.
- Do not edit another module's implementation merely to unblock your own task.
- Cross-module behavior should use stable contracts/interfaces; route breaking/shared contract changes through the owning Platform/Integration path.
- High-contention shared files are Platform/Integration-owned by default. This includes `.github/workflows/**`, shared build/package files, `.ai/state.json`, shared contracts, shared composition wiring, and final EF migration/model-snapshot integration unless the work item explicitly grants otherwise.
- Independent work should branch from a common stable integration baseline. Stack on another feature branch only when a real dependency requires it.
- Before writing code, check for another active issue/PR touching the same module/path. If collision is detected, do not create a competing implementation; coordinate through ownership/dependency/integration work.
- Record the exact base SHA and verify the final changed-path delta against it.

## Instruction-drift protocol — mandatory every task/session

Every agent must determine whether the working instructions required for its task have changed because of architecture, tooling, CI, module ownership, dependencies, testing commands, security/safety boundaries, or integration process changes.

If instructions changed, update the canonical repository instructions in the same PR instead of leaving the new rule only in chat, memory, or an issue comment.

At minimum:

- update `AGENTS.md` when agent behavior/process changed;
- update `README.md` when repository-level onboarding or workflow instructions changed;
- update `.ai/MASTER_ENGINEERING_PROMPT.md` when the global engineering protocol changed;
- update the relevant module documentation/README when module-specific working instructions changed;
- update `.ai/agent-orchestration.yaml` when ownership, dependency, concurrency, or shared-file rules changed.

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

Parallel development and faster integration do not weaken regulatory, broker, security, financial-integrity, or production authorization gates.

## End-of-session checkpoint

Every meaningful engineering session must end with:

- work-item ID/module and owner role;
- exact base SHA and current HEAD;
- intended and actual changed paths;
- dependency versions/heads used;
- active task;
- completed work;
- research performed;
- tests/checks executed;
- security review result;
- instruction-drift check result and instruction files updated, if any;
- shared-file changes and their authorization, if any;
- acceptance result;
- known failures/risks/conflicts;
- remaining blocker;
- exact integration order/next action.

If an important requirement remains unverified, report the work as PARTIALLY COMPLETE rather than DONE.
