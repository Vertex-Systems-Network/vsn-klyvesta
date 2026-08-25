# P0 — pyPSX Public Evidence Refresh — 2026-08-26

Status: **P0-T1 ACTIVE / PUBLIC ROLLOUT STATE CONTRADICTORY / PARTNER CONTRACT STILL REQUIRED**

Tracker: Issue #20 — `P0-T1: Resolve pyPSX public rollout-state contradiction and obtain partner contract`

This document is a time-bounded public-evidence refresh for `P0-T1 — Confirm regulatory and pyPSX Broker API operating model`. It does not replace `docs/P0_PYPSX_OPERATING_MODEL_DUE_DILIGENCE.md`, partner legal/technical documents, or counsel/regulatory review.

No live broker integration, real-money operation, production PII, personalized production AI advice, or Guarded Auto capability is unlocked by this refresh.

## Executive finding

Public pyPSX evidence remains internally inconsistent on the Broker API rollout state.

A fresher search-index view of the official Broker API material observed on 2026-08-26 described the Broker API as **live** and **onboarding partners**. It also continued to advertise the high-level Broker API scope: account opening, KYC/AML, order routing, custody, T+1 settlement, reporting/statements, robo-advisor, model portfolios, embedded investing, and related use cases.

However, directly opened official pyPSX page snapshots available during the same review still described the Broker API as **early access**, stated that the sandbox was coming soon / in development with design partners, and described live broker execution as still in development.

Because those official public views disagree, Klyvesta must not infer that production Broker API access or a Broker API sandbox is currently available to Klyvesta.

## Evidence observed

### Official pyPSX Broker API — fresher indexed material

Observed from the official `https://www.pypsx.com/broker` search-index result on 2026-08-26:

- public status text represented the Broker API as live/onboarding partners;
- high-level scope included accounts, KYC/AML, order routing, custody, T+1 settlement, and reporting/statements;
- product examples included robo-advisor and model portfolios;
- the access form stated that the team replies personally within a week.

This is useful rollout/product evidence, but it is still public marketing material rather than a technical or commercial contract.

### Official pyPSX Broker API — directly opened snapshot

Directly opened official Broker API content available during the same review still showed:

- `Broker API · early access`;
- sandbox coming soon / design-partner development wording;
- request-early-access language.

### Official pyPSX home page — contradictory snapshots

Fresh indexed pyPSX home-page material also represented the Broker API as live and separated it from the Trading API.

A directly opened cached official home-page snapshot still described:

- live broker execution as coming/in development;
- Broker API as early access;
- Broker API sandbox as coming soon.

## Public evidence classification

Use these states for P0 only:

| Item | State after 2026-08-26 refresh |
|---|---|
| Broker API product exists | `VERIFIED_PUBLIC_CLAIM` |
| Account opening | `VERIFIED_PUBLIC_CLAIM` |
| KYC/AML | `VERIFIED_PUBLIC_CLAIM` |
| Order routing | `VERIFIED_PUBLIC_CLAIM` |
| Custody | `VERIFIED_PUBLIC_CLAIM` |
| T+1 settlement | `VERIFIED_PUBLIC_CLAIM` |
| Reporting/statements | `VERIFIED_PUBLIC_CLAIM` |
| Robo-advisor/model-portfolio positioning | `VERIFIED_PUBLIC_CLAIM` |
| Production-live rollout state | `CONTRADICTORY_PUBLIC_EVIDENCE / UNVERIFIED_FOR_KLYVESTA` |
| Sandbox currently available | `CONTRADICTORY_PUBLIC_EVIDENCE / UNVERIFIED_FOR_KLYVESTA` |
| Klyvesta partner acceptance | `UNVERIFIED` |
| Underlying licensed broker identity | `UNVERIFIED` |
| API/OpenAPI/Postman contract | `UNVERIFIED` |
| Authentication / credential semantics | `UNVERIFIED` |
| Order idempotency / timeout / retry semantics | `UNVERIFIED` |
| Stable execution IDs | `UNVERIFIED` |
| Webhook/event signing and replay contract | `UNVERIFIED` |
| Funding/deposit architecture | `UNVERIFIED` |
| Withdrawal API semantics | `UNVERIFIED` |
| Market-data redistribution rights | `UNVERIFIED` |
| SLA / support / incident contract | `UNVERIFIED` |
| Commercial / white-label / customer-data terms | `UNVERIFIED` |
| Securities Manager / advisory / discretionary path | `UNVERIFIED` |

