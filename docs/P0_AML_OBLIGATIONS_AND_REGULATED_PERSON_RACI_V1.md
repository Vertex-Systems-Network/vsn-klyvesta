# P0 AML/CFT/CPF Obligations & Regulated-Person RACI V1

Status: **Draft non-live P0 operating artifact for ABD-67 / GitHub Issue #8.** This is an engineering/compliance operating model, not legal advice. Partner-specific accountability remains unverified until direct broker/pyPSX evidence is received.

## Current authoritative baseline

Verified on 2026-08-26 against SECP official sources:

- `SECP-AML-CFT-CPF Regulations-2020 as amended up to July 3, 2026` is the current SECP-published AML/CFT/CPF regulations baseline.
- `Circular 03 of 2026 — Digital Onboarding of Investors through FI or 3rd Parties via API Integration`, dated 2026-01-29, is an active onboarding source.

Repository sources:

- `docs/P0_DIGITAL_ONBOARDING_AML_OPERATING_BASELINE.md`
- `docs/P0_PARTNER_RESPONSIBILITY_RACI.md`
- `docs/P0_PRIVACY_DATA_GOVERNANCE_BASELINE.md`
- `docs/AUTHORIZATION_PRIVILEGE_MODEL.md`
- GitHub Issue #8
- GitHub Issue #20 / P0-T1 partner evidence tracker

## Clause-verification status

Current official material confirms at least the following clause anchors used by this engineering matrix:

- Regulation 4 — risk assessment / risk understanding.
- Regulation 19 — ongoing monitoring / ongoing due diligence.
- Regulation 26 — record keeping, including transaction reconstruction, CDD records and minimum retention obligations.
- Regulation 27 — compliance program / internal policies, procedures and controls.

Other obligation rows below are required by the current SECP AML/CFT/CPF framework and SECP guidance/FAQs, but exact current clause numbering must be rechecked against the authoritative July 3, 2026 consolidated PDF before final legal acceptance. Where a clause number has not been directly verified, this document intentionally does not invent one.

## Core allocation rule

Klyvesta may orchestrate workflows and enforce deterministic controls, but **the legally accountable regulated person must be explicitly identified before production**.

Until direct partner evidence resolves responsibility:

- `REGULATED_PERSON = UNKNOWN / PARTNER EVIDENCE REQUIRED`
- partner-specific fields remain `UNKNOWN / PARTNER EVIDENCE REQUIRED`
- onboarding may not reach production `ACTIVE` based on Klyvesta-only assumptions
- AI may assist triage/summarization but may not clear AML alerts, approve CDD, lift restrictions, file/suppress reports, or override sanctions/TFS controls.

## Role definitions used in this RACI

- **RP** — the legally accountable SECP-regulated person for the customer/account. Exact entity currently unknown.
- **Klyvesta Compliance Workflow** — deterministic orchestration, evidence collection, state enforcement and case management.
- **Provider** — identity/biometric/bank/sanctions/other third-party evidence provider.
- **Broker/pyPSX** — broker/API/partner layer; exact responsibilities unverified.
- **Human Compliance** — authorized KYC/AML/compliance analyst/officer working under the accountable RP model.
- **AI Assist** — non-authoritative summarization/triage only.

## Obligations matrix

