# Checkpoint — P0 pyPSX Operating Model Due Diligence

Date: 2026-08-25
Branch: `p0/pypsx-operating-model`
Draft PR: #7

## Status

**PARTIALLY COMPLETE — P0 public/regulatory evidence, responsibility/mode gates, regulatory-function classification, a no-reply continuation path, and paper/shadow acceptance requirements are formalized; pyPSX partner/API terms and the final regulated operating model remain unverified.**

Canonical active task remains:
`P0-T1 — Confirm regulatory and pyPSX Broker API operating model`.

The owner confirmed in the current session that **pyPSX has not replied yet**.

## Completed

- Re-read canonical engineering protocol, guardrails and acceptance gates.
- Verified `main` remains unprotected (`protected: false`) and has no required status checks configured.
- Verified F1 draft PR #6 latest head has both required CI/security checks passing.
- Confirmed Issue #1 and Issue #2 remain governance blockers for merging substantive implementation.
- Performed fresh public/official regulatory and pyPSX research.
- Confirmed SECP Securities Managers framework materially constrains discretionary/portfolio-management product design.
- Recorded the PKR 5 million minimum client investment threshold, independent-custodian requirement and securities-manager licensing eligibility evidence from SECP public material.
- Confirmed current SECP licensing guidance states giving investment advice to others is a regulated Securities Adviser activity.
- Confirmed SECP's January 2026 Research Analyst amendments introduced a registration mechanism and strengthened controls around research reports/social-media-equivalent research functions.
- Confirmed PSX IBTS rules require order identification, audit trail, risk controls, information security and client-order/communication records.
- Confirmed the current SECP site lists a draft amendment dated 19 August 2026 to Securities Brokers (Licensing & Operations) Regulations, so broker-regulation changes must be tracked before launch.
- Created `docs/P0_PYPSX_OPERATING_MODEL_DUE_DILIGENCE.md` with operating-model separation, public capability evidence, regulatory consequences, technical/commercial contract checklist, P0 acceptance mapping and a partner question package.
- Created `docs/P0_PARTNER_RESPONSIBILITY_RACI.md` to force one accountable party for legal/customer/KYC/AML/UIN/CDC/custody/funding/orders/IBTS/AI/security/data responsibilities before production.
- Created `docs/P0_PRODUCT_MODE_ELIGIBILITY.md` to separate subscription entitlement from regulatory/security/risk/mandate authority for Manual, AI-assisted and Guarded Auto modes.
- Created `docs/P0_REGULATORY_FUNCTION_CLASSIFICATION.md` separating Education (E), Research (R), Personalized Advice (A) and Discretionary Management (D).
- Created `docs/P0_LAUNCH_PATH_WITHOUT_PYPSX.md` so paper/shadow/foundation work can continue without guessing live broker behavior.
- Created `.ai/regulatory-feature-gates.json` as the machine-readable runtime/engineering gating baseline.
- Created `docs/PAPER_SHADOW_ACCEPTANCE_SPEC_V1.md` with deterministic financial invariants, paper-broker failure scenarios, ledger/reconciliation requirements, AI shadow evals, performance/load/security acceptance evidence and explicit non-goals.
- Created `contracts/broker/PAPER_BROKER_SCENARIOS_V1.yaml` with 20 machine-readable PaperBroker failure/edge scenarios and zero-tolerance outcomes.
- Updated `.ai/state.json` to record the user-confirmed no-reply state and allowed/blocked parallel work.
- Added owner/admin action notes to Issues #1/#2 for branch protection and immediate repository-privacy/licensing containment.
- Opened draft PR #7 from the isolated P0 branch; no merge performed.

## Research performed

Primary/high-authority evidence used:

- pyPSX Broker API public material for high-level account/KYC/order-routing/custody/T+1/reporting claims.
- SECP Securities Managers (Licensing and Operations) Regulations, 2024 and related public release.
- SECP Securities/Futures Adviser licensing guidance.
- SECP January 2026 Research Analyst regulatory update.
- PSX public rule/security material for broker/IBTS and information-security due-diligence requirements.
- SECP current laws/drafts listings, including the 19 August 2026 draft broker-regulation amendments.

Important findings:

1. SECP's Securities Managers framework states eligible securities brokers may provide portfolio-management services after obtaining a Securities Manager licence; customer funds/securities are maintained with an independent custodian and the public framework includes a PKR 5 million minimum investment threshold for an eligible customer.
2. Giving investment advice to others is a regulated Securities Adviser activity under SECP's current licensing guidance.
3. Research/public market-opinion functionality also requires separate review: SECP's January 2026 Research Analyst amendments added a registration mechanism and explicitly strengthened controls around research reports and social-media-equivalent research functions.
4. Therefore Klyvesta must not assume that small-ticket fully discretionary AI management, personalized AI advice, or AI-generated security research is automatically available to every user merely because the implementation is automated.

