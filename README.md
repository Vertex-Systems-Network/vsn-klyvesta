# Klyvesta — AI-Native Investing Platform for PSX

Repository target: `Vertex-Systems-Network/vsn-klyvesta`

Klyvesta is an **AI Investment Operating System** designed around the pyPSX Broker API. It supports three customer modes:

1. **Manual** — the customer chooses and confirms each trade.
2. **AI Assisted** — AI researches, constructs recommendations, explains risk, and the customer confirms.
3. **Guarded Auto** — AI manages a portfolio only inside a customer-approved mandate and only after the required legal/regulatory structure is approved.

The product direction includes beginner-first investing, tiered AI-agent packages, a complete investment-event timeline, Email/WhatsApp/SMS notifications, advanced portfolio intelligence, and a full-featured manual + AI-assisted + Guarded Auto experience.

## Non-negotiable product principle

Klyvesta must never claim or imply that losses are impossible, that investing is risk-free, or that profit is guaranteed. Markets can decline and investments can lose value. The platform objective is to reduce avoidable risk, control drawdown, diversify appropriately, adapt exposure, and optimize risk-adjusted outcomes — not guarantee profit or capital preservation.

## Current project status

Planning/foundation only. No production trading implementation is authorized yet.

The first acceptance gate is regulatory + broker fit:
- pyPSX Broker API production capabilities confirmed.
- Underlying regulated broker/custody structure confirmed.
- Regulatory treatment of AI recommendations confirmed.
- Regulatory treatment of discretionary/automatic portfolio management confirmed.
- Required adviser/securities-manager licence or licensed partner arrangement confirmed.

The implementation foundation has been specified and generic foundation work may be validated independently, but no real-money capability is unlocked until the canonical acceptance gates are satisfied.

## Repository model

This repository uses an approved **split distribution model**:

- `vsn-klyvesta` remains public and GPL-3.0 for intentionally open/generic foundation code, public architecture/contracts/examples and public due-diligence material.
- Proprietary investment strategy, confidential broker material, customer/business logic intended to remain closed, production secrets and customer data must not be committed here.
- Proprietary production components require a separately approved private repository/security boundary and separate licence/dependency review.
- Existing GPL-3.0 history is not silently relicensed.

See `docs/REPOSITORY_LICENSING_MODEL.md` and `docs/REPOSITORY_GOVERNANCE.md`.

## Engineering governance

All AI/human engineering sessions must begin with `AGENTS.md` and `.ai/MASTER_ENGINEERING_PROMPT.md`, then read `.ai/agent-orchestration.yaml`, `.ai/parallel-branch-registry.yaml`, the machine-readable project state, guardrails, acceptance gates, active work item, Supervisor Coordination Feed, relevant module contracts/documentation, and latest checkpoint before implementation.

Repository evidence, tests, documentation, Git history, branch registry and Supervisor coordination events are the source of truth; chat memory is not.

The normal workflow is protected-main + pull request + required CI/security checks + review. A narrowly scoped temporary risk acceptance for the current foundation integration is documented as `F0-RISK-001`; it does not authorize bypassing technical acceptance or production/regulatory gates.

### Parallel-agent development

Klyvesta uses a module-ownership and dependency-DAG model so independent engineering agents can work concurrently without routinely modifying the same files.

Current recommended concurrency is **8-10 specialized agents** when enough independent READY work exists. The target may scale toward **12-16 agents** only after path-ownership CI, automatic verifier discovery, dependency/work-item validation, shared-file enforcement, integration/merge-train automation, migration integration, and conflict checks are operational.

Key rules:

- one active implementation owner per module/path;
- before a new parallel work wave, the Main-repo Supervisor first creates/reserves all module branches before assignment or Supervisor implementation work;
- branch/module/agent-slot/readiness mapping is canonical in `.ai/parallel-branch-registry.yaml`;
- independent modules should branch from a common stable integration baseline rather than an unnecessary sequential feature stack;
- dependent modules may stack only when a real dependency requires it;
- module agents work inside explicit allowed paths and communicate across modules through stable contracts/interfaces;
- shared files such as central CI, build/package configuration, shared state, contracts, composition wiring, and final migration/model-snapshot integration are Supervisor/Platform/Integration-owned by default;
- every work item records its exact base SHA and dependency evidence;
- every agent performs a pre-work collision check against active PRs/issues and module ownership;
- every meaningful PR/checkpoint records its instruction-drift check.

The canonical parallel development plan is `docs/PARALLEL_AGENT_DEVELOPMENT.md`; the audited Supervisor workflow is `docs/MULTI_AGENT_REPOSITORY_WORKFLOW.md`; the deep audit is `docs/MULTI_AGENT_WORKFLOW_AUDIT.md`; machine-readable ownership/dependencies live in `.ai/agent-orchestration.yaml` and branch assignments in `.ai/parallel-branch-registry.yaml`.

### Supervisor submission and refresh protocol

A module agent announces completion by posting the exact top-level PR comment:

**Work Done and Submitted**

That signal causes the Supervisor to checkpoint/pause its own platform work, review the submission, integrate it only if ownership/dependency/CI/security/review gates pass, broadcast the accepted-baseline refresh, and then resume its own saved work.

After every accepted integration the Supervisor sends exactly:

**New changes have been merged — please merge these changes into your branch first, then resume your own work.**

The durable coordination feed is GitHub Issue #82. Each alert records the accepted baseline SHA and whether it is protected `main` or `parallel/integration-staging`.

