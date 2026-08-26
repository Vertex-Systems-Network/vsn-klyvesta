# P0 Data Inventory & PII Boundary V1

Status: **Draft P0 non-live operating artifact for ABD-66 / GitHub Issue #8.** This document does not authorize live onboarding, production PII processing, real-money trading, or any downstream phase transition.

## Purpose

Define Klyvesta's concrete data inventory, classification, storage/ownership boundaries, minimum-purpose access model, PII vault boundary, logging/analytics prohibitions, and known external-provider unknowns before production customer data exists.

Canonical inputs:

- `docs/P0_PRIVACY_DATA_GOVERNANCE_BASELINE.md`
- `docs/P0_DIGITAL_ONBOARDING_AML_OPERATING_BASELINE.md`
- `docs/DOMAIN_DATABASE_MODEL_V1.md`
- `docs/AUTHORIZATION_PRIVILEGE_MODEL.md`
- GitHub Issue #8

## Governing rules

1. Data minimization and purpose limitation apply by default.
2. Authentication does not grant data access; authorization is deny-by-default and resource-scoped.
3. High-risk identity data is isolated behind a dedicated PII access boundary.
4. Domain services receive opaque references or minimum derived attributes instead of raw identity evidence whenever possible.
5. Secrets, credentials, raw biometric/KYC artifacts, full bank identifiers and other restricted fields must never enter logs, analytics, prompts, model traces or notification payloads by default.
6. Unknown broker/provider semantics remain `UNKNOWN / CONTRACT REQUIRED`; public marketing material is not contract evidence.
7. Historical financial/audit facts are append-only; privacy deletion does not destroy legally required records.

## Classification inventory

### Class A — Secrets / execution credentials

Records and examples:

- broker API credentials, signing/private keys, KMS/HSM references;
- production database credentials and workload identities;
- provider API secrets, webhook signing secrets, OAuth/client credentials;
- session/token signing material and recovery-security secrets.

Primary boundaries:

- secret manager / vault / KMS / HSM only;
- never persisted in customer/domain payload tables;
- never readable by browser/mobile/desktop clients or AI/LLM contexts;
- access only by narrowly scoped machine principals or approved break-glass workflows.

Allowed consumers:

- Broker Adapter for broker credentials;
- designated server-side integration services for provider credentials;
- deployment/runtime platform via workload identity.

Forbidden consumers:

- UI clients, analytics, notification service, support staff, AI agents, ordinary application logs.

### Class B — Restricted identity / financial PII

Records and examples:

- CNIC/passport identifiers and document references;
- identity/biometric verification evidence and provider case references;
- customer address/contact methods;
- KYC/AML case data, sanctions/PEP/TFS evidence, risk/compliance records;
- bank/IBAN/e-wallet/funding identifiers and beneficiary evidence;
- tax/regulatory identifiers;
- sensitive complaint/privacy-request evidence.

Primary schema ownership:

- `customer` — contact, address, KYC references, profile/consent evidence;
- `funding` — tokenized funding/beneficiary references;
- dedicated PII vault/access layer — raw or high-risk retained identity evidence where retention is justified;
- `audit` — redacted references/hashes only, never uncontrolled raw identity payloads.

Access rule:

- purpose-specific, least-privilege, auditable;
- Support L1/L2 receive redacted case context only;
- KYC/AML staff receive only evidence required for assigned case scope;
- security staff receive identity/portfolio detail only where incident need is documented;
- analytics has no direct Class B access by default.

### Class C — Confidential financial / account data

Records and examples:

- broker accounts and external account references;
- portfolios, holdings, mandates, suitability/risk profiles;
- deposits, withdrawals, beneficiaries, funding state;
- order intents, broker orders, executions/fills;
- ledger accounts, journals, reservations;
- reconciliation runs/breaks;
- AI recommendations tied to an identified customer;
- customer timeline and financial notifications.

Primary schema ownership:

