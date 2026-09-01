# Multi-Agent Repository Workflow

This document is the canonical human-readable execution protocol for Supervisor-led parallel development.

## 1. Roles

### Supervisor

The agent operating the main repository acts as **Supervisor**.

The Supervisor:

- creates/reserves module branches before assigning parallel work;
- owns `parallel/supervisor-platform` for its own platform work;
- reviews every submitted module PR;
- owns merge/integration order;
- owns shared-file integration unless explicitly delegated;
- publishes refresh alerts after every accepted integration;
- never treats a merge as regulatory/production authorization.

### Module Agent

A module agent:

- works only on its assigned module branch;
- follows `.ai/agent-orchestration.yaml` ownership/dependencies;
- updates canonical instructions when instruction drift is discovered;
- submits a focused PR with exact-head evidence;
- announces the exact completion signal only when genuinely ready for Supervisor review.

### Database Integration Agent

Owns final EF migration ordering, model snapshots, integrated schema verification, and migration conflict resolution.

### Verification / Security Agent

Provides independent technical/security acceptance evidence and does not redefine product authority.

## 2. Immediate Supervisor bootstrap

Before documenting assignments or starting Supervisor feature work, the Supervisor creates/reserves every module branch intended for parallel execution.

Created branch inventory is canonical in `.ai/parallel-branch-registry.yaml`.

The Supervisor also maintains:

- own working branch: `parallel/supervisor-platform`;
- integration branch: `parallel/integration-staging`;
- target production/default branch: `main`.

No branch creation implies that work is automatically READY; dependency/existing-PR reconciliation still controls readiness.

## 3. Assignment and AI-native plan

Every assigned branch records:

- module;
- branch;
- agent slot/role;
- exact base SHA;
- dependency heads;
- allowed paths;
- shared/forbidden paths;
- current state;
- merge-order group;
- acceptance evidence required.

Independent modules start from the common accepted baseline. Dependent modules do not begin implementation until required dependency contracts/heads are accepted or the work item explicitly permits isolated contract-first work.

## 4. Completion signal

When an agent finishes its assigned task, it must post the exact phrase below as a top-level comment on its submitted PR:

**Work Done and Submitted**

The signal is valid only when:

- final intended commits are pushed;
- exact head SHA is recorded;
- required CI/checks have completed or any inability is explicitly recorded;
- instruction-drift check is recorded;
- changed paths match ownership;
- remaining limitations/blockers are disclosed.

Agents must not emit the phrase for partial work, draft experiments, or a branch known to require another implementation pass.

## 5. Supervisor interrupt handling

The Supervisor also works on its own platform module. When a valid `Work Done and Submitted` signal arrives:

1. **Checkpoint own work.** Record Supervisor branch/head, current work item, pending state, and exact resume action.
2. **Pause own implementation.** Do not continue making unrelated shared changes during submission review.
3. **Review submitted PR.** Verify scope, ownership, dependency freshness, architecture, tests, security, instruction drift, review threads, and exact head.
4. **Reject or return when needed.** Failed/ambiguous evidence means no integration.
5. **Integrate if approved.** Advance the accepted integration baseline in dependency order.
6. **Promote to `main` only when governance permits.** If hosted main protection or another canonical gate blocks promotion, integrate to `parallel/integration-staging` only and record the blocked promotion.
7. **Run/verify full relevant regression** on the advanced integration baseline.
8. **Broadcast refresh alert** through the canonical Supervisor Coordination Feed (Issue #82) and any active orchestration channel.
9. **Refresh Supervisor branch** against the newly accepted baseline when its own work depends on affected contracts/shared paths.
10. **Resume paused Supervisor work** from the saved micro-checkpoint.

## 6. Required Supervisor alert

After every accepted integration, the Supervisor sends exactly:

**New changes have been merged — please merge these changes into your branch first, then resume your own work.**

The same event must also include:

- integrated module;
- source PR and branch;
- new accepted baseline SHA;
- target used (`main` or `parallel/integration-staging`);
- changed contracts/shared paths;
- agents/modules materially affected;
- required revalidation, if any.

Canonical durable feed: GitHub Issue #82.

## 7. Agent response to refresh alerts

Before resuming after a refresh alert, every active agent must:

1. save/checkpoint any uncommitted work;
2. fetch the latest accepted baseline identified by the Supervisor;
3. merge/rebase that baseline into its module branch according to the repository merge policy;
4. resolve only conflicts within its ownership or escalate shared/cross-module conflicts to Supervisor/Integration;
5. update recorded base/dependency SHAs;
6. rerun tests affected by the refresh;
7. repeat instruction-drift and ownership checks if the integration changed contracts/process;
8. only then resume its assigned task.

An agent must not keep implementing against a stale accepted dependency baseline after receiving the alert.

## 8. Merge/integration strategy

### Independent modules

Independent READY modules may be implemented simultaneously. Supervisor integrates completed PRs one accepted baseline advance at a time.

### Dependency chain

Default chain:

`brokerage -> orders -> portfolio -> risk -> compliance -> ai-shadow`

Completion time does not override dependency order.

### Existing work reconciliation

Newly-created parallel branches for modules with existing draft PRs are RESERVED/BLOCKED until Supervisor reconciles that prior work. Do not recreate an implementation that already exists on another active branch.

### Shared files

Module agents do not opportunistically edit shared CI/build/state/contracts/composition/migration files. Platform/Integration/Database Integration owns those changes unless the work item grants an exception.

## 9. Supervisor review gate

Before integration, Supervisor verifies:

- correct branch/module/agent ownership;
- final diff is limited to intended paths;
- dependency heads are accepted and fresh;
- no duplicate active implementation exists;
- exact-head CI is green for required checks;
- local module verifier passes;
- inherited regression verifiers pass where applicable;
- security review is complete;
- no unresolved review threads remain;
- instruction-drift check is recorded;
- shared-file edits have explicit authorization;
- rollback/recovery implications are understood;
- financial/regulatory authority boundaries remain unchanged unless separately accepted.

## 10. Main vs integration-staging

The requested end-state is Supervisor-reviewed promotion into protected `main`.

However, the workflow is fail-closed:

- when `main` governance/protection permits promotion, accepted work may advance `main` through the required reviewed PR process;
- when promotion is blocked, the Supervisor advances `parallel/integration-staging` for technical integration/regression and records that `main` remains unchanged;
- no agent may interpret integration-staging as production authorization.

## 11. Supervisor message/state durability

Chat messages are not the source of truth. Important coordination state must be durable in GitHub/repository evidence.

Use:

- `.ai/parallel-branch-registry.yaml` for branches/agent slots/readiness;
- `.ai/agent-orchestration.yaml` for ownership/dependencies/process rules;
- Issue #82 for Supervisor merge/refresh events;
- PR comments for `Work Done and Submitted` completion events;
- checkpoints for resumable work state.

## 12. Instruction synchronization

At every task start and after every baseline refresh, agents re-check working instructions. If the workflow, branch map, module ownership, merge policy, commands, contracts, CI, safety boundaries, or dependencies changed, update the relevant README/instruction documents in the same PR.

## 13. Required terminal wording

A module agent that has actually completed and submitted its task ends its execution report with:

**Work Done and Submitted**

The Supervisor uses the phrase as an interrupt trigger only when it appears on the corresponding PR and the branch/head can be verified.