| Obligation area | Current engineering requirement | Accountable | Responsible / system role | Evidence required | Fail-closed state |
| --- | --- | --- | --- | --- | --- |
| Enterprise/customer risk assessment | Identify and assess ML/TF/PF risk across customer, geography, product/service, transaction and delivery-channel factors; customer rating must not be derived from one sector factor alone. | RP — **UNKNOWN** | RP policy owner + Klyvesta deterministic risk workflow | approved risk methodology, factor/version evidence | `COMPLIANCE_HOLD` if policy/accountability unknown |
| Customer identification & verification (CDD/KYC) | Identify and verify customer using current permitted evidence/methods; identity proofing is separate from login authentication. | RP — **UNKNOWN** | Provider evidence + Human Compliance + Klyvesta case state | provider reference, method, timestamps, policy version, reviewer where required | no account activation if CDD incomplete |
| Beneficial ownership | Identify/verify beneficial owner(s) where applicable and preserve entity/control evidence. | RP — **UNKNOWN** | Human Compliance + provider/registry evidence | ownership/control evidence and verification result | onboarding restricted until resolved |
| Customer purpose / expected activity | Establish intended nature/purpose and expected activity sufficient for ongoing monitoring. | RP — **UNKNOWN** | Klyvesta intake + Human Compliance | declared purpose/profile, source and version | `REVIEW_PENDING` / `COMPLIANCE_HOLD` |
| Customer risk categorization | Apply documented risk-based classification using multiple relevant factors and update when material facts change. | RP — **UNKNOWN** | deterministic policy + Human Compliance | risk score/category inputs, result, policy version | no silent downgrade by AI/provider |
| PEP identification & EDD | Screen beyond self-declaration; apply risk-sensitive PEP controls, appropriate approvals, source-of-wealth/source-of-funds measures and enhanced monitoring where required. | RP — **UNKNOWN** | screening provider + Human Compliance | screening source/version, match resolution, approvals, SoW/SoF evidence | high-risk match stays restricted until authorized disposition |
| Sanctions / TFS screening | Screen customers/beneficial owners/associates against authoritative applicable sources and enforce freezes/restrictions where required. | RP — **UNKNOWN** | screening provider + deterministic Compliance Gate + Human Compliance | source/list version, match evidence, disposition | positive/uncertain material match fails closed |
| Source of funds / source of wealth | Collect and verify risk-appropriate evidence where required, especially elevated-risk relationships. | RP — **UNKNOWN** | Human Compliance + evidence provider | evidence refs, reviewer, decision reason | restricted until required evidence accepted |
| Ongoing monitoring — Reg. 19 | Monitor relationship/transactions and keep CDD data current using risk-sensitive periodic/event-driven review. | RP — **UNKNOWN** | monitoring engine + Human Compliance | transaction/activity alerts, review dates, refreshed CDD evidence | unresolved material anomaly triggers hold/review |
| Event-driven re-review | Re-review on material profile/account activity changes, suspicion, doubts about prior evidence, recovery/security changes or policy/provider changes. | RP — **UNKNOWN** | Klyvesta event rules + Human Compliance | trigger, prior/new evidence, decision | affected capabilities restricted pending review |
| CDD failure / inability to verify | Do not activate or continue a relationship where required CDD cannot be satisfactorily completed; consider reporting/escalation obligations. | RP — **UNKNOWN** | Human Compliance + Klyvesta state machine | failure reason, attempted evidence, escalation decision | `REJECTED` / `COMPLIANCE_HOLD` / controlled closure |
| Suspicious activity / STR escalation | Detect and escalate suspicious activity through the accountable RP process; reporting decision and filing authority must be identified. | RP — **UNKNOWN** | Human Compliance / Compliance Officer | case evidence, disposition, reporting reference where applicable | AI cannot suppress or close reporting decision |
| Freeze / restriction handling | Apply legally/policy-required account restrictions and preserve evidence; lifting high-risk restrictions requires authorized workflow. | RP — **UNKNOWN** | Compliance Gate + Human Compliance | source authority, restriction reason, approval/audit trail | restricted state persists until authorized release |
| Record keeping — Reg. 26 | Maintain sufficient records to reconstruct transactions; preserve CDD/identification, account/business correspondence and analysis records for required periods; retain longer for litigation/authority hold. | RP — **UNKNOWN** | Klyvesta records policy + designated records custodian | immutable refs, retention policy, legal-hold state | deletion blocked where regulatory/legal retention applies |
| Regulatory/authority record response — Reg. 26 | Support timely production of requested records to competent authorities according to applicable instruction/time limits. | RP — **UNKNOWN** | records custodian + Human Compliance | request ID, authority, scope, production audit trail | no uncontrolled support export |
| Compliance program — Reg. 27 | Maintain written policies, procedures, controls, governance, designated compliance responsibility, testing/training and monitoring appropriate to risk. | RP — **UNKNOWN** | RP compliance management + Klyvesta control evidence | approved policies, versioning, training/testing evidence | production disabled if control owner absent |
| Third-party CDD reliance | Third-party/provider use does not silently transfer ultimate accountability; provider suitability, evidence availability, controls and record-keeping must be verified. | RP — **UNKNOWN** | RP due diligence + Klyvesta provider register | contract, regulatory status, security/evidence terms | `UNKNOWN / CONTRACT REQUIRED` blocks production reliance |
| Staff/privileged access | Least privilege, case-purpose access, maker-checker for high-risk actions, audited access to restricted evidence. | RP + Klyvesta control owner — **partner allocation pending** | authorization service + Human Compliance | privileged access audit, approvals, role tests | unauthorized access = release blocker |
| AML model/AI assistance | AI may summarize/triage only. Deterministic/human-authorized workflow owns final CDD/AML disposition. | RP — **UNKNOWN** | AI Assist is Consulted only | model/version/output hash linked to authoritative decision | AI-only approval prohibited |

