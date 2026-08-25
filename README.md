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

Planning only. No production trading implementation is authorized yet.

The first acceptance gate is regulatory + broker fit:
- pyPSX Broker API production capabilities confirmed.
- Underlying regulated broker/custody structure confirmed.
- Regulatory treatment of AI recommendations confirmed.
- Regulatory treatment of discretionary/automatic portfolio management confirmed.
- Required adviser/securities-manager licence or licensed partner arrangement confirmed.

The implementation foundation has now been specified, but it is **not an active implementation task** until canonical state/owner direction explicitly activates it within the acceptance gates.

## Engineering governance

All AI/human engineering sessions must begin with `AGENTS.md` and `.ai/MASTER_ENGINEERING_PROMPT.md`, then read the machine-readable project state, guardrails, acceptance gates, and latest checkpoint before implementation.

Repository evidence, tests, documentation, and Git history are the source of truth; chat memory is not.

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
