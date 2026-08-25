# Checkpoint — Product Assumptions V2

Date: 2026-08-25

## Repository state at checkpoint

- Branch: `main`
- Product status: `PLANNING`
- Active product task: `P0-T1 — Confirm regulatory and pyPSX Broker API operating model`
- Product roadmap changed: **No phase order changed**
- Product scope clarified/expanded: **Yes**
- Regulatory acceptance changed: **No**

## New product assumptions recorded

- Klyvesta is positioned as an **AI Investment Operating System**, not merely a trading terminal.
- Guarded Auto should manage eligible customer investment workflows after required KYC, suitability, funding and mandate steps.
- Every material investment event should be available in a user timeline and may trigger Email, WhatsApp and/or SMS according to policy/preferences.
- Klyvesta should support 3–4 AI capability packages; a four-tier draft (`Guide`, `Advisor`, `Pro`, `Autopilot`) is documented.
- Platform ambition is full-featured, secure and operationally complete, benchmarked against the usability/automation expectations created by large global platforms.
- Beginner users should be able to invest without needing prior trading knowledge.
- AI should optimize risk-adjusted outcomes using diversification, quantitative risk, rebalancing, cash allocation, monitoring and abstention when appropriate.

## Critical correction preserved

The owner expressed a desired outcome of no loss / risk-free management / profit in all conditions.

This outcome is **not technically achievable or a valid product guarantee**. The repository therefore preserves the stricter existing rule:
- no guaranteed-profit claims,
- no zero-loss claims,
- no risk-free claims,
- no martingale/recovery logic,
- no AI bypass of deterministic risk/compliance/execution controls.

The product target is capital-protection-oriented, risk-bounded, explainable AI investing — not guaranteed profit.

## Research performed

Current public/official product documentation was reviewed for competitive context:
- Binance documentation for Trading Bots and Binance Earn (2026 material) — automated strategies, DCA/rebalancing/Auto-Invest capabilities and explicit acknowledgement that automation does not eliminate risk or guarantee positive outcomes.
- KuCoin Help Center for Trading Bots and KuCoin Earn — broad bot/asset-management features, risk categories and execution-only/no-advice language for relevant Earn services.

No competitor claim was adopted as a Klyvesta requirement merely because the competitor offers it.

## New documentation

- `docs/PRODUCT_VISION_V2.md`
- `docs/AI_AGENT_PACKAGES.md`
- `docs/INVESTMENT_EVENT_NOTIFICATIONS.md`
- `docs/COMPETITIVE_POSITIONING.md`

## Validation performed

- Verified new assumptions do not override `.ai/guardrails.md`.
- Verified Guarded Auto remains regulatory-gated.
- Verified personalized recommendations remain regulatory-gated.
- Verified the current active task remains `P0-T1`.
- Documentation/planning only; no runtime build, unit, integration or E2E tests applicable.

## Security / risk review

- AI remains unable to directly execute broker orders.
- Paid package entitlements cannot override suitability, jurisdiction, compliance or risk restrictions.
- Notification failures must not change authoritative financial state or execution outcome.
- User-facing explanations must derive from structured evidence and must not fabricate investment events.
- Critical provider channels require idempotency, delivery receipts, retries and auditability.

## Known risks / blockers

- pyPSX production/commercial Broker API details are still outstanding.
- Advice/discretionary portfolio licensing/partner structure remains unresolved.
- Main branch remains unprotected (Issue #1).
- Repository visibility/licensing remains a deliberate owner decision (Issue #2).
- WhatsApp/SMS/email providers are not selected yet; selection should occur after architecture/cost/compliance evaluation.
- AI package pricing cannot be finalized until broker, market-data, AI, notification and compliance costs are known.

## Acceptance result

**DONE — product assumptions/documentation update only.**

No real-money trading, personalized AI advice or Guarded Auto production capability is accepted by this checkpoint.

## Exact next action

Continue `P0-T1`: obtain pyPSX Broker API technical/commercial onboarding information and document the regulated operating model. Once enough API detail is available, produce the implementation-level foundation plan for identity, portfolio/ledger, BrokerAdapter, market data, AI orchestration, risk/compliance engines, notifications and paper/shadow trading.
