# Checkpoint — P0 Compliance + P1 Readiness

Date: 2026-08-25
Branch: `p0/pypsx-operating-model`
Draft PR: #7

## Status

**PARTIALLY COMPLETE.** pyPSX partner evidence remains outstanding, P0 remains active/not accepted, but the project now has implementation-ready onboarding/AML, investor-protection, privacy/data-governance and P1 paper/shadow security baselines.

## Completed

- Reconfirmed pyPSX has not replied yet (owner-confirmed current status).
- Researched current official/high-authority Pakistan capital-market material covering digital onboarding, AML/CFT/CPF, broker/IBTS security, investor complaints and current federal privacy-legislation status indicators.
- Added `docs/P0_DIGITAL_ONBOARDING_AML_OPERATING_BASELINE.md`.
- Added `docs/P0_INVESTOR_PROTECTION_COMPLAINTS_BASELINE.md`.
- Added `docs/P0_PRIVACY_DATA_GOVERNANCE_BASELINE.md`.
- Added `docs/P1_PAPER_SHADOW_IMPLEMENTATION_BACKLOG.md`.
- Added `contracts/security/P1_SECURITY_ACCEPTANCE_V1.yaml` containing 36 security acceptance checks and six zero-tolerance classes.
- Updated canonical `.ai/state.json` with the new P0/P1 baselines.

## Research findings

### Digital onboarding / AML

- SECP lists Circular 03 of 2026 (2026-01-29) for digital onboarding of investors through financial institutions or third parties via API integration.
- SECP lists AML/CFT/CPF Regulations, 2020 as amended up to 2026-07-03 as the current published AML baseline.
- SECP's 2026-04-24 press release proposed IBAN verification, verified bank/e-wallet usage and multi-biometric authentication and explicitly stated regulated persons remain responsible for KYC, due diligence, transaction monitoring and AML compliance.
- Because the April item was described as a proposal and the July regulation text has not yet been mapped line-by-line here, proposed mechanisms are not treated as final obligations without current-text/partner verification.

### Investor complaints

- PSX provides investor grievance handling through its Regulatory Affairs function and describes mediation/arbitration paths for unresolved broker disputes.
- SECP provides an investor complaint mechanism and advises taking complaints first to the relevant broker/exchange/entity before escalation.
- Klyvesta therefore requires an internal complaint/evidence system that preserves external escalation rights and cannot silently rewrite financial history.

### Privacy / data law status

- Current National Assembly passed-bills listings reviewed on 2026-08-25 did not show a passed federal Personal Data Protection Bill/Act.
- Senate records show the Personal Data Protection Bill, 2023 was not passed and was disposed/withdrawn at committee stage.
- Other government material references finalized/proposed data-protection legislation, but that wording is not treated as sufficient evidence that a Personal Data Protection Act 2026 is currently in force.
- Pre-launch legal recheck against Gazette/Pakistan Code/Parliament remains mandatory.

## P1 implementation backlog ready after F0

Implementation order is now explicitly defined:

1. PostgreSQL/persistence.
2. Identity/security abstraction.
3. Authorization.
4. Immutable double-entry ledger.
5. Transactional outbox.
6. PaperBrokerAdapter.
7. OMS state machine.
8. Portfolio/reconciliation.
9. Deterministic Risk Governor.
10. Compliance Gate.
11. AI/Quant shadow boundary.
12. Notifications/investment timeline.
13. Observability/incident baseline.
14. Security acceptance automation.
15. Performance/resilience.

## P1 security zero tolerance

- unauthorized financial command = 0;
- cross-customer resource access = 0;
- AI direct broker execution = 0;
- duplicate financial effect from replay = 0;
- secret/restricted PII in logs = 0;
- ledger imbalance = 0.

## Verification

This increment adds specifications/documentation only; no production runtime code was introduced.

Verified:
- files committed on isolated P0 branch;
- canonical state references the new documents/catalogue;
- no live broker/advice/Auto feature was unlocked;
- no credentials/customer data/proprietary strategy implementation was added.

Not verified/executable yet:
- P1 security catalogue runtime tests;
- database migrations;
- identity provider integration;
- PaperBroker runtime;
- AML/KYC provider API contracts;
- pyPSX sandbox/API semantics;
- current privacy statute opinion from counsel.

## Open blockers

1. Issue #1 — `main` branch protection.
2. Issue #2 — public GPL vs private/proprietary/split model.
3. pyPSX technical/commercial/API response.
4. regulated-person allocation for onboarding/KYC/AML.
5. final funding/custody/withdrawal/data-rights model.
6. advisory/research/discretionary legal classifications and partner roles.
7. final privacy/data-retention legal review before live customer data.

## Exact next action

Resolve F0 Issues #1/#2 so generic F1 can be merged safely. Then implement the P1 backlog in paper/shadow mode beginning with PostgreSQL, identity/authorization and immutable ledger foundations. Continue partner/regulatory due diligence in parallel. When pyPSX replies, map actual evidence into the BrokerAdapter/RACI/capability matrices and do not infer live behavior from marketing material.
