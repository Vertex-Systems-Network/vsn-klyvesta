# P0-PAR Tabletop T-01 — Restricted-Data Provider Incident Dry Run

Status: **BLOCKED — NON-LIVE / SYNTHETIC / NOT PRODUCTION AUTHORITY**

- Parent gate: `ABD-73` / GitHub Issue #8
- Scenario: `T-01 — Restricted-data provider incident`
- Scenario version: `v1`
- Execution date: `2026-09-01`
- Repository base: `fcb662935ba21ce4b11049c994185d2dfc65a9eb`
- Data used: **synthetic scenario facts only; no customer PII, real credentials, provider secrets or real funds**
- Selected production provider: **NONE / NOT APPROVED**
- Production-authorizing: `false`

This record instantiates the tabletop format required by `docs/P0_PAR_PRODUCTION_ACCEPTANCE_EVIDENCE_PLAN.md`. It is intentionally documentation-only and uses a fictional provider identifier. It does not select or approve a provider, establish contract terms, prove runtime containment, establish legal notification obligations, prove broker/pyPSX responsibility, or authorize production processing.

## Participants and roles

- Repository AI execution operator — scenario recorder and repository evidence cross-check only; no legal, privacy, security, provider, broker or production-approval authority.
- Security incident owner — **NOT PRESENT / UNVERIFIED**.
- Privacy/compliance owner — **NOT PRESENT / UNVERIFIED**.
- Legal/counsel reviewer — **NOT PRESENT / UNVERIFIED**.
- Selected provider incident contact — **NONE; no production provider selected/approved**.
- Broker/pyPSX escalation owner — `UNKNOWN / PARTNER EVIDENCE REQUIRED`.

Absence of required accountable participants and provider-specific evidence is a blocking finding, not an implied approval.

## Injected synthetic facts

A fictional service named `SYNTHETIC-PROVIDER-A` reports suspected unauthorized access to a restricted-data processing path. The notice states that identity/KYC-related records may have been accessible during an unknown exposure window. It does not establish the exact data classes accessed, affected-record count, region, subprocessor involvement, encryption state, actor identity, or whether data was actually exfiltrated.

`SYNTHETIC-PROVIDER-A` is not a real selected provider and is not mapped to any real contract, account, credential, legal entity or infrastructure. No real customer identifier, CNIC, passport, biometric, bank/IBAN, broker record, provider record or transaction is used.

## Expected decisions and controls

Per T-01, the exercise must prove:

1. incident ownership/RACI activates;
2. provider contractual notification evidence is available;
3. affected data/system scope can be identified without exposing raw PII in general logs;
4. legal/privacy escalation path is explicit;
5. evidence preservation and containment decisions are attributable;
6. customer/regulator notification is decided by authorized owners rather than guessed by engineering;
7. unresolved legal facts are escalated to counsel.

## Evidence references reviewed

- `docs/P0_PRIVACY_BREACH_AND_COMPLAINT_RUNBOOKS_V1.md`
- `docs/P0_PAR_PRODUCTION_ACCEPTANCE_EVIDENCE_PLAN.md`
- `docs/P0_PAR_CURRENT_ACCEPTANCE_EVIDENCE_LEDGER_2026-08-30.md`
- `docs/P0_PROVIDER_EVIDENCE_AND_ONBOARDING_DATA_FLOW_REGISTER_V1.md`
- `docs/P0_DATA_INVENTORY_AND_PII_BOUNDARY_V1.md`
- `contracts/security/P0_PRIVILEGED_PII_ACCESS_AND_LOG_REDACTION_V1.yaml`
- `contracts/broker/PYPSX_PARTNER_EVIDENCE_CHECKLIST_V1.yaml`
- `docs/P0_PRIVACY_LEGAL_RECHECK_2026-08-30.md`

Only repository design/evidence artifacts were evaluated. No runtime, provider, partner, counsel or production evidence is inferred from these documents.

## Decision log and observations

### O-01 — Incident activation and ownership

Documented control: Runbook D requires an incident ID/severity, immutable evidence preservation, affected session/credential/integration revocation where necessary, workload/data-path isolation, blocking continued processing when scope is unknown, and engagement of security and privacy/compliance owners.

