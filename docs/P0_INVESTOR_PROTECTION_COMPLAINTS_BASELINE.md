# P0 Investor Protection & Complaints Baseline

Status: **P0 operating baseline; broker-specific complaint allocation remains unverified.**

## Purpose

Define Klyvesta's minimum customer-protection, complaints, dispute-evidence and escalation architecture for a regulated investment platform.

## Current public framework (as of 2026-08-25)

PSX states that investor grievances concerning securities trading can be taken to its Regulatory Affairs Division and, where unresolved amicably, may proceed through mediation/arbitration under PSX regulations. SECP also provides an investor complaint channel and advises investors to first raise the issue with the relevant broker/exchange/entity before escalation.

Therefore Klyvesta must not build a closed complaint system that traps or suppresses broker/exchange/regulatory escalation.

## Complaint ownership model

Every complaint must have:

- complaint ID;
- customer ID;
- affected regulated entity/broker/account;
- category;
- severity;
- received timestamp;
- channel;
- assigned owner/team;
- regulatory/escalation timer where applicable;
- linked order/fill/ledger/notification/evidence references;
- status history;
- customer-visible updates;
- final disposition;
- external escalation/reference IDs;
- audit trail.

## Categories

At minimum:

- onboarding/KYC delay or rejection;
- deposit/funding mismatch;
- withdrawal delay/rejection;
- unauthorized order allegation;
- wrong/duplicate order allegation;
- price/execution dispute;
- missing/incorrect position;
- ledger/cash mismatch;
- fee/tax dispute;
- statement/report discrepancy;
- AI recommendation/explanation complaint;
- Auto/mandate dispute;
- security/account-takeover complaint;
- privacy/data complaint;
- notification failure;
- broker/partner service complaint;
- staff misconduct;
- accessibility/service complaint.

Security/fraud incidents must also trigger the incident-response workflow; creating a complaint must not replace incident handling.

## State model

```text
RECEIVED
 -> TRIAGED
 -> INVESTIGATING
 -> WAITING_ON_CUSTOMER? 
 -> WAITING_ON_PARTNER? 
 -> REMEDIATION_PENDING?
 -> RESOLVED
 -> CLOSED
```

Escalation states:

```text
ESCALATED_BROKER
ESCALATED_PSX
ESCALATED_SECP
ARBITRATION_OR_FORMAL_DISPUTE
LEGAL_HOLD
```

Reopening must preserve the original history; never overwrite/delete prior disposition.

## Evidence integrity

Complaint investigators need read-only correlated evidence across:

- authentication/session events;
- device/security events;
- customer mandate versions;
- order intents;
- broker orders;
- fills/executions;
- ledger journals;
- reconciliation results;
- market-data snapshots used for decisions;
- AI proposal/evidence/policy versions;
- risk/compliance decisions;
- notification delivery attempts;
- admin/manual interventions.

Evidence presented to staff should be redacted by role. Complaint staff do not automatically receive broad PII, broker credentials or secret access.

## Customer timeline integrity

The user-facing investment timeline is also a dispute-protection surface.

Requirements:

- distinguish `requested`, `approved`, `submitted`, `filled`, `settled`, `reversed`;
- never show `filled` from an order acknowledgement;
- never fabricate a reason after the fact;
- display corrections/reversals as new entries rather than silently editing history;
- material automated actions link to mandate/risk reason and evidence version;
- timestamps include consistent timezone presentation and canonical UTC storage.

## AI-specific complaint protections

If a complaint concerns AI:

- preserve exact model ID/version;
- prompt/template version;
- structured input evidence IDs;
- AI output hash;
- deterministic policy result;
- user approval/mandate evidence;
- final execution evidence.

The complaint workflow must be able to demonstrate that AI did not directly execute or bypass risk/compliance.

## Service design

The platform should provide:

- in-app complaint creation;
- complaint status tracking;
- secure document upload when needed;
- support email/contact path;
- accessible non-chat-only escalation option;
- regulator/broker escalation guidance where legally required;
- downloadable case summary where appropriate.

Do not make complaint filing conditional on accepting new terms, waiving rights, deleting negative reviews or chatting only with an AI bot.

## Internal privileges

Support agents may investigate but must not be able to silently alter financial truth.

Forbidden single-role powers:

- modify ledger history;
- fabricate/correct a broker fill manually;
- delete audit evidence;
- approve own financial remediation;
- change mandate to justify a past action;
- close high-severity security complaint without required second-level review.

Financial remediation uses compensating/reversal transactions with maker-checker approval and explicit case linkage.

## Monitoring and metrics

Track without exposing customer PII:

- complaint volume/rate;
- category distribution;
- first-response time;
- resolution time;
- reopened cases;
- partner-related cases;
- unauthorized-order allegations;
- reconciliation-related complaints;
- AI-related complaints;
- security/fraud complaints;
- regulatory escalations;
- repeat root causes.

Repeated complaint patterns must feed risk/product/security review, not only customer support dashboards.

## Acceptance requirements

Before production:

1. Broker/pyPSX complaint ownership and escalation path is documented.
2. PSX/SECP escalation information is legally reviewed and user-accessible.
3. Complaint evidence can reconstruct an order/portfolio/AI decision end to end.
4. Support/admin authorization tests prove financial records cannot be silently edited.
5. Complaint records have retention/legal-hold rules.
6. Security complaints trigger incident workflow automatically.
7. Notification failures cannot hide a financial action from the in-app immutable timeline.
8. High-severity complaints have maker-checker escalation.

## Primary sources reviewed

- Pakistan Stock Exchange investor resources / Investors' Complaints and arbitration information.
- SECP Complaints / Investor Guidelines for Lodging a Complaint.
