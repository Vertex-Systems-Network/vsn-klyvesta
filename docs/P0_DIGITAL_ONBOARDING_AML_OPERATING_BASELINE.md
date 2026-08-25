# P0 Digital Onboarding & AML Operating Baseline

Status: **P0 design baseline; regulated-person allocation and provider contracts remain unverified.**

## Purpose

Define the minimum Klyvesta architecture and due-diligence requirements for digital investor onboarding, identity proofing, KYC/AML/CFT/CPF, bank/payment-account verification and evidence retention without assuming pyPSX or any third party contract semantics.

## Current regulatory evidence (as of 2026-08-25)

- SECP lists **Circular 03 of 2026 — Digital Onboarding of Investors through FI or 3rd Parties via API Integration**, dated 2026-01-29.
- SECP lists the **AML/CFT/CPF Regulations, 2020 as amended up to 2026-07-03** as the current published AML regulations baseline.
- SECP's 2026-04-24 press release proposed stronger digital-onboarding controls including IBAN-based verification, verified bank accounts/e-wallets and multi-biometric verification, while explicitly stating that regulated persons remain responsible for KYC, due diligence, transaction monitoring and AML compliance.
- Because the April item was a proposal and the July regulation text has not yet been mapped line-by-line into this repository, Klyvesta must **not assume** that every proposed mechanism is final law. Final obligations must be verified against the current regulation text and partner compliance position before production.

## Core architecture rule

Klyvesta may orchestrate onboarding, but it must never silently become the legal KYC/AML authority by implementation accident.

Every onboarding deployment must identify:

- the regulated person legally responsible for KYC/AML;
- identity-verification provider(s);
- source of bank/IBAN/e-wallet verification;
- sanctions/PEP/adverse-risk data provider(s);
- broker account authority;
- UIN/CDC/NCCPL responsibility;
- records custodian and retention responsibility;
- escalation/STR/SAR reporting responsibility where applicable.

Unknown responsibility => account remains `ONBOARDING_RESTRICTED` / `COMPLIANCE_HOLD`.

## Onboarding state model

```text
CREATED
  -> CONTACT_VERIFICATION_PENDING
  -> IDENTITY_PROOFING_PENDING
  -> KYC_DATA_PENDING
  -> AML_SCREENING_PENDING
  -> BANK_OR_PAYMENT_ACCOUNT_VERIFICATION_PENDING
  -> BROKER_ONBOARDING_PENDING
  -> REVIEW_PENDING? 
  -> ACTIVE
```

Terminal/restricted states:

```text
REJECTED
COMPLIANCE_HOLD
SECURITY_HOLD
EXPIRED
CANCELLED
RESTRICTED
```

No trading/funding authority is inferred from a partially completed state.

## Evidence model

For every material onboarding assertion store an immutable evidence record containing at minimum:

- internal customer ID;
- evidence type;
- provider/source;
- provider reference (tokenized/redacted where necessary);
- request/correlation ID;
- observed timestamp;
- provider timestamp if available;
- result/status;
- schema/version;
- verification method;
- expiry/revalidation date where applicable;
- reviewer/approver when human review occurred;
- hash/reference to retained evidence, not uncontrolled raw copies;
- policy version that accepted/rejected the evidence.

Never store biometric templates or identity-provider secrets merely because an API returned them. Retain only what is legally/operationally required.

## Identity and account controls

Minimum design requirements:

1. Identity proofing must be independent from login authentication. Passing KYC does not create a trusted session.
2. Login authentication must be phishing-resistant where practical (passkeys preferred) and support step-up authentication.
3. New device, credential recovery, email/phone change or identity re-verification may downgrade account security state and restrict withdrawals/high-risk changes.
4. Verified bank/IBAN/e-wallet ownership must be represented separately from customer identity status.
5. New withdrawal beneficiary/payment account must not become immediately usable without policy-defined verification/cooling-off controls.
6. KYC evidence must be re-evaluable if identity data changes or compliance rules/providers change.
7. Manual reviewer access to raw identity evidence must be least-privilege, purpose-limited and audited.

