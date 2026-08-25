# AI Agent Packages

Status: Commercial/product planning. Pricing and production availability remain subject to broker, regulatory, legal, risk and technical acceptance.

## Packaging principle

Klyvesta may sell different levels of AI capability, automation and analytics. It must **not** sell different levels of promised profit or implied certainty.

The package ladder should increase capability, personalization, monitoring and automation while preserving the same core safety boundaries for every customer.

## Proposed 4-tier model

### Tier 1 — Klyvesta Guide
For beginners who want education and simple portfolio visibility.

Includes:
- AI investing assistant
- plain-language portfolio explanations
- basic market/instrument summaries
- watchlist assistance
- basic risk score
- daily portfolio summary
- manual investing tools
- Email notifications

Does not include autonomous order execution.

### Tier 2 — Klyvesta Advisor
For users who want personalized AI-assisted investing.

Includes Tier 1 plus:
- personalized portfolio recommendations where legally permitted
- goal/horizon-aware allocation proposals
- recommendation rationale and downside scenarios
- portfolio health checks
- rebalance recommendations
- deeper risk analytics
- priority market/risk alerts
- Email + WhatsApp notifications where configured

Customer explicitly confirms trade-impacting recommendations unless a legally valid Auto mandate is active.

### Tier 3 — Klyvesta Pro
For investors who want advanced analytics and higher-frequency monitoring.

Includes Tier 2 plus:
- advanced portfolio analytics
- scenario/stress analysis
- factor/exposure analysis
- deeper company/fundamental research where data rights allow
- AI research workspace
- advanced manual trading workspace
- custom alerts
- scheduled reports
- higher monitoring frequency
- Email + WhatsApp + SMS critical alerts

### Tier 4 — Klyvesta Autopilot
For customers who want legally permitted Guarded Auto portfolio management.

Includes Tier 3 plus:
- versioned Auto investment mandate
- automatic allocation of reconciled investable cash
- recurring investment handling
- monitored rebalancing
- risk-triggered exposure reduction/pause behavior
- continuous mandate/risk/compliance validation
- explainable Auto decision timeline
- user pause/resume controls
- emergency system kill-switch support
- enhanced portfolio and audit reporting

## Regulatory gate

`Klyvesta Autopilot` must remain disabled until the repository acceptance gates record the required regulatory/licensing/partner structure for discretionary portfolio management.

Personalized AI recommendations in `Klyvesta Advisor` and above also remain subject to the applicable securities-adviser/partner structure.

## Entitlement architecture

Commercial packages should map to typed product entitlements, not hard-coded UI checks.

Example entitlement concepts:
- `ai_chat`
- `portfolio_explanations`
- `personalized_recommendations`
- `advanced_risk_analytics`
- `scenario_analysis`
- `custom_alerts`
- `whatsapp_alerts`
- `sms_critical_alerts`
- `scheduled_reports`
- `auto_mandate`
- `auto_rebalance`
- `priority_support`

Every entitlement must also pass regulatory, account, jurisdiction, suitability and feature-flag checks. A paid plan never overrides a legal or risk restriction.

## Fairness and conflicts

- Higher tiers may receive more analysis/automation, not weaker risk controls.
- AI must not increase turnover to generate fees.
- Package economics must not change portfolio recommendations in a way that conflicts with customer interest.
- A lower-priced customer must not receive unsafe or misleading investment logic.
- Execution fairness rules must be documented before production.

## Pricing strategy

Do not set final prices until we know:
- pyPSX/broker commercial costs,
- market-data costs,
- notification costs,
- AI inference costs,
- compliance/operations costs,
- expected average assets per user,
- customer acquisition costs,
- regulatory constraints on charging structure.

Potential pricing mechanics to evaluate later:
- monthly subscription,
- annual subscription,
- feature bundle,
- assets-under-management fee where legally permitted,
- broker revenue share where legally permitted,
- enterprise/family plans.

No pricing mechanic may incentivize excessive trading or undermine fiduciary/customer-interest obligations where applicable.
