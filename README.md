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

**Last status update:** `2026-09-22 — P1-23 Customer Alert Rules canonically closed as INTEGRATED after PR #131; README progress-sync policy accepted via PR #132`

Accepted integration branch: `parallel/integration-staging`

Verified parent baseline: `30de8c4c8daeff302cf1c669ef1d3b1a78f02c52` — this is the immutable parent for the current control-plane generation; the current accepted branch head is resolved at runtime and broadcast through Issue #82.

**Repository-owned non-live product/verification lanes:** `████████░░ 79%` — **19 of 24 canonical lanes are accepted/integrated on `parallel/integration-staging`.** P1-23 Customer Alert Rules is integrated; Customer Scenarios, Customer Security Center, Customer Risk Center and Database Integration remain READY, while Security Acceptance remains blocked by external production evidence.

**Overall delivery status:** **NON-LIVE STAGING ACTIVE / LIVE PRODUCTION BLOCKED**

The accepted staging baseline contains deterministic, API-independent/non-live boundaries for paper brokerage, OMS, portfolio/reconciliation, risk, compliance, AI shadow planning, customer data, customer insights, reporting, preferences, planning, activity, dashboard composition, identity/authorization, ledger, notifications, observability, and performance/resilience.

Live/real-money pyPSX operation is not authorized. Production brokerage remains fail-closed until direct pyPSX partner API/contract evidence, credentials and exact provider semantics are available; required legal/regulatory/provider approvals are complete; and repository governance permits production promotion.

The first production acceptance gate remains regulatory + broker fit:
- pyPSX Broker API production capabilities confirmed.
- Underlying regulated broker/custody structure confirmed.
- Regulatory treatment of AI recommendations confirmed.
- Regulatory treatment of discretionary/automatic portfolio management confirmed.
- Required adviser/securities-manager licence or licensed partner arrangement confirmed.

The implementation foundation and non-live safety boundaries may be validated independently, but no real-money capability is unlocked until the canonical acceptance gates are satisfied.

## Module delivery table

> This table tracks the **canonical repository-owned non-live engineering state on `parallel/integration-staging`**. `100%` means the scoped non-live module is accepted/integrated; it does not mean live pyPSX, regulatory, provider, KYC/PII, or real-money approval. README lifecycle rows are checked by Platform CI so accepted module transitions must update this table.

| Module | Progress | Work Item | Status |
| --- | --- | --- | --- |
| Brokerage Trust / Paper Broker | `██████████ 100%` | P1 brokerage foundation | Integrated — API-independent paper broker + broker trust boundary; live pyPSX remains external |
| Orders / OMS | `██████████ 100%` | P1 OMS | Integrated — deterministic paper/non-live order state machine |
| Portfolio & Reconciliation | `██████████ 100%` | P1 portfolio | Integrated — deterministic paper projection/reconciliation boundary |
| Risk | `██████████ 100%` | P1 risk | Integrated — deterministic paper risk governor |
| Compliance | `██████████ 100%` | P1 compliance | Integrated — deterministic paper compliance gate |
| AI Shadow | `██████████ 100%` | P1 AI shadow | Integrated — proposal/paper-shadow boundary; no direct execution authority |
| Customer Data | `██████████ 100%` | P1-16 | Integrated — customer-owned profile, risk-profile, goals and watchlist foundation |
| Customer Insights | `██████████ 100%` | P1-17 | Integrated — deterministic informational customer insights |
| Customer Reporting | `██████████ 100%` | P1-18 | Integrated — customer-scoped paper portfolio reporting |
| Customer Preferences | `██████████ 100%` | P1-19 | Integrated — privacy-safe notification preference state |
| Customer Planning | `██████████ 100%` | P1-20 | Integrated — deterministic goal/contribution planning without advice authority |
| Customer Activity | `██████████ 100%` | P1-21 | Integrated — sanitized customer-scoped order/ledger activity timeline |
| Customer Dashboard | `██████████ 100%` | P1-22 | Integrated — deterministic read-only paper/informational composition |
| Customer Alert Rules | `██████████ 100%` | P1-23 | Integrated — customer-scoped deterministic rule configuration/evaluation; no dispatch, live-market, trading or money-movement authority |
| Customer Scenarios | `░░░░░░░░░░ 0%` | P1-24 | Ready — deterministic paper scenario/risk views without advice or execution authority |
| Customer Security Center | `░░░░░░░░░░ 0%` | P1-25 | Ready — read-only customer-scoped session/device/security-state projection |
| Customer Risk Center | `░░░░░░░░░░ 0%` | P1-26 | Ready — P1-26 deterministic customer-scoped risk visibility without advice/execution authority |
| Identity & Authorization | `██████████ 100%` | P1-02/03 | Integrated — server-authoritative identity, withdrawal/session/device and break-glass boundaries |
| Ledger | `██████████ 100%` | P1-04 | Integrated — immutable non-live double-entry boundary |
| Notifications | `██████████ 100%` | P1-12 | Integrated — provider-neutral delivery boundary; production provider remains separate |
| Observability | `██████████ 100%` | P1-13 | Integrated — structured secret-safe telemetry boundary |
| Performance & Resilience | `██████████ 100%` | P1-15 | Integrated — deterministic performance/failure contract; production measurements remain pending |
| Database Integration | `░░░░░░░░░░ 0%` | M-AGENT-06 | Ready — migrations/model snapshots remain database-integration owned |
| Security Acceptance | `░░░░░░░░░░ 0%` | P1-14 | Blocked — production governance/provider/legal/runtime evidence required |
| Platform / CI | `█████████░ 90%` | M-AGENT-09 | Active — ownership, lifecycle, verifier discovery, capacity and README status enforcement |
| pyPSX Live Integration | `░░░░░░░░░░ 0%` | External | Blocked — direct partner API/contract/credential semantics not available |

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
- shared files such as `README.md`, central CI, build/package configuration, shared state, contracts, composition wiring, and final migration/model-snapshot integration are Supervisor/Platform/Integration-owned by default;
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