- `broker`, `funding`, `ledger`, `trading`, `portfolio`, `ai`, `notification`, `ops`.

Access rule:

- customer principals: own resources only;
- internal roles: explicit case/function scope;
- no support/admin wildcard access;
- auditor access is read-only and scope-limited;
- AI principals receive approved summaries/evidence references only and never broker credentials or direct financial mutation authority.

### Class D — Operational telemetry

Records and examples:

- request/correlation IDs;
- latency/service-health metrics;
- redacted failure/retry codes;
- deployment/version metadata;
- provider uptime/circuit-breaker state;
- non-sensitive security telemetry.

Rules:

- avoid identity linkage unless operationally necessary;
- use opaque subject IDs when correlation is required;
- short retention where practical;
- no request/response body dumps containing Class A/B/C data.

### Class E — Public / reference data

Records and examples:

- approved public product documentation;
- instrument/reference metadata;
- approved/licensed market/reference content where rights permit;
- public policy/help content.

Rules:

- public classification does not remove licensing/redistribution constraints;
- public/reference data must not be silently promoted to authoritative broker or execution state.

## Domain-to-data inventory

| Domain / schema | Main records | Highest class | Raw PII permitted? | Minimum consumer rule |
| --- | --- | --- | --- | --- |
| `identity` | app user refs, devices, security sessions, recovery state | B | No raw KYC docs | auth/security services only; session handles stored hashed |
| `customer` | customer, contacts, address, KYC refs, suitability/risk, consent | B | Only where justified through PII boundary | customer/compliance services; redacted staff views |
| `broker` | broker account refs, external IDs, sync state | C/B | No identity document copies | broker/account services; customer-scoped reads |
| `funding` | funding account refs, deposits, beneficiaries, withdrawals | B/C | No uncontrolled full bank artifacts | funding/compliance/reconciliation workflows |
| `ledger` | accounts, journals, lines, reservations | C | No Class B payloads | financial core/reconciliation; append-only after posting |
| `market` | instruments, sessions, approved snapshots | E/C | No | market/risk/trading services under data-rights policy |
| `trading` | order intents, checks, broker orders, fills | C | No | execution/risk/compliance; own-customer reads only |
| `portfolio` | portfolios, mandates, allocations, snapshots | C | No | portfolio/risk/customer services |
| `ai` | model versions, decision runs, evidence refs, recommendations/proposals | C | No raw KYC/biometric/bank secrets | minimum approved context; no broker execution authority |
| `notification` | timeline, outbox, delivery metadata | C/D | Sanitized payload only | notification service; financial truth remains elsewhere |
| `ops` | reconciliation, incidents, feature controls | C/D | Case-linked redacted refs only | SRE/security/reconciliation by scope |
| `audit` | immutable privileged/compliance/security evidence | B/C | Redacted values/hashes only | auditors/security/compliance read-only by scope |

## Dedicated PII vault boundary

The PII vault/access layer is the only approved boundary for retained high-risk identity evidence when retention is legally/operationally required.

It may contain or reference:

- identity-document evidence;
- biometric-verification evidence/reference;
- sensitive KYC provider evidence;
- verified bank/account ownership evidence where raw retention is required.

It must expose minimum-purpose derived attributes or opaque references such as:

- `customer_id`;
- `identity_verification_status`;
- `kyc_case_status`;
- `compliance_status`;
- `verification_evidence_id`;
- `verified_funding_account_id`;
- `country/residency flags` where required;
- `reverification_required` / `evidence_expiry`.

It must not expose raw identity images, biometric material or unrestricted document payloads to trading, portfolio, AI, notification, analytics or support workflows.

## Minimum-data contracts by service

### Trading / execution

May require:

- customer/account/portfolio opaque IDs;
- account active/restricted status;
- compliance/risk decision result;
- verified funding eligibility result;
- mandate/profile version references.

Must not require:

- full CNIC/passport;
- selfie/biometric evidence;
- raw KYC documents;
- full bank statement/account evidence.

### Portfolio / risk

