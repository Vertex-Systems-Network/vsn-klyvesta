# AI Agent System

## Core rule

The agent system may decide what it *wants* to do. Deterministic services decide what it *is allowed* to do.

## Agents

### 1. Investor Understanding Agent
Inputs:
- goals
- horizon
- income/liquidity needs
- experience
- risk questionnaire
- preferences

Outputs:
- structured investor profile proposal
- questions requiring clarification
- explanation

Cannot:
- change legally significant suitability values without user confirmation
- execute trades

### 2. Market Intelligence Agent
Inputs:
- approved market data
- fundamentals
- announcements/news feeds
- portfolio context

Outputs:
- evidence-linked market brief
- risk events
- candidate universe notes

Cannot:
- use unapproved web claims as executable price data
- invent missing data

### 3. Signal / Regime Agent
Uses quantitative features to classify conditions and produce structured signals. LLM narrative is optional and cannot replace numeric calculations.

### 4. Portfolio Construction Agent
Produces target weights subject to constraints.

Required output:
- target weights
- expected risk
- concentration
- liquidity
- turnover
- cash
- rationale
- uncertainty
- model/data versions

### 5. Rebalancing Agent
Compares actual vs target and proposes a minimal-cost rebalance.

### 6. Explainability Agent
Converts structured decision/evidence into plain-language explanation.

Must state uncertainty and meaningful downside.

### 7. Investor Coach
Explains investing concepts and helps users understand their own portfolio. It must distinguish education from personalized advice according to product/legal mode.

### 8. Operations Agent
Handles non-financial workflows: support triage, failed KYC guidance, document/status explanations. No trading permissions.

## Non-agent authorities

### Suitability Policy Engine
Hard rules derived from approved policy.

### Risk Governor
Hard limits; cannot be overridden by AI.

### Compliance Gate
Legal/product eligibility and restricted-action checks.

### Execution Validator
Validates order quantities, side, symbol, session, account, cash/position, duplicate/idempotency, and approved intent.

## Agent memory

Separate:
- user preference memory,
- regulatory/consent state,
- portfolio state,
- conversation memory.

Portfolio, mandate, KYC and money state must be read from systems of record, never remembered from chat.

## Tool permissions

Default deny.

Example:
- Coach: read portfolio summary; no write
- Research: read approved market datasets; no broker tools
- Portfolio Agent: read state; write proposal only
- Rebalance Agent: write order-intent proposal only
- Execution system: deterministic service, not LLM agent

## Prompt injection defense

Untrusted text such as news, announcements, uploaded research or websites is data, not instruction.

The agent runtime must:
- tag provenance
- isolate untrusted content
- prevent external text from changing system/tool permissions
- schema-validate all outputs
- require policy authorization for side effects

## Model governance

Every decision stores:
- model provider/model ID
- model version
- prompt/template version
- tool versions
- temperature/settings where relevant
- input evidence IDs
- output hash
- policy result
- human/user approval if required

Model upgrades require regression evaluation before production.