### Mandatory README progress synchronization

README progress/status is part of the durable delivery control plane, not optional presentation. After **every accepted integration** the Supervisor must update the README `Last status update` in the same bounded closeout path. When that integration or terminal closeout changes canonical lifecycle/progress/timeline/public-delivery truth, the same reviewed change must also update the repository-owned lane count/progress bar and every affected row in the Module delivery table.

A milestone that changes canonical delivery truth is not fully closed while README progress is stale. If the accepted code has merged but lifecycle closeout is still pending, README must say that explicitly rather than prematurely advancing the percentage. Governance-only work that does not change accepted delivery truth must not fabricate progress.

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

Operational orchestration CI now verifies onboarding allocation/overflow behavior, durable work-item readiness, accepted-baseline ancestry, module ownership, migration/model-snapshot ownership, dependency-DAG/branch/occupancy consistency, configured concurrency capacity, and README lifecycle freshness. `parallel/integration-staging` advances one reviewed integration at a time while protected `main` remains governance-blocked.


## AI Supervisor durable resume protocol

Supervisor `continue`/resume work is repository-driven. Before new engineering it reads `.ai/compact-state/CURRENT-STATE.yaml` and `.ai/compact-state/LAST-CHECKPOINT.md`, resolves exact `main`, reconciles open Issues before open PRs, then reconciles acceptance claims, the parallel coordination queue, accepted integration baseline, and `.ai/runner-benchmark.yaml`.

One user `continue` turn normally advances one bounded logical milestone. CI/status is refreshed once per milestone by default rather than tight-polled. A prior message-delivery timeout never proves the underlying repository operation failed; repository evidence is re-read before any retry.

Every Supervisor engineering response includes repository name, current active-module progress, overall repository-owned non-live progress, milestone/evidence, CI state, blockers, and the exact next safe action. Progress is reported as unknown instead of guessed when canonical sources disagree.

This control plane does not grant live pyPSX, production PII, broker/provider, deployment, destructive migration, real-money, or release authority.


### Supervisor review-branch CI coverage

The canonical Supervisor branch remains `parallel/supervisor-platform`. Bounded `supervisor/**` review/recovery branches are also covered by orchestration and .NET regression CI and are ownership-restricted to shared governance/control-plane paths. They cannot modify module `src/**` implementation and do not bypass accepted-baseline, review, security, or promotion gates.
