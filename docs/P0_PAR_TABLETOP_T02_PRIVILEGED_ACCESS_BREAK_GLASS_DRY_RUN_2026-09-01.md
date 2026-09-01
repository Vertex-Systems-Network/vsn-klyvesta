# P0-PAR Tabletop T-02 — Privileged-Access Misuse / Break-Glass Dry Run

Status: **BLOCKED — NON-LIVE / SYNTHETIC / NOT PRODUCTION AUTHORITY**

- Parent gate: `ABD-73` / GitHub Issue #8
- Scenario: `T-02 — Privileged-access misuse / break-glass event`
- Scenario version: `v1`
- Execution date: `2026-09-01`
- Repository base: `6fb35c59332fd938c6d8d941cda3896c8ebb550a`
- Data used: **synthetic scenario facts only; no customer PII, credentials, provider secrets or real funds**
- Production-authorizing: `false`

This record instantiates T-02 from `docs/P0_PAR_PRODUCTION_ACCEPTANCE_EVIDENCE_PLAN.md` against the accepted non-live design in `contracts/security/P0_PRIVILEGED_PII_ACCESS_AND_LOG_REDACTION_V1.yaml`. It is a documentation-only exercise. It does not claim that production privileged-access enforcement, break-glass workflow, immutable audit storage, session/device revocation or log-redaction controls are implemented or accepted.

## Participants and roles

- Repository AI execution operator — scenario recorder and evidence cross-check only; no privileged production role or approval authority.
- Security owner — **NOT PRESENT / UNVERIFIED**.
- Privacy/compliance owner — **NOT PRESENT / UNVERIFIED**.
- Break-glass approver — **NOT PRESENT / UNVERIFIED**.
- Runtime/platform owner — **NOT PRESENT / UNVERIFIED**.

Missing accountable participants are blocking evidence, not implied approval.

## Injected synthetic facts

A fictional `DEVOPS_SRE` actor attempts to access raw restricted customer identity evidence during an urgent incident. The actor first attempts ordinary privileged access without an assigned case purpose. After denial, the actor requests emergency break-glass access but initially omits an incident/ticket reference and expiry. A second synthetic attempt supplies a ticket reference, explicit reason, minimum requested scope and expiry, but no distinct second-person approval or executable runtime evidence is available in this dry run.

No real actor, customer, CNIC, passport, biometric, bank/IBAN, token, provider secret, broker credential or production system is involved.

## Expected decisions and controls

Per T-02, the exercise must prove:

1. deny-by-default access where authority is absent;
2. break-glass cannot bypass required reason, expiry and attribution;
3. immutable/auditable access evidence exists;
4. session/device restriction or revocation works where required;
5. security/privacy incident escalation is triggered by policy.

## Evidence references reviewed

- `docs/P0_PAR_PRODUCTION_ACCEPTANCE_EVIDENCE_PLAN.md`
- `contracts/security/P0_PRIVILEGED_PII_ACCESS_AND_LOG_REDACTION_V1.yaml`
- `docs/AUTHORIZATION_PRIVILEGE_MODEL.md`
- `docs/AUTH_SESSION_ARCHITECTURE_V1.md`
- `docs/P0_PRIVACY_BREACH_AND_COMPLAINT_RUNBOOKS_V1.md`
- `docs/P0_DATA_INVENTORY_AND_PII_BOUNDARY_V1.md`
- `.ai/state.json`

Only repository design/evidence artifacts were evaluated. No runtime enforcement is inferred.

## Decision log and observations

### O-01 — Ordinary privileged access without case purpose

Design control: privileged decisions require authenticated principal, role/workload identity, action, resource/customer or case scope, documented purpose, security/compliance state and approval/break-glass context where required. `DEVOPS_SRE` is explicitly forbidden from standing production customer-data access.

Dry-run decision: deny the synthetic raw-PII request because no assigned case purpose or approved emergency context exists.

Result: `PASS` for the synthetic policy decision only; `BLOCKED` for runtime proof.

### O-02 — Incomplete break-glass request

Design control: break-glass requires phishing-resistant MFA, incident/ticket reference, explicit reason, minimum temporary scope, expiry timestamp, security/compliance notification, immutable audit record and after-action review; second-person approval is required where practical.

Dry-run decision: reject the first break-glass request because ticket reference and expiry are missing. Emergency context must not silently convert incomplete authorization into access.

Result: `PASS` for the synthetic decision only; `BLOCKED` for runtime proof.

### O-03 — Minimally specified break-glass request

The second synthetic request contains a ticket reference, explicit reason, minimum requested scope and expiry.

Observation: these fields satisfy only part of the design contract. This dry run has no phishing-resistant MFA evidence, distinct authorized approver, security/compliance notification evidence, runtime scope enforcement, immutable audit event or after-action workflow.

Dry-run decision: do **not** treat the request as production-authorized merely because some required fields are present.

Result: `BLOCKED`.

### O-04 — Least privilege and restricted-data scope

Design control: raw evidence access is explicit-purpose-and-audit only; support and infrastructure roles receive redacted/minimum context by default; standing superuser and unrestricted customer browsing are prohibited.

Dry-run decision: if incident investigation can proceed with opaque identifiers, redacted provider references and approved security telemetry, raw restricted identity evidence is not granted.

Observation: no runtime field-level minimization/view policy was exercised.

Result: `PASS` for the synthetic minimization decision; `BLOCKED` for production proof.

