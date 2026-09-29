# Roadmap

## Phase 0 — Regulatory & Partner Discovery
Acceptance:
- pyPSX Broker API technical/commercial docs obtained
- underlying regulated roles identified
- funding/custody/settlement model documented
- market-data rights documented
- legal classification of Manual, AI Assisted, and Guarded Auto documented
- licence/partner path for advice/discretionary management decided

No production autonomous investing before Phase 0 acceptance.

## Phase 1 — Foundation + Paper/Shadow
Build:
- identity
- risk profile
- market data
- portfolio simulator
- ledger
- BrokerAdapter interface
- AI research
- AI recommendation engine
- risk engine
- compliance policy framework
- complete audit trail
- paper trading/backtesting
- admin/risk console

Acceptance:
- no real money
- strategy/model QA passed
- security baseline passed

## Phase 2 — Real Manual Trading
Build:
- pyPSX production adapter
- KYC/account opening
- funding state
- real orders
- fills
- positions
- reconciliation
- statements
- withdrawals/status

Acceptance:
- end-to-end broker certification
- ledger reconciliation evidence
- incident/runbook
- production security review

## Phase 3 — AI Assisted
Build:
- personalized recommendation workflow
- recommendation approval
- scenario/risk explanations
- recommendation recordkeeping
- portfolio optimization

Acceptance:
- legal/adviser permissions accepted
- recommendation QA/evals passed
- customer disclosures accepted

## Phase 4 — Guarded Auto
Build:
- signed/versioned mandate
- automated allocation
- recurring deposits
- auto rebalancing
- risk kill switches
- global Auto pause
- model governance
- enhanced reporting

Acceptance:
- discretionary management legal/licence/partner structure explicitly approved
- risk committee acceptance
- live shadow-mode comparison
- limited pilot
- staged rollout

## Phase 5 — Scale
- multi-broker adapter
- advanced portfolios
- tax/reporting improvements
- corporate actions automation
- advanced risk
- additional regulated products only by separate approval

## Deployment strategy for Auto
1. offline backtest
2. paper
3. historical replay
4. shadow live (no orders)
5. internal/controlled pilot
6. limited customer cohort
7. progressive rollout
8. continuous drift/risk monitoring


## Phase 1A — Lovable Investor UI

The UI is delivered as evidence-gated batches and remains demo/paper-only until the existing regulatory, provider, security and production gates are accepted.

- UI-0 — evidence audit and information architecture
- UI-1 — design system and visual identity
- UI-2 — onboarding, investor profile, risk and goals
- UI-3 — investor home, portfolio, performance, risk and notifications
- UI-4 — market discovery, instrument detail and paper trading
- UI-5 — AI assistant and explainable recommendation experience
- UI-6 — Guarded Auto controls and operations/risk surfaces
- UI-7 — cross-device QA, accessibility and release certification

Acceptance:
- all required routes/screens have deterministic entry and exit states;
- implementation, empty/loading/error states and responsive behavior are committed;
- accessibility, reduced-motion and visual regression checks pass;
- screenshots/browser evidence is attached;
- no UI work unlocks live trading, KYC, custody, funding, withdrawals or production AI authority.

Detailed plan: `docs/UI_UX_DELIVERY_BATCH_PLAN_V1.md`.