Every active agent must refresh its assigned branch to that accepted baseline and rerun affected validation before resuming. Agents must not continue against a stale accepted baseline after receiving/observing a refresh alert.

Current hosted-main governance remains fail-closed: when `main` promotion is not permitted, the Supervisor may technically integrate accepted work on `parallel/integration-staging`, but must not silently promote it to `main`.

### Mandatory instruction synchronization

At the start of **every task/session** and after every accepted-baseline refresh, the agent must re-check the working instructions relevant to that task. If architecture, tooling, workflow, branch assignment, module ownership, dependencies, testing commands, safety boundaries, Supervisor behavior, or integration rules changed, the new instructions must be persisted in the repository in the same PR.

At minimum:

- agent process changes → update `AGENTS.md`;
- repository onboarding/workflow changes → update `README.md`;
- global engineering protocol changes → update `.ai/MASTER_ENGINEERING_PROMPT.md`;
- module-specific working changes → update the relevant module documentation/README;
- ownership/dependency/shared-file/Supervisor changes → update `.ai/agent-orchestration.yaml`;
- branch assignment/readiness/baseline changes → update `.ai/parallel-branch-registry.yaml`.

New working rules must not exist only in chat, memory, or issue comments.

## Core planning documents

- `AI-PLAN.md`
- `docs/PARALLEL_AGENT_DEVELOPMENT.md`
- `docs/MULTI_AGENT_REPOSITORY_WORKFLOW.md`
- `docs/MULTI_AGENT_WORKFLOW_AUDIT.md`
- `.ai/agent-orchestration.yaml`
- `.ai/parallel-branch-registry.yaml`
- `docs/PRODUCT_VISION_V2.md`
- `docs/PRODUCT_REQUIREMENTS.md`
- `docs/AI_AGENT_PACKAGES.md`
- `docs/INVESTMENT_EVENT_NOTIFICATIONS.md`
- `docs/COMPETITIVE_POSITIONING.md`
- `docs/ARCHITECTURE.md`
- `docs/DATA_FLOW.md`
- `docs/AI_AGENTS.md`
- `docs/RISK_COMPLIANCE.md`
- `docs/SECURITY.md`
- `docs/SECURITY_ARCHITECTURE_AUDIT_V1.md`
- `docs/THREAT_MODEL.md`
- `docs/PLATFORM_STACK_V2.md`
- `docs/DESIGN_SYSTEM.md`
- `docs/QA_PERFORMANCE.md`
- `docs/ROADMAP.md`
- `docs/REPOSITORY_GOVERNANCE.md`
- `docs/REPOSITORY_LICENSING_MODEL.md`

## Implementation foundation V1

- `docs/DOMAIN_DATABASE_MODEL_V1.md`
- `docs/AUTH_SESSION_ARCHITECTURE_V1.md`
- `docs/AUTHORIZATION_PRIVILEGE_MODEL.md`
- `docs/AUTHORIZATION_MATRIX_V1.md`
- `docs/ORDER_LEDGER_STATE_MACHINES_V1.md`
- `docs/API_CONTRACT_BASELINE_V1.md`
- `contracts/openapi/klyvesta.v1.yaml`
- `contracts/broker/BROKER_ADAPTER_V1.md`
- `docs/FOUNDATION_IMPLEMENTATION_PLAN_V1.md`

Accepted planning ADRs:
- `docs/adr/ADR-001-AI-CANNOT-EXECUTE-DIRECTLY.md`
- `docs/adr/ADR-002-PLATFORM-STACK-AND-NATIVE-CLIENTS.md`
- `docs/adr/ADR-003-AUTHENTICATION-AND-FINANCIAL-API-SECURITY.md`
- `docs/adr/ADR-004-FINANCIAL-DATA-INTEGRITY-AND-STATE-MACHINES.md`

## Canonical AI engineering state

- `AGENTS.md`
- `.ai/MASTER_ENGINEERING_PROMPT.md`
- `.ai/agent-orchestration.yaml`
- `.ai/parallel-branch-registry.yaml`
- `.ai/state.json`
- `.ai/guardrails.md`
- `.ai/acceptance-gates.yaml`
- `docs/PARALLEL_AGENT_DEVELOPMENT.md`
- `docs/MULTI_AGENT_REPOSITORY_WORKFLOW.md`
- latest checkpoint path is recorded in `.ai/state.json`

## New-agent onboarding and operational orchestration

Every **new agent starts on `main`**. The Supervisor immediately checks `.ai/parallel-branch-registry.yaml` for a `READY` + `OPEN` module whose durable work item is also `READY`. If available, Supervisor assigns the pre-created module branch, marks the slot occupied, records agent/start state, and the agent then checks out that assigned branch. Before implementation the assigned branch must contain the latest `parallel/integration-staging` accepted baseline.

If no free slot exists, Supervisor sends exactly **Go Home Come Back Next Time** and the unassigned agent starts no work.

See `docs/NEW_AGENT_ONBOARDING.md`, `.ai/integration-baseline.yaml`, and `scripts/onboard-new-agent.rb`.

Operational orchestration CI now verifies onboarding allocation/overflow behavior, durable work-item readiness, accepted-baseline ancestry, module ownership, migration/model-snapshot ownership, dependency-DAG/branch/occupancy consistency, and configured concurrency capacity. `parallel/integration-staging` advances one reviewed integration at a time while protected `main` remains governance-blocked.
