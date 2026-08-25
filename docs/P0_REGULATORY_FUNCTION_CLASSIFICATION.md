# P0 Regulatory Function Classification

Status: **P0 evidence / legal-classification working model**. This document is an engineering and product-control artifact, not legal advice. Production classification must be confirmed by Pakistani securities counsel, SECP requirements, and the regulated broker/adviser/manager partner.

## Purpose

Klyvesta must classify a feature by what it actually does, not by UI label, AI package name, or marketing wording. The same AI model can create very different regulatory risk depending on whether it educates, publishes research, personalizes advice, or exercises discretion over customer assets.

## Four functional classes

### Class E — Education / neutral assistance

Examples:
- explain what a stock, ETF, dividend, P/E ratio, volatility, drawdown, diversification, order type, or settlement means;
- explain historical portfolio events already recorded by systems of record;
- explain how Klyvesta works;
- show user-owned factual portfolio/account data without recommending an action;
- answer generic financial-literacy questions;
- provide neutral product navigation/help.

Default control:
- may be available before advisory/discretionary licensing if counsel/partner confirms the specific implementation stays outside regulated advice/research;
- must not imply guaranteed return, zero loss, or risk-free outcome;
- must distinguish factual data from forecasts/opinions;
- must cite/retain evidence for material market facts used in user-facing explanations.

### Class R — Research / market opinion

Examples:
- publish a report/opinion on a security or issuer;
- assign ratings, target prices, forecasts, expected-return views, or buy/hold/sell style conclusions;
- generate public/social content that performs a function comparable to regulated research analysis;
- white-label research content for distribution.

Current public regulatory signal:
- SECP amended the Research Analyst Regulations in January 2026 and stated that research analysts are now subject to a registration mechanism;
- the amended framework also addresses social-media activity comparable to research-analysis functions, research-report scope, target-price/date disclosures, white-labelling and conduct requirements.

Engineering consequence:
- do not treat `AI Research` as an automatically unregulated feature;
- before production, classify every research output type against the current Research Analyst Regulations and partner/counsel position;
- retain analyst/model identity, evidence, methodology/version, conflict disclosures, publication timestamp and approval state where required;
- public research cannot silently become personalized advice merely because the user is logged in.

### Class A — Personalized investment advice

Examples:
- recommend a particular security, allocation, trade, rebalance or risk action based on a specific user's money, holdings, goals, age/horizon, risk profile or circumstances;
- tell the user what they personally should buy/sell/hold;
- construct a user-specific portfolio recommendation even if the user must approve every trade.

Current public regulatory signal:
- SECP states that giving investment advice to others is a regulated Securities Adviser activity and requires licensing under the applicable Securities and Futures Advisers framework, subject to exemptions/permissions that must be verified for the actual operating model.

Engineering consequence:
- `AI Assisted` is feature-flagged off for production until the responsible licensed/advisory path is documented and accepted;
- user approval does **not** automatically convert personalized advice into an unregulated feature;
- recommendation evidence, suitability inputs, model/prompt version, disclosures, conflicts, user approval and final execution intent must be auditable;
- package purchase never creates legal authority to advise.

### Class D — Discretionary portfolio management

Examples:
- Klyvesta/AI decides what to buy/sell/rebalance on behalf of the customer without per-trade approval;
- automated recurring-deposit allocation;
- automated risk reduction or re-entry;
- automated portfolio construction/rebalancing under a mandate.

Current public regulatory signal:
- SECP's Securities Managers (Licensing and Operations) Regulations, 2024 establish a securities-manager framework for portfolio management by eligible securities brokers;
- SECP's public release states portfolio-management services require a Securities Manager licence for the eligible broker, customer assets are held with an independent custodian, and the minimum investment threshold is PKR 5 million under that framework;
- this means Klyvesta cannot assume small-ticket fully discretionary `Guarded Auto` is legally available to every retail user.

Engineering consequence:
- `Guarded Auto` remains disabled until the exact licensed operating model, eligible-customer rule, independent-custodian arrangement, mandate/Investment Policy Statement requirements and partner responsibilities are confirmed;
- no code path may infer discretionary authority from an `Autopilot` subscription;
- no AI/LLM may directly execute an order even after discretionary authority exists: deterministic mandate, Risk Governor, Compliance Gate and Execution Validator remain mandatory.

## Feature-classification decision tree

```text
Does the feature merely explain factual/general concepts?
  YES -> Class E candidate
  NO  -> continue

Does it publish an issuer/security opinion, rating, forecast or target?
  YES -> Class R candidate
  NO  -> continue

Is the output tailored to a specific user's holdings/goals/risk/circumstances?
  YES -> Class A candidate
  NO  -> continue legal review

Can the system cause investment action without per-trade customer approval?
  YES -> Class D candidate
```

A feature may fall into more than one class. Apply the **strictest applicable production gate** until authoritative review says otherwise.

## Package mapping

Commercial package names are deliberately separated from regulatory classes.

| Package | Potential functions | Production rule |
|---|---|---|
| Guide | E, possibly limited R | enable only approved functions |
| Advisor | E + R + A | A requires accepted advisory path |
| Pro | E + R + A + analytics | advanced tooling does not bypass licensing |
| Autopilot | E + R + A + D | D requires accepted discretionary-management path |

## Prohibited product shortcuts

Never use wording or implementation tricks to avoid classification, including:
- calling personalized advice `education` when it tells the user what to buy;
- calling a target-price/security rating `AI insight` to avoid research controls;
- requiring one generic checkbox and then treating all future trades as authorized;
- treating a subscription purchase as mandate execution authority;
- using `not financial advice` as a substitute for actual functional/legal classification.

## Required pre-production evidence by class

### E
- counsel/partner confirms scope is non-advisory/non-research where relevant;
- content accuracy/safety controls;
- no misleading performance claims.

### R
- current Research Analyst Regulations mapped;
- responsible registered/licensed entity/person identified where required;
- conflicts/disclosures/white-label/social-publication obligations documented;
- approval/evidence/audit workflow tested.

### A
- responsible Securities Adviser or permitted broker/advisory path identified;
- suitability and disclosure obligations documented;
- user-approval semantics documented;
- recommendation audit/evidence requirements tested.

### D
- responsible Securities Manager/licensed partner identified;
- eligible-customer/minimum-ticket rules confirmed;
- independent custodian confirmed;
- mandate/IPS and fiduciary requirements documented;
- discretionary scope/limits/withdrawal rights documented;
- shadow-live, risk, reconciliation, kill-switch and controlled-pilot gates passed.

## Default production rule

When classification is uncertain:

```text
UNKNOWN REGULATORY CLASSIFICATION
        -> FEATURE OFF
        -> PAPER/SHADOW ONLY
        -> LEGAL/PARTNER REVIEW
```

Do not use an AI model to determine its own legal authority.