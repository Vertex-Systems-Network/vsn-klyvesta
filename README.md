# Klyvesta — AI-Native Investing Platform for PSX

Repository target: `Vertex-Systems-Network/vsn-klyvesta`

Klyvesta is an AI-native investing platform designed around the pyPSX Broker API. It supports three customer modes:

1. **Manual** — the customer chooses and confirms each trade.
2. **AI Assisted** — AI researches, constructs recommendations, explains risk, and the customer confirms.
3. **Guarded Auto** — AI manages a portfolio only inside a customer-approved mandate and only after the required legal/regulatory structure is approved.

## Non-negotiable product principle

Klyvesta must never claim or imply that losses are impossible. Markets can decline and investments can lose value. The platform objective is to reduce avoidable risk, control drawdown, diversify appropriately, and optimize risk-adjusted outcomes — not guarantee profit or capital preservation.

## Current project status

Planning only. No production trading implementation is authorized yet.

The first acceptance gate is regulatory + broker fit:
- pyPSX Broker API production capabilities confirmed.
- Underlying regulated broker/custody structure confirmed.
- Regulatory treatment of AI recommendations confirmed.
- Regulatory treatment of discretionary/automatic portfolio management confirmed.
- Required adviser/securities-manager licence or licensed partner arrangement confirmed.

See:
- `AI-PLAN.md`
- `docs/PRODUCT_REQUIREMENTS.md`
- `docs/ARCHITECTURE.md`
- `docs/DATA_FLOW.md`
- `docs/AI_AGENTS.md`
- `docs/RISK_COMPLIANCE.md`
- `docs/SECURITY.md`
- `docs/DESIGN_SYSTEM.md`
- `docs/QA_PERFORMANCE.md`
- `docs/ROADMAP.md`
- `.ai/state.json`
- `.ai/guardrails.md`
- `.ai/acceptance-gates.yaml`
