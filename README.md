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

**Last status update:** `2026-09-14 18:48 PKT`

**Repository-owned non-live engineering:** `█████████░ 86%` — **12 of 14 canonical module lanes are accepted/integrated on `parallel/integration-staging`.**

**Overall delivery status:** **NON-LIVE STAGING ACTIVE / LIVE PRODUCTION BLOCKED**

API-independent, **non-live engineering is active and integrated through `parallel/integration-staging`**. The accepted staging baseline includes deterministic paper/simulation boundaries for core investing, brokerage trust, identity/authorization, risk, compliance, ledger, notifications, observability, performance/resilience and platform CI.

**Live/real-money pyPSX operation is not authorized.** Production brokerage remains fail-closed until direct pyPSX partner API/contract evidence, credentials and exact provider semantics are available; required legal/regulatory/provider approvals are complete; and repository governance permits production promotion.

The two remaining repository-owned canonical module lanes are:
- **Database Integration** — ready, not yet started/integrated.
- **Security Acceptance** — blocked pending production governance/provider/legal/runtime evidence.

The first production acceptance gate remains regulatory + broker fit:
- pyPSX Broker API production capabilities confirmed.
- Underlying regulated broker/custody structure confirmed.
- Regulatory treatment of AI recommendations confirmed.
- Regulatory treatment of discretionary/automatic portfolio management confirmed.
- Required adviser/securities-manager licence or licensed partner arrangement confirmed.

The implementation foundation and non-live safety boundaries may be validated independently, but no real-money capability is unlocked until the canonical acceptance gates are satisfied.

## Module delivery table

> Progress is for the **current repository-owned non-live engineering scope**. `100%` does **not** mean production/live/regulatory approval. Provider, legal and pyPSX-dependent work remains separately gated.

| Module | Progress | Start Date | End Date | Status |
| --- | --- | --- | --- | --- |
| Orders / OMS | `██████████ 100%` | 2026-09-01 | 2026-09-03 | Integrated — deterministic paper/non-live state machine |
| Portfolio & Reconciliation | `██████████ 100%` | 2026-09-01 | 2026-09-03 | Integrated — paper projection/reconciliation boundary |
| Risk | `██████████ 100%` | 2026-09-01 | 2026-09-03 | Integrated — deterministic paper risk governor |
| Compliance | `██████████ 100%` | 2026-09-01 | 2026-09-03 | Integrated — deterministic paper compliance gate |
| AI Shadow | `██████████ 100%` | 2026-09-01 | 2026-09-03 | Integrated — AI proposal/paper-shadow boundary; no direct execution authority |
| Brokerage Trust / Paper Broker | `██████████ 100%` | 2026-09-01 | 2026-09-14 | Integrated — API-independent broker trust + paper broker complete; live pyPSX adapter is a separate external gate |
| Identity & Authorization | `██████████ 100%` | 2026-09-03 | 2026-09-14 | Integrated — withdrawal, session/device revocation and maker-checker break-glass hardening |
| Ledger | `██████████ 100%` | 2026-09-03 | 2026-09-14 | Integrated — immutable non-live double-entry boundary |
| Notifications | `██████████ 100%` | 2026-09-03 | 2026-09-14 | Integrated — provider-neutral delivery boundary; production provider pending |
| Observability | `██████████ 100%` | 2026-09-03 | 2026-09-14 | Integrated — structured secret-safe boundary; production telemetry provider pending |
| Performance & Resilience | `██████████ 100%` | 2026-09-03 | 2026-09-14 | Integrated — deterministic acceptance contract; production SLO evidence pending |
| Platform / CI | `██████████ 100%` | 2026-09-03 | 2026-09-14 | Integrated — multi-agent ownership, verifier and CI control plane |
| Database Integration | `░░░░░░░░░░ 0%` | — | — | Ready — canonical lane available, work not yet started/integrated |
| Security Acceptance | `░░░░░░░░░░ 0%` | — | — | Blocked — production governance/provider/legal/runtime evidence required |
| pyPSX Live Integration | `░░░░░░░░░░ 0%` | — | — | External blocker — partner API/contract not yet available |

## Today’s close plan — 2026-09-14

| Work item | Start Date | End Date | Status |
| --- | --- | --- | --- |
| AI-agent prompt-injection hardening | 2026-09-14 | 2026-09-14 | Done |
| EF Core dependency alignment | 2026-09-14 | 2026-09-14 | Done |
| Broker trust canonicalization + staging merge | 2026-09-14 | 2026-09-14 | Done |
| Withdrawal/session/break-glass canonicalization | 2026-09-14 | 2026-09-14 | Done |
| README module/status reconciliation | 2026-09-14 | 2026-09-14 | Done — corrected current progress/status values |
| Stale/experimental PR cleanup | 2026-09-14 | 2026-09-14 | Done |
| Database Integration canonical lane | — | — | Ready / next repository-owned module |
| Security Acceptance | — | — | Blocked by production evidence/governance gates |
| pyPSX live adapter | — | — | Blocked until partner API/contract arrives |

## Repository model

This repository uses an approved **split distribution model**:

- `vsn-klyvesta` remains public and GPL-3.0 for intentionally open/generic foundation code, public architecture/contracts/examples and public due-diligence material.
- Proprietary investment strategy, confidential broker material, customer/business logic intended to remain closed, production secrets and customer data must not be committed here.
- Proprietary production components require a separately approved private repository/security boundary and separate licence/dependency review.
- Existing GPL-3.0 history is not silently relicensed.

See `docs/REPOSITORY_LICENSING_MODEL.md` and `docs/REPOSITORY_GOVERNANCE.md`.

## Engineering governance

All AI/human engineering sessions must begin with `AGENTS.md` and `.ai/MASTER_ENGINEERING_PROMPT.md`, then read the machine-readable project state, guardrails, acceptance gates, and latest checkpoint before implementation.

Repository evidence, tests, documentation, and Git history are the source of truth; chat memory is not.

The normal workflow is protected-main + pull request + required CI/security checks + review. `main` must remain fail-closed for substantive production/security-sensitive promotion while hosted branch protection/ruleset enforcement is unresolved, unless a new explicit owner risk decision narrowly authorizes a named change.

## Core planning documents

- `AI-PLAN.md`
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
- `.ai/state.json`
- `.ai/guardrails.md`
- `.ai/acceptance-gates.yaml`
- latest checkpoint path is recorded in `.ai/state.json`
