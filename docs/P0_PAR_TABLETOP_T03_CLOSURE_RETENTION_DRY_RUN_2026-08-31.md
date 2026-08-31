# P0-PAR Tabletop T-03 — Account Closure vs Mandatory Retention

Status: **SYNTHETIC DRY RUN — BLOCKED / NOT PRODUCTION AUTHORITY**

Linear parent gate: `ABD-73`
GitHub gate: Issue #8
Repository base: `093a144593cd41954e810dc54f410d27dc3f25b2`
Scenario source: `docs/P0_PAR_PRODUCTION_ACCEPTANCE_EVIDENCE_PLAN.md` — T-03
Runbook source: `docs/P0_PRIVACY_BREACH_AND_COMPLAINT_RUNBOOKS_V1.md` — Runbook C
Data: **synthetic only**
Production-authorizing: **false**

This record exercises the paper/control contract only. It is not legal advice, does not process a real customer, does not execute deletion against a production database/provider, and does not establish broker/pyPSX, provider, legal or real-money authority.

## 1. Synthetic inject

Assume a synthetic customer requests full account closure and deletion while the following synthetic obligations exist:

- one historical KYC/identity record classified for mandatory retention;
- one historical financial/order/ledger record classified for mandatory retention;
- ordinary mutable profile/preferences that are otherwise eligible for deletion/anonymization;
- an active synthetic legal hold covering one complaint-related evidence set;
- one hypothetical external provider copy whose contractual deletion/backup semantics have not been selected or verified;
- no live positions, pending orders or real-money activity.

No real CNIC/passport/biometric/bank data, credentials, provider secrets or funds are used.

## 2. Expected decision path from accepted runbook

The accepted non-live runbook requires closure to classify active obligations before data disposition. A single global `delete user` operation is prohibited.

Expected per-class outcomes are:

| Synthetic data class | Expected disposition | Reason |
| --- | --- | --- |
| ordinary profile/preference data | `DELETE_NOW` or `ANONYMIZE_NOW` when no independent retention basis exists | minimize retained customer data |
| KYC/identity record under mandatory retention | `REGULATORY_RETENTION` / `RETAIN_UNTIL_DATE` | closure must not destroy mandatory records |
| financial/order/ledger history under mandatory retention | `REGULATORY_RETENTION` / `RETAIN_UNTIL_DATE` | immutable financial truth must remain preserved |
| complaint evidence inside active hold | `LEGAL_HOLD` | hold overrides ordinary deletion only within defined scope |
| provider-held copy | `BLOCKED / CONTRACT REVIEW REQUIRED` until selected-provider deletion/backup semantics are authoritative | provider behavior may not be inferred |

Product access should be disabled independently from mandatory record retention. Retained records must not remain available for ordinary product use merely because they are legally preserved.

## 3. Paper-control observations

### O-01 — selective disposition is documented

**Result: DOCUMENTED / NON-LIVE**

The runbook explicitly distinguishes `DELETE_NOW`, `ANONYMIZE_NOW`, `RETAIN_UNTIL_DATE`, `LEGAL_HOLD` and `REGULATORY_RETENTION`, and prohibits one global delete operation.

### O-02 — closure and access revocation are separated from retention

**Result: DOCUMENTED / NON-LIVE**

The runbook requires safe product closure/session-token-device handling while immutable financial/audit history is preserved independently.

### O-03 — legal hold precedence is documented

**Result: DOCUMENTED / NON-LIVE**

Legal hold is an explicit blocker to ordinary deletion for the covered evidence scope. The dry run does not prove runtime hold enforcement or prevent an implementation bug from deleting held data.

### O-04 — provider propagation remains unresolved

**Result: BLOCKED**

No selected production provider/contract exists in this exercise. Provider deletion scope, backup retention, subprocessors, termination handling and deletion confirmation therefore remain UNKNOWN / CONTRACT REVIEW REQUIRED.