`VERIFIED_PUBLIC_CLAIM` remains intentionally weaker than production verification.

## Fresh SECP context

The regulatory baseline remains active rather than frozen:

- SECP continues to list Securities Managers as of July 31, 2026;
- SECP continues to list NBFCs and Securities and Futures Advisors as of July 31, 2026;
- SECP lists Securities Brokers as of July 31, 2026;
- SECP published `S.R.O.1376(I)/2026` on August 19, 2026 as draft amendments to the Securities Brokers (Licensing & Operations) Regulations, 2016;
- the SECP AML/CFT/CPF Regulations 2020 are published as amended through July 3, 2026.

Therefore launch/legal review must use the final current rules in force at the relevant launch date and must re-check whether the August 19 broker-regulation draft has been adopted, changed, or withdrawn.

## P0 gate impact

No P0 gate is upgraded to accepted by this refresh.

- `pypsx_broker_api_contract_obtained` — **NOT MET**.
- `underlying_regulated_roles_identified` — **NOT MET**.
- `custody_settlement_flow_documented` — **PARTIAL PUBLIC CLAIM ONLY**.
- `funding_flow_documented` — **NOT MET**.
- `market_data_rights_documented` — **NOT MET**.
- `ai_assisted_legal_classification_documented` — **NOT MET**.
- `discretionary_auto_legal_classification_documented` — **PARTIAL REGULATORY EVIDENCE ONLY**.
- `required_licence_or_licensed_partner_path_decided` — **NOT MET**.

## Required direct partner evidence

Issue #20 is the active resolution checklist. At minimum, written partner evidence must answer:

1. Is Broker API currently production-live, limited-live, or early access for new partners?
2. Is a Broker API sandbox available now, and what is the onboarding/certification sequence?
3. Which exact SECP-licensed broker legal entity and PSX/TREC identity back the product?
4. What are the KYC/UIN/CDC/NCCPL/custody and customer-agreement responsibility boundaries?
5. What are the technical auth, environment, key-rotation and network-security requirements?
6. What are the exact order/execution/idempotency/timeout/retry/cancel/replace semantics?
7. How are ambiguous side effects represented and reconciled?
8. Are events/webhooks available, and how are signatures, replay prevention, retry and ordering handled?
9. What are the deposit/funding and withdrawal flows?
10. What market-data source, freshness and redistribution rights apply to Klyvesta clients?
11. What SLA, rate-limit, support, maintenance and incident obligations apply?
12. What commercial, branding, white-label, client-ownership, data-ownership and termination terms apply?
13. Which regulated party, if any, can support personalized advice, research, model portfolios or discretionary management?
14. What minimum-investment, suitability, IPS, custody, disclosure and client-eligibility conditions apply?

## Engineering consequence

Until direct evidence resolves the contradiction:

- do not implement `PyPsxBrokerAdapter` from public website assumptions;
- do not map the public Trading API's paper/live semantics onto the separate Broker API;
- do not assume Broker API sandbox credentials exist;
- keep all external financial side effects behind the generic `BrokerAdapter` boundary;
- keep ambiguous external effects in `UNKNOWN` until query/reconciliation resolves them;
- continue only public/regulatory due diligence and explicitly allowed paper/security work;
- preserve the live/PII/advice/Guarded-Auto feature gates.

## Sources reviewed

- pyPSX official Broker API page: `https://www.pypsx.com/broker`
- pyPSX official home page: `https://www.pypsx.com/`
- SECP Securities Managers list as of July 31, 2026
- SECP NBFCs and Securities and Futures Advisors list as of July 31, 2026
- SECP Securities Brokers list as of July 31, 2026
- SECP `S.R.O.1376(I)/2026` draft broker-regulation amendments dated August 19, 2026
- SECP AML/CFT/CPF Regulations 2020 as amended through July 3, 2026

## Exact continuation point

Obtain direct pyPSX partner technical/commercial evidence and reconcile it field-by-field against:

- `docs/P0_PYPSX_OPERATING_MODEL_DUE_DILIGENCE.md`;
- `docs/P0_PARTNER_RESPONSIBILITY_RACI.md`;
- `contracts/broker/BROKER_ADAPTER_V1.md`;
- funding/custody/market-data flows;
- threat model and security controls;
- `.ai/acceptance-gates.yaml`.

Do not mark P0 complete until those fields have direct acceptance evidence.