Dry-run decision: treat the synthetic notice as a security/privacy incident immediately and do not wait for proof of exfiltration before opening the incident path.

Observation: activation semantics are explicit, but no named accountable security/privacy owners participated and no case-management runtime was exercised.

Result: `PASS` for the synthetic decision to open the incident path; `BLOCKED` for production proof.

### O-02 — Provider contractual notification evidence

T-01 requires provider contractual notification evidence to be available. B-01 additionally requires executed provider-specific evidence for contracting entity, region, subprocessor chain, incident-notification obligation/timing, retention/deletion, government-request handling, termination/portability and AI/model-use terms as applicable.

Observation: no production provider is selected or approved, so there is no executed provider contract/DPA/order form, named incident contact, notification SLA, region, subprocessor chain or production configuration to evaluate.

Result: `BLOCKED`.

### O-03 — Immediate containment

Documented control: affected sessions, credentials, API keys or provider integrations may need revocation/disablement; affected workloads/data paths must be isolated; continued export/sharing/processing must be blocked if scope is unknown.

Dry-run decision: because exposure scope is unknown, the safe paper decision is to fail closed for the affected synthetic processing path and preserve evidence before destructive cleanup.

Observation: the repository defines this control at runbook level, but no actual provider integration, credential store, kill switch, routing policy or runtime containment mechanism was exercised.

Result: `ACCEPTED_NON_LIVE` for documented fail-closed design; `BLOCKED` for production proof.

### O-04 — Scope assessment without uncontrolled PII exposure

Documented control: Runbook D requires affected data classes, record-count range, systems/providers, exposure window, actor/access path, encryption/tokenization state, actual-versus-potential access, financial/security impact, secret-rotation needs and regulated-partner involvement. The data-inventory/PII-boundary design and privileged-PII/log-redaction contract prohibit uncontrolled raw sensitive data in general logging/analytics paths.

Observation: repository artifacts provide classification vocabulary and deny-list/redaction expectations, but this dry run has no production telemetry, runtime query path, log sample, redaction test result or provider evidence proving safe scope reconstruction.

Result: `BLOCKED`.

### O-05 — Evidence preservation and attribution

Documented control: preserve audit events, authentication/session/device events, redacted infrastructure/security logs, relevant deployment/config versions, provider references, access/export history, and AI model/tool version/trace references where relevant. Do not create a second uncontrolled copy of leaked PII as evidence.

Dry-run decision: preserve references/hashes/identifiers needed for investigation rather than copying synthetic restricted data into general evidence notes.

Observation: the decision is consistent with the runbook, but no immutable production evidence store, access-control path, retention policy enforcement or retrieval audit was exercised.

Result: `PASS` for the synthetic evidence-handling decision; `BLOCKED` for production proof.

### O-06 — Legal/privacy notification decision

Documented control: authorized legal/privacy/compliance owners decide regulator, broker/partner, affected-customer, law-enforcement or other notifications and record the authority/source consulted, evidence, approver and timestamp. Engineering and AI must not guess or suppress notification obligations.

Dry-run decision: do not invent a notification deadline, statutory duty, customer-notice text or regulator destination. Escalate unresolved legal facts to competent counsel and current formal sources.

Observation: no authorized privacy/legal owner participated and the current privacy-law evidence explicitly requires launch-time/formal-law recheck rather than inferred federal-law status.

Result: `PASS` for refusing to invent a legal conclusion; `BLOCKED` for production acceptance.

### O-07 — Provider/subprocessor/region impact

A real provider incident must be reconciled against the exact approved provider path. A new or implicated subprocessor, region or materially different processing path cannot inherit approval automatically.

Observation: because there is no selected production provider, no approved entity/region/subprocessor baseline exists for comparison. The synthetic incident therefore cannot prove whether the affected path matches an approved production configuration.

Result: `BLOCKED` under B-01.

### O-08 — Broker/pyPSX dependency

If the affected path includes broker/customer-account, onboarding, funding or regulated-partner data, actual responsibility and escalation depend on current direct partner evidence. `P0-T1` / GitHub Issue #20 remains open and the canonical partner evidence checklist still treats production-sensitive fields as unresolved until direct written evidence exists.

Observation: the exercise cannot establish a real broker/pyPSX notification owner, SLA, legal-account relationship or incident responsibility split.

Result: `BLOCKED`.