### O-05 — Audit evidence

Design control: privileged audit events require actor, role/workload identity, action, target, scope, documented purpose, correlation ID, timestamp, policy version, decision/reason, approval/break-glass references and approved redacted mutation summaries; forbidden payloads include secrets, full identity documents, biometric artifacts and unnecessary full bank/IBAN. Audit mutability is `APPEND_ONLY`.

Observation: the schema is explicit, but no production append-only store, tamper-evidence control, write-failure behavior or runtime access-event emission was exercised.

Result: `BLOCKED`.

### O-06 — Session/device containment

T-02 requires session/device restriction or revocation where policy requires it. The repository contains provider-neutral session/device security design and separately reviewed feature-branch implementation evidence, but this P0-PAR dry run does not establish production-path runtime acceptance.

Dry-run decision: suspected privileged misuse should trigger the incident path and permit authorized session/device revocation or restriction rather than preserving access for convenience.

Observation: no production session/device operation was executed.

Result: `PASS` for the synthetic containment decision; `BLOCKED` for runtime proof.

### O-07 — Incident escalation

Runbook D requires incident activation, evidence preservation, isolation/revocation where necessary, and engagement of security and privacy/compliance owners. AI may not suppress or downgrade notification obligations.

Dry-run decision: link the attempted prohibited access/break-glass event to a security/privacy incident path and preserve disputed facts as observations rather than established misconduct.

Observation: no named incident owner or executable workflow participated.

Result: `PASS` for the synthetic escalation decision; `BLOCKED` for production proof.

### O-08 — Log redaction / evidence safety

Design control prohibits raw secrets, CNIC/passport, identity documents, biometric artifacts, full bank/IBAN, raw KYC/AML payloads and sensitive request bodies from general logs; restricted fields must be redacted or dropped.

Observation: no adversarial production log/error/trace tests were executed. Therefore the exercise cannot prove that a denied or break-glass attempt itself avoids leaking restricted values through observability.

Result: `BLOCKED` under B-02.

### O-09 — Maker-checker / independent approval

High-risk privileged actions require separation of duties where defined. A requester must not manufacture their own effective approval context.

Observation: no distinct authorized approver participated; second-person approval is absent in this dry run.

Result: `BLOCKED`.

### O-10 — Repository governance dependency

Current main read-back remains `protected=false`, tracked by Issue #1. Repository policy/verification tooling does not substitute for effective hosted protection.

Result: independent production gate remains `BLOCKED`.

## Timeline

1. Synthetic ordinary privileged raw-PII access request injected.
2. Request denied for missing case purpose/authority.
3. Incomplete break-glass request injected without ticket reference/expiry.
4. Incomplete break-glass request denied.
5. Minimally specified break-glass request injected with reason, scope, ticket and expiry.
6. Missing MFA/approver/notification/runtime/audit evidence identified; production authorization withheld.
7. Least-privilege/redacted investigation path preferred over raw evidence access.
8. Security/privacy incident linkage and session/device containment decision recorded at paper level.
9. Audit/log-redaction/runtime evidence gaps recorded.
10. Final tabletop state held `BLOCKED`.

## Control gaps and corrective actions

| Gap | State | Corrective action | Owner / dependency |
| --- | --- | --- | --- |
| No executable server-side privileged authorization proof | `BLOCKED` | Execute positive/negative runtime authorization tests against the actual production-path policy | Issue #8 / B-02 |
| No exercised phishing-resistant MFA for break-glass | `BLOCKED` | Prove approved authentication/step-up path in the selected runtime | B-02 / identity runtime |
| No distinct break-glass approver | `BLOCKED` | Re-run with authorized separate approver where required and recorded decision | security/compliance operations |
| No runtime scope/expiry enforcement | `BLOCKED` | Prove minimum temporary scope and automatic expiry with negative tests | B-02 |
| No append-only privileged audit runtime proof | `BLOCKED` | Exercise event creation, attribution, tamper resistance and failure behavior | B-02 |
| No production session/device restriction/revocation evidence | `BLOCKED` | Exercise authorized containment against the accepted production session/device path | security runtime |
| No adversarial log-redaction evidence | `BLOCKED` | Execute forbidden-field fixtures across logs/errors/traces/exception paths | B-02 |
| No named security/privacy incident owners | `BLOCKED` | Re-run with accountable operating participants | operating governance |
| Hosted protected-main enforcement unresolved | `BLOCKED` | Apply and verify effective main protection with repository-admin authority | Issue #1 |

No due date or human owner is invented where none has been authoritatively assigned.

## Final tabletop state

`BLOCKED`

Reason: the accepted design contract is sufficiently explicit to make correct synthetic deny-by-default, incomplete-break-glass rejection, minimization, containment and escalation decisions. Production acceptance nevertheless requires executable authorization/break-glass/audit/redaction/session-device evidence and accountable human approvals that are absent from this dry run.

`production_authorizing=false` remains mandatory.

## Reviewer provenance

- Review classification: `SELF REVIEW / documentation-only dry run`
- Independent security/privacy approval: **not claimed**
- Runtime privileged-access acceptance: **not claimed**
- Break-glass production acceptance: **not claimed**
- Legal/provider/broker authority: **not claimed**

A later successful T-02 tabletop must be a new exercise against the actual accepted production runtime and must not overwrite or retroactively relabel this `BLOCKED` result.
