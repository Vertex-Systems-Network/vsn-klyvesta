# Parallel Agent Development Foundation

Status: PLANNED / GOVERNANCE FOUNDATION

This document defines the reusable engineering model for running multiple AI or human engineering agents concurrently without turning shared-file contention, hidden dependencies, or stale instructions into development bottlenecks.

The machine-readable companion is `.ai/agent-orchestration.yaml`. `AGENTS.md` remains the mandatory entry point for every engineering agent.

## 1. Objective

Klyvesta should support parallel module development while preserving financial, regulatory, security, architectural, and audit boundaries.

The target operating range is:

- **8-10 concurrent agents** during the current foundation/P1 stage;
- **12-16 specialized agents** after ownership enforcement, automatic verifier discovery, dependency scheduling, and integration automation are mature;
- more agents are allowed only when the dependency graph exposes enough independent READY work and shared-file contention remains low.

Increasing agent count is not itself a speed strategy. Parallelism is permitted only when ownership and dependencies make the work independently reviewable.

## 2. Core rule: one module, one owner

Every active module has exactly one primary implementation owner at a time.

A module agent may modify only the paths granted to its work item. It must not edit another module's implementation to make its own task easier.

Cross-module interaction must use stable public contracts/interfaces. If a contract change is required, the module agent must record the dependency and route the shared change through the Platform/Integration ownership path.

Typical module ownership domains include:

- brokerage;
- orders/OMS;
- ledger;
- portfolio/reconciliation;
- risk;
- compliance;
- AI/quant shadow;
- identity/authorization;
- notifications;
- observability;
- security acceptance;
- performance/resilience;
- API/application composition;
- persistence/migrations.

## 3. Agent roles

### Module Agent

Owns one feature/module slice and its local verifier. It must not edit protected shared files unless the work item explicitly grants that path.

### Platform Agent

Owns shared engineering infrastructure such as CI workflow structure, package/build configuration, common scripts, verifier discovery, and other repository-wide mechanics.

### Database Integration Agent

Owns final EF migration generation, migration ordering, model snapshots, rollback verification, and schema integration. Feature agents specify schema requirements but should not concurrently rewrite shared migration snapshots.

### Integration Agent

Owns dependency-order integration, shared composition wiring, merge-train preparation, cross-module regression verification, and creation/advancement of stable integration baselines.

### Architecture / Conflict Agent

Checks module ownership, forbidden paths, dependency violations, circular dependencies, contract drift, duplicate implementation, migration collisions, and shared-file contention before integration.

### Verification / Security Agent

Runs independent acceptance/regression/security checks. It does not redefine the owning module's implementation merely to make tests pass.

## 4. Shared files

The following are treated as high-contention shared resources and are Platform/Integration-owned by default:

- `.github/workflows/**`;
- `Directory.Build.props`;
- `Directory.Packages.props`;
- solution-wide/project registration files when they are shared by multiple modules;
- API composition root / shared dependency registration;
- `.ai/state.json`;
- machine-wide orchestration manifests;
- shared contracts;
- EF model snapshot and final migration ordering.

A module agent must not edit a shared file unless its work item explicitly grants the path or the Platform/Integration Agent performs that integration step.

## 5. Automatic verifier discovery

Module verifiers should be independently addable without editing the central CI workflow on every feature branch.

Target convention:

```text
tools/Klyvesta.<Module>Verifier/Klyvesta.<Module>Verifier.csproj
```

The Platform Agent should implement reusable discovery/execution that restores, formats, builds, and runs verifier projects matching the approved convention. Until that automation lands, changes to shared workflow files remain Platform/Integration-owned.

## 6. Dependency DAG and READY work

The roadmap must be represented as a dependency graph, not an unnecessarily linear chain.

Each work item has at least:

- ID;
- module;
- owner role;
- base SHA or integration baseline;
- dependency IDs/contracts;
- allowed paths;
- forbidden/shared paths;
- status: `READY`, `ACTIVE`, `BLOCKED`, `VERIFYING`, or `VERIFIED`;
- acceptance commands/evidence expectations.

Independent READY work may run in parallel from the same stable integration baseline. Dependent work must not claim acceptance against an unverified dependency.

Example parallel wave:

```text
integration/p1-baseline
  |-- ledger agent
  |-- notifications agent
  |-- observability agent
  |-- security agent
  |-- performance tooling agent
```

Example dependency chain:

```text
portfolio contract -> risk -> compliance -> AI shadow
```

Only the dependent chain remains sequential; unrelated modules do not wait for it.

## 7. Stable integration baselines

Do not stack every new feature on the previous feature simply because it was implemented most recently.

Use a stable integration baseline for independent modules. A branch may stack on another feature only when there is a real contract/code dependency that cannot be satisfied from the current integration baseline.

Every work item must record its exact base SHA. Integration must compare the final branch against that base and verify that only intended paths changed.

## 8. Contract-first cross-module development

Module agents depend on interfaces/contracts rather than concrete implementations whenever practical.

Examples:

- `IBrokerAdapter` rather than a provider implementation;
- `IRiskGovernor` rather than a concrete risk engine;
- `IComplianceGate` rather than a concrete compliance engine;
- explicit portfolio/read-model contracts rather than internal storage details.

Contracts must be versioned when compatibility matters. A breaking contract change is a dependency event, not an invisible local refactor.

## 9. Database parallelism

Feature agents may add or update module-local schema specifications and persistence models where their work item permits it.