### O-09 — Runtime privileged-access/log-redaction proof

Issue #8 B-02 requires executable runtime evidence for server-side authorization, least privilege, maker-checker/break-glass, session/device trust, PII-access audit integrity, log redaction, data minimization, export controls and retention/deletion integration.

Observation: accepted design evidence exists, but this dry run does not execute the actual production runtime or adversarial log/access tests.

Result: `BLOCKED`.

### O-10 — Repository governance dependency

The repository contains protected-main policy/apply/verify tooling, but GitHub Issue #1 remains open and hosted branch-protection read-back is not established as accepted live evidence for this exercise.

Result: independent production governance gate remains `BLOCKED`.

## Timeline

1. Synthetic provider incident notice injected.
2. Incident path opened immediately at paper level.
3. Unknown scope treated fail-closed; affected synthetic processing path marked for isolation.
4. Required provider contract/notification evidence checked and found unavailable because no production provider is selected.
5. Required scope-assessment fields mapped to the data-inventory and breach runbook.
6. Evidence-preservation decision recorded without copying restricted synthetic data into general evidence notes.
7. Notification decision routed to authorized legal/privacy owners rather than inferred by engineering/AI.
8. Broker/pyPSX responsibility checked and preserved as unresolved under P0-T1.
9. Runtime privileged-access/log-redaction proof checked and found absent for production acceptance.
10. Production acceptance withheld because provider-specific, runtime, human/legal and repository-governance evidence is absent.

## Control gaps and corrective actions

| Gap | State | Corrective action | Owner / dependency |
| --- | --- | --- | --- |
| No selected/approved production provider | `BLOCKED` | Select the actual production processing path only through B-01 evidence review; do not infer approval from marketing or this tabletop | `B-01` / provider governance |
| No executed provider incident-notification terms/SLA | `BLOCKED` | Obtain executed contract/DPA/order-form evidence for the exact provider entity/service/region and incident obligations | `B-01` |
| No named security/privacy incident owners in exercise | `BLOCKED` | Re-run with accountable named operating participants and recorded decision authority | security/privacy operations |
| No runtime provider isolation/credential revocation proof | `BLOCKED` | Exercise the actual provider kill/isolation path and credential/session revocation controls in a safe environment | runtime/security acceptance |
| No production scope-reconstruction/redaction proof | `BLOCKED` | Execute log/error/trace/access tests proving restricted data can be scoped without raw PII leakage | `B-02` |
| No immutable production evidence-store/retrieval audit proof | `BLOCKED` | Exercise evidence preservation/retrieval with least privilege, attribution and negative tests | `B-02` / incident runtime |
| No authoritative legal/privacy notification decision | `BLOCKED` | Re-run with authorized privacy/legal owner and current formal-law/counsel evidence | privacy/legal pre-launch gate |
| Broker/pyPSX escalation responsibility unresolved | `BLOCKED` | Obtain direct current partner evidence and reconcile Issue #20 / P0-T1 | `ABD-7 / P0-T1` |
| Hosted protected-main enforcement unresolved | `BLOCKED` | Apply/verify live repository protection with repository-admin authority and accepted read-back evidence | `ABD-44` / Issue #1 |

No due date or accountable human is invented where none has been authoritatively assigned.

## Final tabletop state

`BLOCKED`

Reason: the repository contains a sufficiently explicit non-live incident-response design to make correct synthetic fail-closed decisions, preserve evidence safely at paper level and refuse invented legal conclusions. However, T-01 production acceptance specifically depends on an actual selected provider path and contractual notification evidence, accountable human participants, executable containment/scope/redaction/evidence controls, current legal/privacy authority, broker/pyPSX responsibility where applicable, and accepted protected-main governance. Those requirements are not currently established.

`production_authorizing=false` remains mandatory.

## Reviewer provenance

- Review classification: `SELF REVIEW / documentation-only dry run`
- Independent security/privacy/legal approval: **not claimed**
- Selected provider approval: **not claimed**
- Runtime acceptance: **not claimed**
- Broker/pyPSX acceptance: **not claimed**
- Legal conclusion: **not claimed**

A later successful T-01 tabletop must be a new exercise/record against the actual selected production path. This `BLOCKED` result must not be overwritten or retroactively relabeled.
