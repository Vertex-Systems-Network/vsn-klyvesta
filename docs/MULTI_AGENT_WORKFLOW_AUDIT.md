# Multi-Agent Repository Workflow — Deep Audit

## Executive finding

The supplied Supervisor/module-agent model is directionally correct for accelerating Klyvesta, but the raw workflow requires governance, dependency, and durability controls before it is safe for 8-16 concurrent agents.

The audited design preserves the requested behavior while adding fail-closed integration rules:

- Supervisor creates module branches before assignment/work.
- Every module has one owner branch/agent slot.
- Agent completion is signaled by the exact phrase `Work Done and Submitted` on the submitted PR.
- Supervisor interrupts its own module work when a completion signal arrives, reviews the submission, integrates approved work, publishes the exact refresh alert, and then resumes its prior task.
- Agents receiving a refresh alert must update their own branch to the new integration baseline before resuming.
- Direct promotion to `main` occurs only when repository governance/branch-protection gates permit it; while the current hosted `main` protection gate is unresolved, accepted integration is staged on `parallel/integration-staging` and promotion remains blocked.

## Audit findings

### A1 — Branches must exist before assignment

Status: IMPLEMENTED.

Supervisor-created branches are recorded in `.ai/parallel-branch-registry.yaml`. They were created from the recorded `main` baseline before the workflow documentation/assignment phase continued.

### A2 — Raw direct-main merge is unsafe under the current repository gate

Risk: CRITICAL governance conflict.

The requested workflow says the Supervisor merges approved work into `main`. Klyvesta currently records hosted `main` branch protection as unresolved. Therefore a literal unconditional main merge would contradict the existing fail-closed repository governance.

Resolution:

1. Supervisor still owns all final merge/promotion decisions.
2. Approved work is first integrated into `parallel/integration-staging` when `main` promotion is blocked.
3. Full regression is run on the integration baseline.
4. Promotion to `main` happens only when branch-protection/governance conditions are satisfied or a new explicit owner risk decision authorizes a narrowly scoped exception.
5. The coordination alert states whether the change reached `main` or only integration staging.

### A3 — Existing draft/stacked PRs create duplicate-work risk

Risk: HIGH.

Brokerage, Orders, Portfolio, Risk, Compliance, and AI Shadow already have active/draft implementation history. Starting new agents from newly-created clean module branches without reconciliation could duplicate or conflict with that work.

Resolution:

- Those newly-created parallel branches are reserved but initially BLOCKED in the registry.
- Supervisor must reconcile/promote or deliberately supersede the existing draft work before assigning new implementation on those branches.
- Independent modules such as Ledger, Identity/Authorization, Notifications, Observability, and Performance/Resilience may be scheduled in parallel where their work items are READY.

### A4 — Conversation-only alerts are not durable

Risk: HIGH coordination loss.

An agent may not receive or retain a transient conversational alert.

Resolution:

Issue #82 is the canonical Supervisor Coordination Feed. Every accepted integration event is posted there with:

- exact required refresh alert;
- merged module;
- source branch/PR;
- new integration/main SHA;
- changed shared contracts/paths;
- promotion status.

Every agent must check the feed at session start and before resuming after an interruption.

### A5 — Completion signal needs an unambiguous carrier

Risk: MEDIUM.

`Work Done and Submitted` without a canonical location can be missed or falsely inferred.

Resolution:

The exact phrase must be posted as a top-level PR conversation comment only after the agent has:

- pushed final intended changes;
- recorded exact head SHA;
- completed its local/exact-head checks;
- recorded instruction-drift result;
- marked remaining limitations/blockers.

A draft or partial implementation must not emit the phrase.

### A6 — Supervisor interrupt/resume needs a checkpoint

Risk: MEDIUM.

Stopping supervisor work for every incoming submission can cause lost local context or half-finished shared changes.

Resolution:

Before switching to review, the Supervisor records a resumable micro-checkpoint containing:

- supervisor branch/head;
- current work item;
- dirty/in-progress files or pending commit;
- exact next action;
- tests currently valid/invalidated.

After integration and broadcast, Supervisor rechecks its branch against the new integration baseline, refreshes dependencies if required, and resumes from that checkpoint.

### A7 — Blindly merging latest main into every branch can increase conflicts

Risk: HIGH.

For every integration event, forcing all agents to merge arbitrary `main` can produce needless merge commits, dependency churn, and accidental acceptance of unrelated incomplete work.

Resolution:

Agents refresh from the canonical accepted integration baseline identified by the Supervisor alert. When governance permits and `main` is that baseline, they merge/rebase `main`; otherwise they refresh from `parallel/integration-staging`.

The required semantic remains: no agent resumes on a stale accepted baseline.

### A8 — Dependency order must override completion order

Risk: HIGH.

An agent may finish before a prerequisite dependency. Completion time alone cannot decide merge order.

Resolution:

Supervisor integrates only when dependency heads are accepted and fresh. The canonical dependency chain is:

`brokerage -> orders -> portfolio -> risk -> compliance -> ai-shadow`

Independent READY modules may integrate in any reviewed order, one accepted baseline advance at a time.

### A9 — Shared files require a single writer role

Risk: HIGH merge conflict.

Central CI, package/build props, shared contracts, composition wiring, state, and migration snapshots are high-contention files.

Resolution:

Module agents do not modify shared paths unless their work item explicitly grants it. Platform/Integration/Database Integration roles own these areas. Module agents provide required integration specs/contract requests instead of editing shared files opportunistically.

### A10 — Merge success must not equal product/phase acceptance

Risk: CRITICAL for a regulated financial platform.

Resolution:

Supervisor approval/integration means only that the submitted engineering slice satisfied its declared technical gate. It does not unlock regulatory acceptance, live broker access, production PII, real-money execution, AI authority, Guarded Auto, or full P1 acceptance unless the corresponding canonical gates are separately satisfied.

## Concurrency recommendation after audit

### Current safe operating target

Use 6-10 concurrent roles, with 8-10 as the practical ceiling when enough independent READY work exists.

Recommended immediate lanes:

1. Supervisor / Platform
2. Ledger
3. Identity / Authorization
4. Notifications
5. Observability
6. Performance / Resilience
7. Verification / Security
8. Database Integration
9. Integration / merge-train support when needed
10. One dependency-chain module only when its accepted prerequisites are available

### Mature target

12-16 agents only after automated path ownership, dependency freshness, verifier discovery, work-item readiness, migration integration, conflict detection, and merge-train checks are operational.

## Acceptance criteria for the workflow itself

The multi-agent workflow is operational when:

- branch registry exists and matches remote branches;
- every active work item records branch, owner, module, base SHA, dependencies, allowed/shared paths, status and acceptance evidence;
- completion and refresh signals use exact canonical phrases;
- Supervisor Coordination Feed exists;
- agents cannot integrate stale dependency work;
- shared-file ownership is enforced;
- integration staging is used when main promotion is blocked;
- exact-head CI and review evidence is required before integration;
- instruction drift is checked and canonical instructions are updated in the same PR when needed;
- no merge/promotion is treated as regulatory or production authorization.
