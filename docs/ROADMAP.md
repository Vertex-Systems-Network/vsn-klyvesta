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
