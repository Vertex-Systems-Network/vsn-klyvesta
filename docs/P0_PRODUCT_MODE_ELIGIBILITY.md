# P0 — Product Mode Eligibility and Regulatory Gates

Status: Planning / enforcement requirements. No mode is production-authorized by this document.

## Objective

Klyvesta must separate product UX packages from regulated trading authority. A customer buying a higher AI package does **not** automatically gain access to a legally restricted mode.

Entitlement answers: **what product features the customer paid for**.

Authorization answers: **what the customer and Klyvesta are legally, contractually, technically and risk-wise allowed to do right now**.

These must be independent checks.

## Mode 1 — Manual

### Product behavior

- customer chooses the security;
- customer chooses/enters order parameters;
- customer explicitly confirms the order;
- Klyvesta validates permissions, account state, risk, market state and broker capability;
- BrokerAdapter routes only the normalized approved order;
- broker/PSX execution evidence is reconciled back into Klyvesta.

### Required production gates

- P0 regulatory/broker operating model accepted;
- P1 paper/shadow safety gate accepted;
- P2 broker sandbox/certification/reconciliation/security gate accepted.

### AI allowed

AI may explain, summarize, screen, compare and surface analytics subject to the AI-assisted classification boundary. It may not silently turn an informational response into an executable order.

## Mode 2 — AI-assisted

### Product behavior

- AI/quant system can generate personalized or contextual analysis/recommendation only after the regulatory model permits it;
- recommendation is represented as a proposal, not an order;
- customer sees rationale, risks, conflicts/disclosures and evidence appropriate to the approved product design;
- customer remains the final decision-maker and explicitly confirms the order;
- recommendation and resulting customer decision are separately auditable.

### Required production gates

All Manual gates plus:

- written classification of personalized AI-assisted activity;
- adviser/licensed-partner permission where required;
- approved suitability/risk-profile process;
- approved disclosures;
- recommendation quality/evaluation gate;
- evidence/audit retention requirements;
- no dark pattern that turns recommendation into automatic consent.

### Prohibited shortcut

A paid `Advisor`, `Pro`, or similarly named package cannot be used to bypass regulatory permission. Package naming has no legal effect.

## Mode 3 — Guarded Auto

### Product behavior

Guarded Auto means Klyvesta's system may participate in portfolio/order decisions within a valid approved discretionary mandate and deterministic policy boundary. This is not merely "faster AI-assisted trading".

### Required production gates

All Manual and AI-assisted prerequisites plus:

- discretionary/portfolio-management legal classification accepted;
- required Securities Manager or other licensed partner path documented;
- applicable customer minimum-investment rule documented and enforced;
- Investment Policy Statement/mandate requirements approved;
- suitability process approved;
- independent custody requirement satisfied where applicable;
- conflict-of-interest policy accepted;
- portfolio limits and risk budget configured;
- user pause/kill capability;
- global kill switch;
- shadow-live validation;
- controlled pilot;
- operational/reconciliation/security acceptance.

### Current public-regulatory implication

SECP's 2024 Securities Managers framework publicly states a Rs 5 million minimum client investment threshold for securities-manager portfolio-management services. Until a different lawful structure is documented, Klyvesta must treat small-ticket Guarded Auto as **ineligible/unavailable**, not as a default retail feature.

## Product package mapping

Illustrative commercial tiers can still exist, but tiers must not equal regulated authority.

| Package concept | Manual | Generic analytics | Personalized AI recommendation | Guarded Auto |
|---|---:|---:|---:|---:|
| Guide | Yes, if account eligible | Yes | No by default | No |
| Advisor | Yes | Yes | Only if P3 permission + customer eligible | No |
| Pro | Yes | Yes | Only if P3 permission + customer eligible | No by entitlement alone |
| Autopilot | Yes | Yes | Only if P3 permission | Only if P4 + customer/regulatory eligibility |

The package table is commercial configuration, not a legal interpretation.

## Server-side eligibility decision

Every protected action must evaluate a deterministic policy equivalent to:

```text
Authenticated
AND role/resource authorization passes
AND account active
AND KYC/compliance state valid
AND broker capability available
AND product entitlement active
AND jurisdiction/product mode legally enabled
AND customer meets regulatory eligibility
AND mandate/suitability valid where required
AND security state valid
AND risk limits valid
AND dependency/market state safe
= ALLOW
```

Any failed/unknown mandatory condition => `DENY` or `HOLD`, never optimistic execution.

## Required mode states

Server should model modes with explicit availability states rather than booleans:

- `UNAVAILABLE_REGULATORY`
- `UNAVAILABLE_PARTNER`
- `UNAVAILABLE_CUSTOMER_ELIGIBILITY`
- `UNAVAILABLE_SECURITY`
- `UNAVAILABLE_RISK`
- `AVAILABLE`
- `PAUSED_BY_USER`
- `PAUSED_BY_SYSTEM`
- `SUSPENDED_COMPLIANCE`

The UI must display the real reason without exposing sensitive internal control details.

## Auto-mode mandate lifecycle

If Guarded Auto is eventually authorized, mandate state should be explicit:

`DRAFT -> REVIEW_REQUIRED -> CUSTOMER_ACCEPTED -> COMPLIANCE_REVIEW -> ACTIVE`

Possible non-active states:

- `PAUSED_BY_CUSTOMER`
- `PAUSED_BY_SYSTEM`
- `SUSPENDED`
- `EXPIRED`
- `REVOKED`

No AI order may execute unless the mandate is `ACTIVE` and all current policy checks pass at execution time.

## Customer-safety UX

The platform must never tell a customer:

- profit is guaranteed;
- loss is impossible;
- the AI is risk-free;
- paying for a higher package guarantees better returns;
- Auto mode eliminates investment risk.

Instead, the UI should communicate that AI can automate analysis/risk controls/portfolio processes but market loss remains possible.

## Engineering consequence

Implementation must keep these distinct concepts separate in schema and policy code:

- subscription/package;
- feature entitlement;
- security role;
- customer ownership;
- regulatory eligibility;
- broker/account eligibility;
- risk eligibility;
- mandate authority;
- current system safety state.

A single field such as `user.plan = autopilot` must never be sufficient to authorize automated trading.
