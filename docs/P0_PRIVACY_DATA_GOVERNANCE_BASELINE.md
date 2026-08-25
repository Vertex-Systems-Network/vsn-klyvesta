# P0 Privacy & Data Governance Baseline

Status: **Engineering/privacy baseline; Pakistan-wide personal-data legislation status must be rechecked by counsel before launch.**

## Current legal-status caution (as of 2026-08-25)

Public National Assembly passed-bills listings reviewed for the current parliamentary year do not show a passed Personal Data Protection Bill/Act. The Senate's 2023 Personal Data Protection Bill record shows that bill was not passed and was disposed/withdrawn in committee. Other government material references a finalized or proposed data-protection framework, but Klyvesta must not treat that wording as proof that a federal Personal Data Protection Act is currently in force.

This is not legal advice. Before production, counsel must recheck Gazette/Pakistan Code/Parliament and any sectoral SECP/PSX/NCCPL/CDC obligations.

## Engineering position

Klyvesta will implement a strong privacy baseline even where statutory status is uncertain.

Principles:

- data minimization;
- purpose limitation;
- least privilege;
- explicit data classification;
- encryption in transit and at rest;
- field-level protection for high-risk PII;
- controlled retention/deletion;
- auditable access;
- cross-border/vendor review;
- privacy by default;
- no hidden AI-training reuse of customer data.

## Data classification

### Class A — Secrets / execution credentials

Examples:
- broker API secrets;
- signing/private keys;
- KMS/HSM material;
- production database credentials.

Rules:
- never exposed to clients or LLMs;
- never logged;
- vault/KMS/HSM-backed;
- strict rotation and access audit.

### Class B — Restricted identity/financial PII

Examples:
- CNIC/passport data;
- biometric verification evidence;
- bank/IBAN/e-wallet data;
- addresses/contact details;
- KYC/AML risk records;
- tax/regulatory identifiers.

Rules:
- purpose-specific access;
- encryption/tokenization/field protection;
- redacted staff views;
- no analytics warehouse copying by default;
- strict export controls.

### Class C — Confidential financial/account data

Examples:
- portfolio holdings;
- cash/ledger history;
- orders/fills;
- risk profile;
- mandates;
- AI recommendations tied to identity.

Rules:
- resource ownership/ABAC;
- encrypted storage;
- audit all privileged access;
- no public/shareable URL defaults.

### Class D — Operational telemetry

Examples:
- request IDs;
- latency;
- service health;
- redacted error codes.

Rules:
- avoid unnecessary identity linkage;
- short retention where practical;
- no secrets/financial payload dumps.

### Class E — Public/reference data

Examples:
- approved public market/reference content;
- public product documentation.

Still respect licensing/redistribution rights.

## PII vault boundary

High-risk identity data should be isolated behind a dedicated PII access layer/service/module.

Domain services receive minimum required attributes or opaque references, not full customer identity documents.

Example:

```text
Trading domain needs:
customer_id
account_status
compliance_status
country/residency flags if required

It does NOT need:
full CNIC image
selfie image
raw biometric data
```

## Retention model

Retention is defined per data class and legal purpose.

Do not implement a single `delete user` operation that destroys records needed for:

- AML/KYC retention;
- securities transaction/audit records;
- legal hold;
- dispute/arbitration;
- tax/reporting;
- security investigations.

A privacy deletion/erasure request must produce a policy decision such as:

```text
DELETE_NOW
ANONYMIZE_NOW
RETAIN_UNTIL_DATE
LEGAL_HOLD
REGULATORY_RETENTION
```

with reason and policy version.

## User rights workflow baseline

Even before final statutory mapping, provide controlled processes for:

- access/export request;
- correction request;
- contact/preference change;
- consent/communication preferences;
- account closure;
- deletion/anonymization request subject to legal retention;
- complaint/escalation.

Identity verification and step-up authentication are required for sensitive privacy requests.

## AI privacy rules

Default policy:

- customer conversations, KYC, holdings and financial history are **not** used to train external/general models unless a separately approved legal/product process explicitly authorizes it;
- send minimum necessary context to model providers;
- redact/tokenize direct identifiers where possible;
- no broker credentials or secret configuration in prompts;
- model provider retention/training settings must be contractually reviewed;
- sensitive AI traces have retention limits and role-based access;
- user-facing AI must not reveal another customer's data through retrieval/tool bugs.

## Analytics

Prefer privacy-preserving event schemas:

```text
user_id -> opaque analytics subject ID
portfolio_value -> bucket/derived metric where exact value not needed
CNIC/email/phone -> never analytics dimensions
```

Production analytics access is separate from operational/customer-support privileges.

## Cross-border and vendors

Before using any external SaaS/model/cloud provider for customer data, record:

- data categories;
- countries/regions;
- subprocessors;
- encryption;
- retention;
- deletion controls;
- incident notification terms;
- government/law-enforcement request policy where relevant;
- AI training/use terms;
- portability/termination plan.

Do not infer that a vendor's generic privacy policy is sufficient for regulated financial data.

## Logging/observability

Forbidden in application logs:

- full CNIC/passport;
- passwords/OTP/passkeys;
- access/refresh tokens;
- bank account/IBAN in full;
- broker credentials;
- raw KYC documents/biometric images;
- complete order/payment request bodies when they contain restricted data.

Use structured redaction and automated secret/PII tests.

## Data breach / privacy incident

Incident workflow must support:

- containment;
- credential/key rotation;
- impacted-data classification;
- affected-customer identification;
- immutable forensic evidence;
- provider/partner coordination;
- regulatory/legal notification decision;
- customer communication decision;
- post-incident root-cause/remediation.

Never let an AI agent independently decide whether a legally reportable incident should be suppressed.

## Production acceptance gates

Before live customer data:

1. current Pakistan privacy-law status rechecked by counsel;
2. sectoral retention requirements mapped;
3. data inventory/data-flow map approved;
4. processor/subprocessor register exists;
5. retention schedule implemented/tested;
6. privileged PII access audit works;
7. deletion/closure preserves legally required records;
8. backup deletion/retention behavior documented;
9. AI/model-provider data handling contract reviewed;
10. breach runbook/tabletop completed;
11. export/correction/account-closure workflows tested;
12. secrets/PII logging tests pass.

## Primary sources reviewed

- National Assembly current passed-bills listings (reviewed 2026-08-25).
- Senate record for the Personal Data Protection Bill, 2023.
- MoITT public material on draft/finalization work for personal-data protection legislation.