## AML / financial-crime controls

The regulated operating model must support, as applicable:

- customer identification and verification;
- beneficial-owner identification for relevant account types;
- risk categorization;
- PEP/sanctions/TFS screening;
- source-of-funds/source-of-wealth evidence when required;
- ongoing transaction/activity monitoring;
- re-screening after material profile changes;
- alert review and escalation;
- account restriction/freeze paths;
- regulator/law-enforcement evidence preservation;
- false-positive handling without weakening screening rules.

Klyvesta AI/LLM output must never clear an AML alert by itself. AI may assist triage/summarization, but a deterministic/compliance-authorized workflow owns final disposition.

## Record retention

Current SECP AML guidance historically requires customer/transaction records to be retained through the relationship and generally for at least five years after relationship/transaction completion, with longer retention for litigation or competent-authority requirements. Production retention must be mapped to the **current July 2026 regulations and partner legal schedule** before activation.

Retention must be expressed as a machine-readable policy by data class, not a single global `delete_after` value.

## Provider/API security

External onboarding/KYC providers are untrusted network dependencies.

Required controls:

- server-to-server credentials only; never ship provider secrets in web/mobile/desktop clients;
- scoped credentials per environment and capability where supported;
- TLS plus stronger sender-constraining/mTLS where provider supports it;
- signed/authenticated callbacks/webhooks;
- replay protection and event de-duplication;
- strict schema validation and body-size limits;
- request/response correlation IDs;
- timeout classification;
- provider circuit breaker;
- no blind retries for side-effecting operations;
- redact CNIC/biometric/bank data from logs;
- provider outage must fail closed for actions requiring fresh verification.

## Funding linkage

Customer funding architecture must enforce:

```text
verified customer identity
+ verified/approved funding account
+ active broker/account status
+ compliance state OK
+ reconciliation evidence
= investable cash eligibility
```

A payment/provider `success` response alone must not create authoritative investable cash.

## Admin privilege controls

No single support/admin role may both:

- alter identity/KYC data and approve it;
- add a withdrawal beneficiary and approve withdrawal;
- dismiss AML risk and release restricted funds;
- modify retained evidence and approve the same case.

Use maker-checker/dual-control for high-risk manual interventions and append-only audit records.

## P0 acceptance questions

Before live onboarding, obtain authoritative answers to:

1. Who is the regulated person responsible for KYC/AML?
2. Does pyPSX orchestrate KYC or only transport data?
3. Which broker/legal entity owns final customer acceptance/rejection?
4. Which identity/biometric mechanisms are mandatory versus optional?
5. How are IBAN/bank/e-wallet ownership checks performed?
6. What provider evidence must Klyvesta retain?
7. What data is Klyvesta prohibited from retaining?
8. What are re-verification triggers and expiries?
9. Which sanctions/PEP/TFS sources are authoritative?
10. Who performs ongoing transaction monitoring?
11. Who files regulatory reports/escalations where required?
12. What are account freeze/restriction semantics?
13. What are retention periods by record type?
14. What are API/webhook authentication and replay guarantees?
15. What is the outage/manual-review fallback?

## Production blockers

Do not enable live onboarding/funding/trading until:

- regulated-person responsibility is documented;
- current AML regulation mapping is approved;
- provider contracts/security semantics are verified;
- identity/bank verification evidence model is tested;
- negative/timeout/replay cases pass;
- privacy/retention schedule is approved;
- incident and manual-review runbooks exist.

## Primary sources reviewed

- SECP Circular 03 of 2026 — Digital Onboarding of Investors through FI or 3rd Parties via API Integration (2026-01-29).
- SECP AML/CFT/CPF Regulations, 2020 as amended up to 2026-07-03.
- SECP press release dated 2026-04-24 on proposed IBAN/biometric digital-onboarding enhancements.
