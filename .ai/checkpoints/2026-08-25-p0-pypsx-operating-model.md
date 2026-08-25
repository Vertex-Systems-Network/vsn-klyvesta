# Checkpoint — P0 pyPSX Operating Model Due Diligence

Date: 2026-08-25
Branch: `p0/pypsx-operating-model`
Draft PR: #7

## Status

**PARTIALLY COMPLETE — regulatory/public partner evidence and implementation-ready responsibility/mode gates are formalized, but the pyPSX partner contract, underlying regulated roles and production technical terms remain unverified.**

Canonical active task remains:
`P0-T1 — Confirm regulatory and pyPSX Broker API operating model`.

## Completed

- Re-read canonical engineering protocol, guardrails and acceptance gates.
- Verified `main` remains unprotected (`protected: false`).
- Verified F1 draft PR #6 latest head has both required CI/security checks passing.
- Confirmed Issue #1 and Issue #2 remain open governance blockers for merging substantive implementation.
- Performed fresh public/official regulatory and pyPSX research.
- Confirmed SECP Securities Managers framework materially constrains discretionary/portfolio-management product design.
- Recorded the Rs 5 million minimum client investment threshold, independent-custodian requirement and securities-manager licensing eligibility evidence from SECP's 14 November 2024 press release.
- Created `docs/P0_PYPSX_OPERATING_MODEL_DUE_DILIGENCE.md` with operating-model separation, public capability evidence, regulatory consequences, technical/commercial contract checklist, P0 acceptance mapping and a partner question package.
- Created `docs/P0_PARTNER_RESPONSIBILITY_RACI.md` to force one accountable party for legal/customer/KYC/AML/UIN/CDC/custody/funding/orders/IBTS/AI/security/data responsibilities before production.
- Created `docs/P0_PRODUCT_MODE_ELIGIBILITY.md` to separate subscription entitlement from regulatory/security/risk/mandate authority for Manual, AI-assisted and Guarded Auto modes.
- Opened draft PR #7 from the isolated P0 branch; no merge performed.

## Research performed

Primary/high-authority evidence used:

- pyPSX Broker API public material for high-level account/KYC/order-routing/custody/T+1/reporting claims.
- SECP press release dated 14 November 2024: Securities Managers (Licensing and Operations) Regulations, 2024.
- PSX public rule/security material for broker/IBTS and information-security due-diligence requirements.

Important finding:

SECP's 2024 Securities Managers framework states eligible securities brokers may provide portfolio-management services after obtaining a Securities Manager licence; eligibility includes Rs 30 million minimum net worth, BFR 2 or higher and adequate research capacity. Customer funds/securities are to be with an independent custodian, and the minimum investment threshold a securities manager may accept from a client is Rs 5 million.

Therefore Klyvesta must not assume that small-ticket fully discretionary AI management is legally available to every retail user.

## Product / architecture decisions persisted

- Manual, AI-assisted and Guarded Auto are separate authority modes.
- A commercial package such as `Autopilot` is never sufficient authorization for automatic trading.
- Guarded Auto requires its own regulatory/licensed-partner path, customer eligibility, mandate/IPS/suitability controls and safety gates.
- Unknown mandatory eligibility => DENY/HOLD, never optimistic execution.
- No implementation may evade discretionary-management classification by renaming the activity as an AI agent.
- Customer funds must not default through Klyvesta's ordinary corporate bank account.
- Broker integration remains behind `BrokerAdapter`; broker marketing claims are not accepted as production contract evidence.

## External data not verified

A Gmail search for a pyPSX response was attempted because the owner had already submitted the partner application, but connected Gmail access was not approved in this session. Therefore:

- no claim is made that pyPSX has or has not replied;
- no private partner email evidence was used;
- direct pyPSX technical/commercial documentation remains the next authority.

## P0 gate status

- `pypsx_broker_api_contract_obtained`: NOT MET
- `underlying_regulated_roles_identified`: NOT MET
- `custody_settlement_flow_documented`: PARTIAL ONLY
- `funding_flow_documented`: NOT MET
- `market_data_rights_documented`: NOT MET
- `ai_assisted_legal_classification_documented`: NOT MET
- `discretionary_auto_legal_classification_documented`: PARTIAL REGULATORY EVIDENCE ONLY
- `required_licence_or_licensed_partner_path_decided`: NOT MET

P0 is therefore **not accepted**.

## Verification / tests

This branch changes documentation only. No production code was added, so application build/runtime testing is not applicable to this P0 increment.

Verified repository evidence:

- `main` branch currently reports `protected: false` and no required status checks.
- F1 PR #6 current head has successful `dotnet-foundation` and `codeql` workflow runs.
- P0 PR #7 is open as draft and isolated from F1 implementation.

## Security / architecture review

No production code, broker credentials, customer data, proprietary strategy logic or AI trading implementation was added.

The following remain locked:

- production real-money integration;
- personalized AI advice production;
- Guarded Auto production;
- claims of guaranteed return, zero loss or risk-free investing.

## Known blockers

- pyPSX technical/commercial partner response and current API documentation.
- exact underlying licensed broker identity and responsibilities.
- discretionary-management/licensing path for Guarded Auto.
- AI-assisted recommendation legal classification.
- market-data licensing/redistribution rights.
- customer funding/withdrawal architecture.
- Issue #1: `main` branch protection.
- Issue #2: public GPL vs proprietary/split repository model.

## Exact next action

Obtain and review the pyPSX partner response/technical contract. Map every supplied field against:

- `docs/P0_PYPSX_OPERATING_MODEL_DUE_DILIGENCE.md`
- `docs/P0_PARTNER_RESPONSIBILITY_RACI.md`
- `docs/P0_PRODUCT_MODE_ELIGIBILITY.md`
- `contracts/broker/BROKER_ADAPTER_V1.md`

Do not mark P0 complete until the required regulated roles, custody/settlement/funding/data-rights and Manual/AI-assisted/Guarded-Auto legal classifications are documented with authoritative evidence.