### O-05 — runtime selective deletion/anonymization is unproven

**Result: BLOCKED**

This documentation-only exercise did not execute a real deletion/anonymization engine, database retention scheduler, legal-hold guard or provider propagation workflow. No exact runtime commit/test/environment evidence exists for those controls.

### O-06 — mandatory-retention dates/legal bases remain launch-time evidence

**Result: BLOCKED**

The runbook provides decision states but this exercise does not establish the operative statutory/sectoral retention period for any real production record. Exact periods and legal bases require current authoritative law/sector rules, partner evidence where applicable and competent legal/compliance review.

### O-07 — broker/pyPSX responsibility remains unresolved

**Result: BLOCKED**

`ABD-7 / P0-T1` remains the authority for broker/pyPSX responsibilities and record ownership. This dry run does not infer which party owns deletion/retention execution or customer communication.

### O-08 — repository-governance production provenance remains unresolved

**Result: BLOCKED**

Hosted protected-main enforcement remains unresolved under `ABD-44 / GitHub Issue #1`. Documentation CI does not substitute for that production evidence gate.

## 4. Adversarial checks performed on the paper contract

| Adversarial case | Expected paper response | Dry-run result |
| --- | --- | --- |
| request asks to delete every historical record | reject global-delete behavior; classify each record | documented |
| ordinary preference data has no retention basis | delete/anonymize rather than retain indefinitely | documented |
| legal hold overlaps otherwise deletable data | hold wins only for covered scope | documented, runtime unproven |
| mandatory financial truth conflicts with closure request | preserve immutable record; disable ordinary product access separately | documented, runtime unproven |
| provider deletion semantics are unknown | fail closed / contract review required | documented BLOCKED |
| actor tries to release legal hold solely to finish closure | prohibited without proper authority/evidence | design intent documented; runtime authorization unproven |
| engineering guesses legal retention period | prohibited; escalate to authoritative legal/compliance evidence | documented BLOCKED |

## 5. Evidence gaps and remediation

| Gap | State | Required next evidence |
| --- | --- | --- |
| selective runtime delete/anonymize/retain engine | `BLOCKED` | exact implementation + database/integration tests proving per-class decisions |
| legal-hold enforcement | `BLOCKED` | negative tests proving held records cannot be deleted by ordinary/admin flows |
| retained-record access isolation | `BLOCKED` | authorization tests proving retained records are not exposed to ordinary product use |
| selected-provider deletion/backups/subprocessors | `UNKNOWN` | executed provider contract/DPA/technical evidence for actual selected path |
| exact mandatory retention period/legal basis | `UNKNOWN` | launch-time authoritative law/sector/counsel evidence |
| broker/pyPSX record ownership/escalation | `UNKNOWN` | direct P0-T1 partner/pyPSX evidence |
| hosted main protection | `BLOCKED` | ABD-44 authenticated apply + live read-back |
| human closure/hold approver operating evidence | `BLOCKED` | named accountable roles + exercised approval evidence |

## 6. Final tabletop decision

**T-03 final state: `BLOCKED`.**

The repository has a coherent non-live closure/retention decision model, including selective deletion/anonymization, mandatory retention, legal hold and provider propagation rules. That is useful planning evidence but is not runtime or production proof.

This dry run does **not** close GitHub Issue #8 / Linear ABD-73 and does not authorize:

- live customer PII or production onboarding;
- actual deletion/anonymization of production records;
- production provider/subprocessor processing;
- broker/pyPSX operation;
- real-money activity;
- P1+ advancement;
- an assumption about enacted privacy law or exact retention periods.

## 7. Exact next safe action

Continue collecting non-live/runtime evidence independently where possible: implement/test selective retention and legal-hold enforcement only through separately authorized repository work; preserve provider/legal/broker facts as UNKNOWN until authoritative evidence exists; and keep Issue #8 fail-closed until all applicable production acceptance rows are satisfied.