May require:

- customer/portfolio opaque IDs;
- approved risk/suitability classification and version;
- holdings/cash/mandate constraints.

Must not require raw identity evidence.

### AI / model workflows

May require only approved minimum context such as:

- portfolio/risk summary;
- approved market/reference data;
- structured evidence IDs;
- policy decisions and non-secret constraints.

Prohibited:

- broker credentials;
- raw CNIC/passport/biometric data;
- full bank/IBAN/e-wallet identifiers;
- unnecessary contact/address data;
- hidden reuse of customer conversations, holdings or KYC for external/general model training.

### Notifications

May receive sanitized event projections and approved contact channel references.

Prohibited:

- credentials/secrets;
- raw KYC/biometric evidence;
- full financial request/response payloads;
- unrestricted complaint/security evidence.

## Staff view policy

### Support L1/L2

Visible by default:

- customer opaque ID;
- redacted contact data;
- account/service status;
- case references and sanitized reason codes.

Not visible by default:

- full CNIC/passport;
- raw KYC or biometric evidence;
- broker/provider secrets;
- full bank/IBAN;
- unrestricted portfolio/ledger history.

### KYC / AML / Compliance

Access is case-scoped, purpose-limited and audited. High-risk evidence views require explicit role + case association and may require step-up authentication.

### Security / SRE

Security telemetry is preferred over customer PII. Production PII access is exceptional, time-limited, incident-linked and auditable. SRE has no standing Class B/C business-data access.

### Auditor

Read-only access to approved evidence scope. No secrets and no mutation authority.

## Logging and analytics deny-list

The following are forbidden in application logs, telemetry dimensions, analytics events, exception dumps, support traces and model prompts/traces unless a separately approved controlled evidence path explicitly requires a protected reference:

- passwords, OTP values, passkeys/private material;
- access/refresh/session tokens;
- broker/provider credentials and signing secrets;
- full CNIC/passport numbers or document images;
- raw biometric templates/images/evidence payloads;
- full bank account / IBAN / e-wallet identifiers;
- raw KYC documents;
- complete payment/order/funding request bodies containing restricted data;
- unrestricted holdings/ledger payloads where aggregated/opaque values suffice.

Analytics should use opaque subject IDs and derived/bucketed metrics where exact values are unnecessary.

## External-provider unknowns

The following remain fail-closed until direct current contract evidence exists:

- pyPSX/broker KYC orchestration responsibilities;
- exact identity/biometric provider(s);
- mandatory versus optional verification methods;
- IBAN/bank/e-wallet verification provider and authoritative ownership check;
- sanctions/PEP/TFS sources and allocation of monitoring responsibility;
- evidence-retention/prohibited-retention requirements by provider;
- external field lengths/formats and broker account identifiers;
- webhook/authentication/replay guarantees;
- partner retention/deletion schedules;
- processor/subprocessor regions and contract terms.

Unknown values must be represented as `UNKNOWN / PARTNER EVIDENCE REQUIRED` or `UNKNOWN / CONTRACT REQUIRED`; they must not be inferred from public claims.

## Acceptance checklist for ABD-66

- [x] Classes A–E mapped to concrete Klyvesta records.
- [x] Domain/schema ownership identified for known internal data.
- [x] High-risk PII vault/access boundary defined.
- [x] Trading/portfolio/AI/notification minimum-data contracts defined.
- [x] Staff/support redaction baseline defined.
- [x] Logging and analytics prohibited-field list defined.
- [x] External provider/partner unknowns explicitly fail-closed.
- [ ] Review against any newer direct partner evidence if/when received.
- [ ] Merge/accept through repository governance after live protected-main controls exist or a new explicit owner risk decision is recorded.

## Roadmap effect

This artifact advances only the non-live Issue #8 operating-model work. It does not satisfy P0-T1, does not remove the pyPSX partner-evidence blocker, does not authorize live onboarding/PII, and does not unlock P1/P2/P3/P4.