Final shared EF migration naming/order/model-snapshot integration belongs to the Database Integration Agent. This prevents multiple parallel branches from continuously colliding on the same migration snapshot.

A database change is not accepted until migration apply, rollback where required, drift checks, and relevant constraints pass on the integration candidate.

## 10. Work-item state instead of shared-state contention

Multiple agents should not continuously rewrite one global state file.

Target model:

```text
.ai/work-items/<work-item-id>.yaml
```

Each module agent owns only its work-item file. A generated/aggregated view may summarize overall state. `.ai/state.json` remains canonical for current project acceptance state until the generator/migration is implemented, but ordinary parallel module agents must not edit it unless explicitly authorized.

## 11. Instruction-drift protocol — mandatory

Every engineering agent, on **every task/session start**, must determine what instructions apply to its work before implementation.

At minimum it must re-read:

1. `AGENTS.md`;
2. `.ai/MASTER_ENGINEERING_PROMPT.md`;
3. `.ai/agent-orchestration.yaml`;
4. its work-item/issue and module ownership/dependencies;
5. `.ai/state.json`, guardrails and acceptance gates;
6. relevant module documentation/contracts;
7. current branch/base SHA and relevant open PRs.

The agent must explicitly check for **instruction drift** caused by changes to architecture, tooling, workflow, module ownership, dependency rules, testing commands, safety boundaries, or integration process.

If working instructions changed, the same PR must update the applicable canonical instruction source(s). At minimum:

- update `AGENTS.md` when agent behavior/process changed;
- update `README.md` when repository-level onboarding/workflow instructions changed;
- update `.ai/MASTER_ENGINEERING_PROMPT.md` when the global engineering protocol itself changed;
- update module documentation/README when module-specific working instructions changed;
- update `.ai/agent-orchestration.yaml` when ownership/dependencies/shared-file policy changed.

An agent must not leave new operating instructions only in chat, an issue comment, or its own memory.

If no instruction update is needed, the agent should record that the instruction-drift check was performed in its checkpoint/PR evidence.

## 12. Pre-work collision check

Before writing code, every module agent must check:

- whether another active work item owns the same module/path;
- whether its branch base is still valid;
- whether a dependency changed after the branch was created;
- whether it would need to touch a shared file;
- whether another PR already implements the same capability.

If collision is detected, the agent must stop modifying the conflicting path and route the dependency/integration change through the owning agent rather than creating competing implementations.

## 13. End-of-work requirements

A parallel-agent PR/checkpoint must record:

- work-item ID and module;
- exact base SHA and head SHA;
- owner role;
- intended/actual changed paths;
- dependency versions/heads used;
- instruction-drift check result;
- shared-file changes, if any, and authorization for them;
- tests/verifiers executed;
- unresolved conflicts or blockers;
- integration order/next action.

No module may be called integration-ready if it has unresolved ownership violations or depends on unverified contract behavior.

## 14. Recommended current concurrency

For the current stage, prefer a maximum working set around 8-10 agents:

1. active dependent feature agent (for example AI shadow);
2. ledger agent;
3. notifications agent;
4. observability agent;
5. security acceptance agent;
6. performance/resilience agent;
7. Platform/CI agent;
8. Integration/QA agent;
9. optional database integration agent;
10. optional architecture/conflict audit agent.

Do not run separate agents on Risk, Compliance, AI Shadow, and other tightly dependent modules from unrelated bases if their contracts are still moving. Parallelize independent modules first.

## 15. Scale-up gate to 12-16 agents

Scale beyond 10 concurrent agents only after these controls are operational and verified:

- path-ownership CI enforcement;
- dependency/work-item manifest validation;
- automatic verifier discovery;
- shared-file ownership enforcement;
- integration baseline/merge-train workflow;
- migration integration workflow;
- architecture/conflict checks;
- per-work-item state files/aggregation;
- instruction-drift evidence in PR/checkpoints.

## 16. M-AGENT implementation sequence

### M-AGENT-01 — Documentation + manifest

Land this document, `.ai/agent-orchestration.yaml`, and synchronized updates to `AGENTS.md`, `README.md`, and `AI-PLAN.md`.

### M-AGENT-02 — Ownership validator

Add CI/tooling that compares changed paths to the active work-item/module allowlist and fails unauthorized cross-module/shared-file changes.

### M-AGENT-03 — Automatic verifier discovery

Remove per-verifier central workflow edits by introducing convention-based restore/format/build/run discovery.

### M-AGENT-04 — Work-item registry + dependency readiness

Add `.ai/work-items/**`, schema validation, dependency statuses, base SHA tracking, and READY/BLOCKED validation.

### M-AGENT-05 — Integration baseline / merge train

Define integration branch lifecycle, dependency-order promotion, stale-base detection, and full regression requirements.

### M-AGENT-06 — Migration integration

Separate feature schema intent from final shared migration/model-snapshot integration.

### M-AGENT-07 — Conflict/architecture automation

Detect overlapping ownership, forbidden dependencies, circular references, duplicate capability implementation, and shared-file contention.

### M-AGENT-08 — Concurrency scale test

Exercise at least 8 independent synthetic/real work items through the orchestration model and measure conflict rate, CI duplication, stale-base failures, and integration throughput before raising the recommended concurrency ceiling.

## 17. Governance boundary

This delivery model accelerates engineering; it does not weaken product acceptance gates. Parallel agents cannot bypass regulatory, broker, security, financial integrity, Risk Governor, Compliance Gate, or production authorization requirements.