## Regulated-person RACI

Legend: **A** Accountable, **R** Responsible, **C** Consulted, **I** Informed, **U** Unknown pending direct evidence.

| Activity | RP legal entity | Broker/pyPSX | Klyvesta workflow | Provider | Human Compliance | AI Assist |
| --- | --- | --- | --- | --- | --- | --- |
| Final legal KYC/AML accountability | **U→A** | U | C | I | R under RP | I |
| Customer identity evidence collection | A/U | U | R orchestration | R evidence service | C/R review | I |
| Final CDD acceptance/rejection | **U→A** | U | C / enforce result | C | **R** | I |
| Beneficial-owner disposition | **U→A** | U | C | C | **R** | I |
| Customer risk classification policy | **U→A** | U | R deterministic execution | I | R/C | C only |
| PEP/sanctions/TFS screening source | A/U | U | R orchestration | **R** for contracted evidence | R disposition | C only |
| Ongoing transaction monitoring | **U→A** | U | R detection/orchestration | C where used | R disposition | C only |
| STR/regulatory report decision/filing | **U→A** | U | I/C workflow support | I | **R under RP authority** | I |
| Freeze/restriction decision | **U→A** | U | R enforcement | C | R/approve | I |
| Record retention legal schedule | **U→A** | U | R implementation | C contract-specific | R/C | I |
| Evidence storage/custody | A/U | U | R for Klyvesta-controlled records | R for provider-held evidence | C | I |
| Provider due diligence | **U→A** | U | C / maintain register | I | R/C | I |
| AML alert final clearance | **U→A** | U | enforce authorized outcome | C | **R** | **C only — never A/R** |

`U→A` means the activity requires an accountable regulated person, but Klyvesta has not yet verified which legal entity occupies that role.

## AI boundary

AI output may:

- summarize case evidence;
- cluster or prioritize alerts;
- draft analyst notes;
- explain deterministic policy results;
- identify missing evidence for human review.

AI output may not:

- mark CDD complete;
- clear a sanctions/PEP/TFS match;
- downgrade customer risk without deterministic/human authorization;
- release a compliance/freeze hold;
- decide that an STR is unnecessary;
- file or suppress a regulatory report autonomously;
- alter source evidence;
- create financial authority.

## Partner evidence questions required to resolve RACI

The following must be answered by direct written partner evidence before production:

1. Which exact broker/legal entity is the accountable regulated person for Klyvesta customers?
2. Does pyPSX perform KYC/AML, orchestrate it, or only transport data?
3. Who has final customer acceptance/rejection authority?
4. Who owns sanctions/PEP/TFS screening and which sources are authoritative?
5. Who owns ongoing transaction monitoring and alert disposition?
6. Who files STRs/other required reports and with which authority/workflow?
7. Who applies and lifts regulatory/compliance freezes/restrictions?
8. Who is records custodian for KYC/AML and transaction evidence?
9. What retention periods/format/availability obligations apply by record type?
10. What CDD/provider reliance is contractually permitted and what evidence is retrievable?
11. Which identity/biometric/bank verification methods are mandatory versus optional?
12. What API/event fields represent compliance status and are they authoritative or advisory?

All unresolved answers remain `UNKNOWN / PARTNER EVIDENCE REQUIRED` and link back to `P0-T1 / Issue #20`.

## Acceptance checklist for ABD-67

- [x] Current SECP consolidated AML/CFT/CPF baseline date verified as July 3, 2026.
- [x] Current Circular 03/2026 onboarding source verified.
- [x] Engineering obligations matrix created.
- [x] Regulated-person RACI created with fail-closed unknowns.
- [x] AI limited to non-authoritative assistance.
- [x] Partner-specific questions linked to direct evidence requirement.
- [ ] Exact clause-number/legal-text review completed against the authoritative July 3, 2026 consolidated PDF for every row.
- [ ] Direct broker/pyPSX responsibility evidence received and reconciled.
- [ ] Repository governance/acceptance allows merge to canonical main.

## Roadmap effect

This artifact advances non-live P0 operating-model preparation only. It does not satisfy P0-T1 while partner responsibility remains unverified, does not authorize production onboarding/PII, and does not unlock P1/P2/P3/P4.