## Regulatory function model

Klyvesta now treats functions separately:

- **E — Education / neutral assistance**
- **R — Research / market opinion**
- **A — Personalized investment advice**
- **D — Discretionary portfolio management**

A feature may fall into multiple classes; the strictest applicable production gate wins until authoritative review says otherwise.

Important consequences:
- `AI Research` is not assumed unregulated.
- `AI Assisted` remains production-off until an accepted adviser/permitted-partner path exists.
- `Guarded Auto` remains production-off until the discretionary-management path, eligible-customer rule, custodian arrangement, mandate/IPS and partner responsibilities are accepted.
- buying an AI package never creates legal advice, trading or discretionary authority.

## No-reply continuation path

pyPSX delay does not block all engineering. After repository-governance approval, the project may continue with:
- generic .NET foundation;
- PostgreSQL/migrations;
- identity/session/security;
- authorization/entitlements;
- immutable ledger/idempotency/outbox;
- PaperBroker + OMS + reconciliation;
- deterministic risk/compliance in paper mode;
- AI/quant shadow mode;
- customer/client shells with explicit non-live states;
- security/resilience work.

Hard waiting boundary:
- no `PyPsxBrokerAdapter` from guesses;
- no real-money trading;
- no live personalized AI advice;
- no live Guarded Auto.

## Paper/shadow readiness baseline

Before a live broker adapter is considered, the paper system must prove at minimum:
- balanced append-only ledger with compensating reversals;
- exactly-once financial effects for duplicate commands/executions;
- no fill -> no executed position change;
- no valid mandate -> no auto order;
- unauthorized resource -> no broker call;
- ambiguous timeout -> `UNKNOWN` + reconciliation, not blind retry;
- partial fill/cancel race correctness;
- stale critical data -> no auto execution;
- reconciliation mismatch -> exception/hold, not silent correction;
- AI cannot bypass mandate/risk/compliance/execution boundaries.

20 required PaperBroker scenarios are now machine-readable in `contracts/broker/PAPER_BROKER_SCENARIOS_V1.yaml`.

## P0 gate status

- `pypsx_broker_api_contract_obtained`: NOT MET
- `underlying_regulated_roles_identified`: NOT MET
- `custody_settlement_flow_documented`: PARTIAL ONLY
- `funding_flow_documented`: NOT MET
- `market_data_rights_documented`: NOT MET
- `ai_assisted_legal_classification_documented`: PARTIAL REGULATORY EVIDENCE ONLY
- `discretionary_auto_legal_classification_documented`: PARTIAL REGULATORY EVIDENCE ONLY
- `required_licence_or_licensed_partner_path_decided`: NOT MET

P0 is **not accepted**.

## Verification / tests

This branch changes documentation/machine-readable governance only. No production runtime code was added, so application build/runtime testing is not applicable to this P0 increment.

Verified repository evidence:
- `main` branch currently reports `protected: false` and no required status checks.
- F1 PR #6 current head has successful `dotnet-foundation` and `codeql` workflow runs.
- P0 PR #7 is open as draft and isolated from F1 implementation.

## Security / architecture review

No production code, broker credentials, customer data, proprietary strategy logic or AI trading implementation was added.

Hard-deny conditions remain:
- guaranteed-profit claims;
- zero-loss claims;
- risk-free claims;
- subscription implying execution authority;
- AI direct broker execution;
- live broker behavior inferred from unverified marketing/docs;
- customer money routed through Klyvesta's normal operating account without an approved legal structure.

## Known blockers

- pyPSX technical/commercial partner response and current API documentation.
- exact underlying licensed broker identity and responsibilities.
- discretionary-management/licensing path for Guarded Auto.
- personalized AI recommendation path.
- research-analyst classification for relevant AI/public research outputs.
- market-data licensing/redistribution rights.
- customer funding/withdrawal architecture.
- Issue #1: `main` branch protection.
- Issue #2: public GPL vs proprietary/split repository model.

## Exact next action

1. Resolve repository governance Issues #1/#2 so generic foundation implementation can be safely merged.
2. Continue paper/shadow design and security work that does not require live broker assumptions.
3. When pyPSX replies, treat its partner contract as the next authoritative integration input and map it field-by-field against:
   - `docs/P0_PYPSX_OPERATING_MODEL_DUE_DILIGENCE.md`
   - `docs/P0_PARTNER_RESPONSIBILITY_RACI.md`
   - `docs/P0_PRODUCT_MODE_ELIGIBILITY.md`
   - `docs/P0_REGULATORY_FUNCTION_CLASSIFICATION.md`
   - `contracts/broker/BROKER_ADAPTER_V1.md`
4. Do not mark P0 accepted until regulated roles, custody/settlement/funding/data rights and product-mode classifications are supported by authoritative evidence.
