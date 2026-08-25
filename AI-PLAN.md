# AI-PLAN — Klyvesta

## 1. Product thesis

Make regulated investing understandable to a person with little or no market knowledge while keeping the system auditable, bounded, and reversible.

The product is not an autonomous gambling engine. It is a regulated-investing operating system where AI:
- understands the investor,
- monitors allowed market data,
- proposes portfolios,
- explains trade-offs,
- continuously measures risk,
- proposes or performs rebalancing only when permitted,
- records the evidence behind every material decision.

## 2. AI-native definition

AI-native means AI is a first-class system actor, not a chatbot added to a traditional trading app.

Every agent action must have:
- a typed input,
- an explicit tool permission,
- an output schema,
- a policy decision,
- a model/prompt version,
- source/evidence references,
- an immutable audit record,
- deterministic validation before any financial side effect.

No LLM may call the broker execution endpoint directly.

## 3. Customer modes

### Manual
Customer creates the order. Klyvesta validates it and routes it.

### AI Assisted
AI creates a recommendation with:
- rationale,
- expected risk,
- uncertainty,
- portfolio impact,
- downside scenarios,
- costs/turnover,
- evidence freshness.

Customer must explicitly confirm.

### Guarded Auto
Customer signs/accepts a mandate defining:
- allowed instruments,
- investment horizon,
- risk level,
- maximum single-name exposure,
- maximum sector exposure,
- minimum cash buffer,
- maximum turnover,
- maximum daily order count/value,
- drawdown response,
- rebalance rules,
- prohibited instruments,
- pause/kill-switch conditions.

The AI may act only inside this envelope. The deterministic Risk Governor and Compliance Gate can veto the AI at any time.

**Guarded Auto remains feature-flagged OFF until regulatory acceptance is recorded.**

## 4. AI decision pipeline

Market Data -> Feature Pipeline -> Research/Signal Layer -> Portfolio Constructor -> Risk Governor -> Compliance Gate -> Order Intent -> Execution Planner -> pyPSX Adapter -> Broker -> Fill/Reconciliation -> Portfolio State -> Explainability/Audit.

## 5. Agent hierarchy

AI agents may recommend. Policy services authorize.

- Investor Understanding Agent
- Market Intelligence Agent
- Signal/Regime Agent
- Portfolio Construction Agent
- Rebalancing Agent
- Explainability Agent
- Investor Coach Agent
- Operations Agent

Deterministic authorities:
- Suitability Policy Engine
- Risk Governor
- Compliance Gate
- Execution Validator
- Ledger/Reconciliation Engine

## 6. Initial investment universe

MVP should be intentionally conservative:
- PSX listed equities that pass liquidity/eligibility rules
- ETFs where supported and appropriate
- no leverage
- no margin
- no short selling
- no derivatives/futures in automated mode
- no penny/illiquid names in auto portfolios
- no instruments without reliable price/reference data

Expansion requires a new risk and compliance acceptance gate.

## 7. Risk objective

Do not optimize for raw return.

Primary optimization objective:
maximize expected risk-adjusted return subject to hard constraints.

Example constraints:
- concentration
- liquidity
- volatility
- drawdown
- turnover
- transaction costs
- user horizon
- user suitability
- sector limits
- cash reserve
- mandate limits

## 8. Model strategy

Use different technology for different jobs.

LLMs:
- natural-language onboarding
- research summarization
- explanation
- evidence synthesis
- user support
- workflow orchestration

Quant/statistical models:
- return/risk estimation
- volatility
- correlation
- regime classification
- factor scoring
- anomaly detection
- portfolio optimization

Deterministic code:
- money math
- ledger
- order quantities
- limits
- permissions
- suitability rules
- risk rules
- compliance rules
- reconciliation
- broker API calls

## 9. AI safety rules

- No fabricated market prices.
- No trading on stale market state.
- No execution from free-form natural language without structured confirmation/mandate.
- No direct LLM access to broker API secrets.
- No model may alter its own risk limits.
- No agent may expand a mandate.
- No hidden leverage.
- No averaging-down rule without explicit risk approval.
- No guaranteed-return language.
- No “recover losses” martingale behavior.
- No trading to increase platform commission/revenue.
- Conflicts of interest must be modeled and disclosed.

## 10. Product success metrics

Business:
- funded active investors
- retained investors
- recurring deposits
- net assets on platform
- conversion from onboarding to funded account

Investor outcome:
- risk-adjusted return
- max drawdown
- downside capture
- turnover/cost
- diversification
- mandate adherence
- behavior gap reduction

Safety:
- unauthorized orders = 0
- orders outside mandate = 0
- unreconciled money/security movements = 0 beyond defined reconciliation window
- hallucinated execution facts = 0
- unversioned model decisions = 0
- missing decision audit trail = 0
