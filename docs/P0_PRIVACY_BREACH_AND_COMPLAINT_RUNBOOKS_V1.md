# P0 Privacy, Breach & Complaint Runbooks V1

Status: **Non-live operating runbooks. Partner-specific and launch-time legal allocations remain unverified until direct evidence and final legal review exist.**

## Purpose

Provide executable paper/tabletop workflows for privacy requests, data incidents and customer complaints without enabling production customer PII, live onboarding or real-money operation.

These runbooks inherit the fail-closed rules in the P0 privacy, onboarding/AML, investor-protection, retention and authorization baselines.

## Global rules

1. Every case receives an immutable internal case ID and status history.
2. Sensitive customer requests require identity verification and risk-appropriate step-up authentication.
3. Privacy deletion/account closure must not destroy records under regulatory retention, legal hold, active dispute, security investigation or financial audit requirements.
4. Security/fraud complaints open or link an incident workflow; a complaint does not replace incident response.
5. Financial truth is never edited to make a complaint disappear; remediation uses approved compensating/reversal workflows where applicable.
6. AI may summarize, classify or propose next steps, but may not independently suppress a reportable incident, clear AML concerns, authorize sensitive disclosure, release a hold, approve compensation or close a high-risk complaint.
7. Broker/pyPSX-specific escalation ownership remains `UNKNOWN / PARTNER EVIDENCE REQUIRED` until direct evidence is obtained.

---

# Runbook A — Privacy access/export request

## Intake

Capture:
- privacy request ID;
- customer/internal subject ID;
- request channel;
- requested scope;
- received timestamp;
- jurisdiction/residency indicators if policy requires them;
- current account/security state;
- related complaint/incident IDs if any.

Do not request unnecessary identity evidence merely to process the request.

## Verification

1. Authenticate the requester through an approved customer channel.
2. Apply step-up authentication for sensitive export.
3. If recovery/new-device/security-risk state is active, route to enhanced verification or `SECURITY_HOLD`.
4. Never send the export to a newly changed email/bank/contact destination solely because the requester changed it in the same session.

## Scope assembly

Potential categories:
- profile/contact data;
- identity/KYC references where disclosure is permitted;
- broker/account references;
- funding history;
- orders/executions;
- portfolio/mandate history;
- AI recommendation/decision history tied to the customer;
- privacy/consent records;
- complaint/case history;
- approved security/device/session information.

Exclude by default:
- internal security secrets;
- other customers' data;
- broker/provider secrets;
- privileged internal notes protected by law/security policy;
- data outside the request's authorized scope.

## Review and delivery

- privileged export generation must be audited;
- bulk/high-risk export requires second-level approval;
- package must be encrypted or delivered through an authenticated secure channel;
- delivery event and export hash/reference are recorded;
- temporary export artifacts follow a short explicit retention policy.

## Exit states

`FULFILLED | PARTIALLY_FULFILLED_WITH_REASON | IDENTITY_VERIFICATION_REQUIRED | LEGAL_REVIEW_REQUIRED | SECURITY_HOLD | REJECTED_WITH_REASON`

---

# Runbook B — Correction request

1. Receive correction target and asserted corrected value.
2. Verify identity and apply step-up if the field is security/financially sensitive.
3. Classify the field:
   - ordinary mutable profile field;
   - versioned legally relevant record;
   - external-provider/broker authoritative field;
   - immutable financial/audit fact.
4. Ordinary mutable fields may be updated under normal policy.
5. Versioned records create a new version; do not silently overwrite legally relevant history.
6. External authoritative fields require provider/broker reconciliation rather than local fabrication.
7. Immutable financial/audit records are not edited; approved correction/reversal/compensating mechanisms apply.
8. Record actor, reason, evidence, before/after hash or approved redacted values, approval and policy version.

---

# Runbook C — Account closure / deletion / anonymization request

## Step 1 — classify active obligations

Check for:
- open positions/orders/funding/withdrawals;
- unsettled financial activity;
- mandatory AML/KYC retention;
- securities transaction/audit retention;
- tax/reporting obligations;
- legal hold;
- complaint/dispute/arbitration;
- security/fraud investigation;
- regulator/law-enforcement preservation request.

## Step 2 — calculate disposition by data class

Use the canonical retention policy decision states:
- `DELETE_NOW`
- `ANONYMIZE_NOW`
- `RETAIN_UNTIL_DATE`
- `LEGAL_HOLD`
- `REGULATORY_RETENTION`

A single global "delete user" operation is prohibited.

## Step 3 — close product access safely

Where closure is allowed:
- disable new product activity;
- revoke sessions/tokens/devices as appropriate;
- revoke or expire mandates;
- terminate notification/marketing preferences subject to legal requirements;
- preserve immutable financial/audit history;
- schedule deletable/anonymizable data according to policy.

## Step 4 — provider propagation

If a provider is involved, record:
- provider request/reference;
- requested deletion/closure scope;
- contractual deletion result;
- backup/subprocessor handling where known.

Unknown provider deletion semantics remain `CONTRACT REVIEW REQUIRED`.

---

# Runbook D — Personal-data/security breach

## Trigger examples

- unauthorized Class-B/Class-C data access;
- exposed credentials/secrets;
- cross-customer retrieval;
- raw PII appearing in logs/analytics;
- lost/stolen privileged export;
- provider breach notification;
- compromised account/session leading to restricted-data access;
- model/tool leakage of another customer's data.

## Immediate containment

1. Open incident ID and severity.
2. Preserve immutable evidence before destructive cleanup where safe.
3. Disable/revoke affected sessions, credentials, API keys or provider integrations where necessary.
4. Isolate affected workload/data path.
5. Block continued export/sharing/processing if scope is unknown.
6. Engage security and privacy/compliance owners.

