# P0 Fallback Regulated Partner Strategy

Status: **Research/partner-discovery strategy only. No entity is approved by this document.**

## Why this exists

pyPSX remains the preferred embedded-broker/Broker API path under evaluation, but Klyvesta must not create a single-vendor regulatory or technical dependency.

The fallback strategy separates four capabilities that may require different regulated parties:

1. brokerage / execution / account opening;
2. securities research;
3. personalized securities advice;
4. discretionary portfolio management.

## Partner lanes

### Lane B1 — Embedded broker / execution partner

Required for Manual real-money trading candidate:
- current SECP securities-broker licence;
- PSX TREC/approved brokerage status;
- IBTS/online execution capability;
- digital onboarding/KYC support;
- account/UIN/CDC/NCCPL flows;
- funding/withdrawal operations;
- order/execution/position/statement APIs or an acceptable integration path;
- security/audit/SLA/certification evidence;
- white-label/embedded-commercial rights.

pyPSX is currently the primary candidate for this integration layer, but its underlying broker/legal role and technical contract remain unverified.

### Lane B2 — Research provider / Research Analyst path

Required if Klyvesta produces or white-labels outputs that are regulated research reports/opinions.

Due diligence:
- current SECP Research Analyst registration/status where required;
- permitted use/white-labelling;
- target-price/rating/disclosure controls;
- conflict-of-interest controls;
- approval/publication workflow;
- AI-generated research responsibility;
- social-media/public-output compliance.

### Lane B3 — Securities Adviser path

Required for production personalized advice where applicable.

Due diligence:
- current SECP adviser licence/permission;
- exact scope and client/product limits;
- digital-platform obligations;
- suitability/KYC responsibilities;
- fee/revenue model;
- disclosures/conflicts;
- recommendation evidence and complaint handling;
- ability to support Klyvesta's AI-assisted workflow contractually.

### Lane B4 — Securities Manager path

Required for discretionary portfolio management under the current public framework where applicable.

Due diligence:
- current Securities Manager licence;
- securities-broker eligibility/status;
- independent custodian;
- eligible-customer/minimum-ticket requirements;
- Investment Policy Statement / mandate process;
- fiduciary and conflict controls;
- research capacity;
- portfolio-management operations/APIs;
- reconciliation/reporting;
- ability to contractually support algorithmic/AI-assisted portfolio-management workflows;
- regulator/partner position on automation and delegation.

## Current public ecosystem evidence

### SECP current lists

As of the research date, SECP publishes:
- `List of Securities Managers as on July 31, 2026`;
- `List of NBFCs and Securities and Futures Advisors as on July 31, 2026`.

These lists are the preferred current licensing references for candidate verification. The file contents must be inspected directly before marking any entity currently licensed.

### Next Capital historical/current candidate evidence

Public market reporting dated 25 August 2025 states that Next Capital Limited announced to PSX that SECP had granted it a Securities Manager licence and that it was the first securities broker to receive such a licence.

Engineering/business interpretation:
- Next Capital is a **candidate for direct due diligence**, not an automatically approved/current partner;
- current licence status, custodian structure, portfolio-management product readiness, API/white-label capability, automation policy and commercial willingness must be verified independently.

### Current active brokerage ecosystem

PSX's current July 2026 top-broker page includes active firms such as:
- AKD Securities Limited;
- JS Global Capital Limited;
- KTrade Securities Limited;
- Arif Habib Limited;
- BMA Capital Management Limited;
- Next Capital Limited;
- and other active brokers across account-opening, volume and traded-value rankings.

These names establish active brokerage-market presence only. They **do not prove** Securities Manager, Securities Adviser, Research Analyst, API, embedded-broker or white-label eligibility.

## Candidate scoring model

Do not pick a partner by brand/reputation alone. Score independently:

| Dimension | Weight guidance |
|---|---:|
| regulatory fit/current licences | critical gate |
| custody/customer-money model | critical gate |
| API/order/reconciliation quality | critical gate |
| cybersecurity/IBTS evidence | critical gate |
| discretionary/advisory permissions | critical gate per mode |
| sandbox/certification support | high |
| SLA/incident process | high |
| data rights | high |
| white-label/customer ownership | high |
| commercial pricing | medium |
| onboarding conversion/UX | medium |
| scale/performance | medium-high |
| roadmap alignment | medium |

A failed critical gate disqualifies the partner for that product mode regardless of total score.

## Multi-partner architecture

Klyvesta should preserve abstraction boundaries so one provider is not forced to perform every role.

```text
Klyvesta
  |
  +-- BrokerAdapter --------> Broker / embedded broker
  +-- ResearchProvider -----> registered/permitted research path
  +-- AdviceAuthority ------> adviser/permitted broker path
  +-- PortfolioAuthority ---> Securities Manager path
  +-- CustodyReference -----> independent custodian / broker structure
  +-- MarketDataProvider ---> licensed data source
```

A single partner may implement multiple interfaces only when regulatory and contractual evidence supports every role.

## Failover is not automatic for financial custody

Multi-broker support is a long-term architecture goal, but switching a live customer's broker/custodian is not equivalent to changing a cloud API endpoint.

Migration may require:
- customer consent;
- new KYC/account/UIN/custody steps;
- asset/cash transfer;
- regulatory notices;
- tax/cost-basis continuity;
- statement/archive continuity;
- reconciliation and freeze windows.

Therefore provider portability is designed at the software contract layer but executed through regulated migration procedures.

## Partner outreach package

For each candidate request:
- legal entity and licence numbers;
- current licence evidence;
- TREC/IBTS status;
- Securities Manager/Adviser/Research Analyst scope if applicable;
- custodian identity;
- onboarding/account/UIN flow;
- customer-money/funding/withdrawal flow;
- API/sandbox documentation;
- idempotency/order/fill/webhook semantics;
- market-data rights;
- statements/corporate actions;
- rate limits/SLA;
- security certification/testing expectations;
- white-label/customer-data ownership;
- AI/automation policy;
- commercial terms;
- termination/data portability.

## Decision policy

```text
Partner marketing claim
       !=
Production evidence
```

Only current authoritative licence evidence + signed commercial/legal allocation + tested technical capability can unlock a partner role.