AI must not decide to suppress or downgrade notification obligations.

## Scope assessment

Record:
- affected data classes;
- affected customer/record count range;
- systems/providers involved;
- start/end or earliest/latest exposure window;
- actor/access path;
- encryption/tokenization state;
- data actually accessed vs potentially exposed;
- financial/security impact;
- whether secrets require rotation;
- whether regulated partners/providers are implicated.

## Evidence preservation

Preserve:
- audit events;
- authentication/session/device events;
- infrastructure/security logs after redaction controls;
- relevant deployment/config versions;
- provider references;
- access/export history;
- model/tool version and trace references for AI-related incidents.

Do not create a second uncontrolled copy of leaked PII as "evidence".

## Notification decision

Authorized legal/privacy/compliance owners decide:
- regulator notification;
- broker/partner notification;
- affected-customer notification;
- law-enforcement or other escalation;
- timing/content of notices.

Decision must record evidence, authority/source consulted, approver and timestamp.

Pakistan-wide privacy-law status and sectoral obligations require launch-time/legal recheck; do not infer a statute from non-authoritative material.

## Recovery

- rotate/revoke compromised credentials;
- patch root cause;
- verify access boundaries/redaction;
- restore services through controlled recovery;
- monitor for recurrence;
- complete post-incident root-cause and corrective-action review.

---

# Runbook E — Customer complaint / dispute

## Intake fields

At minimum:
- complaint ID;
- customer ID;
- affected broker/entity/account;
- category;
- severity;
- received timestamp/channel;
- assigned owner/team;
- related order/fill/ledger/AI/security/privacy references;
- external escalation/reference IDs;
- status history.

## Categories

Include:
- onboarding/KYC;
- funding/deposit;
- withdrawal;
- unauthorized/wrong/duplicate order allegation;
- price/execution dispute;
- position/ledger mismatch;
- fee/tax/statement dispute;
- AI recommendation/explanation complaint;
- Auto/mandate dispute;
- security/account takeover;
- privacy/data complaint;
- notification failure;
- broker/partner service;
- staff misconduct;
- accessibility/service.

## Triage

1. Classify severity and financial/security impact.
2. Security/fraud allegation -> open/link incident immediately.
3. Financial discrepancy -> link reconciliation evidence; do not edit ledger/execution history.
4. AI complaint -> preserve model ID/version, prompt/template version, structured evidence IDs, output hash, deterministic policy result, approval/mandate evidence and final execution evidence.
5. Privacy complaint -> link appropriate privacy/breach workflow.
6. Determine whether broker/PSX/SECP escalation guidance is applicable; partner-specific ownership remains unverified until direct evidence exists.

## Investigation evidence

Read-only correlated evidence may include:
- session/device/security events;
- mandate versions;
- order intents;
- broker orders;
- fills/executions;
- ledger journals;
- reconciliation results;
- market-data snapshot references;
- AI proposal/evidence/policy versions;
- compliance/risk decisions;
- notification attempts;
- admin/manual interventions.

Role-based redaction still applies during complaint investigation.

## Remediation

Prohibited:
- deleting or editing audit history;
- manually fabricating broker fills;
- editing ledger history to match the complaint outcome;
- changing a historical mandate to justify a past action;
- approving one's own high-risk remediation.

Financial remediation uses explicit compensating/reversal transactions or another approved domain mechanism with maker-checker approval and case linkage.

## Escalation states

`ESCALATED_BROKER | ESCALATED_PSX | ESCALATED_SECP | ARBITRATION_OR_FORMAL_DISPUTE | LEGAL_HOLD`

Exact broker/pyPSX escalation owner/contact/SLA: `UNKNOWN / PARTNER EVIDENCE REQUIRED`.

## Closure

High-severity closure requires:
- evidence summary;
- root-cause/disposition;
- remediation reference;
- required second-level approval;
- customer communication reference;
- external escalation status where applicable;
- retention/legal-hold classification.

Reopening creates new history; prior disposition is never overwritten.

---

# Tabletop scenarios required before Issue #8 production acceptance

1. Customer requests full data export after a recent account-recovery event.
2. Customer requests deletion while AML/financial retention applies.
3. Raw CNIC/IBAN appears in application logs.
4. External identity provider reports a breach.
5. AI retrieval exposes another customer's portfolio information.
6. Support agent attempts bulk export without second-level approval.
7. Customer alleges an unauthorized order while broker evidence is delayed.
8. Reconciliation break conflicts with the customer-visible timeline.
9. Security complaint and privacy complaint arise from the same account takeover.
10. Broker/partner escalation is required but partner ownership/SLA remains unknown.

Each tabletop must produce:
- timeline;
- decision log;
- roles involved;
- evidence references;
- control gaps;
- corrective actions;
- pass/fail outcome.

# Acceptance for this document

Document-level design acceptance requires:
- privacy access/export workflow defined;
- correction workflow defined;
- closure/deletion workflow preserves mandatory records;
- breach containment/evidence/notification decision workflow defined;
- complaint/dispute workflow defined with immutable evidence and maker-checker remediation;
- security/fraud complaint triggers incident response;
- AI cannot independently suppress/close high-risk cases;
- partner/regulatory unknowns remain explicit;
- tabletop catalogue exists.

Production Issue #8 acceptance still additionally requires direct partner/legal evidence, implemented/tested controls, successful table tops and canonical acceptance. This document does not authorize live onboarding, production PII, real money or downstream phase advancement.
