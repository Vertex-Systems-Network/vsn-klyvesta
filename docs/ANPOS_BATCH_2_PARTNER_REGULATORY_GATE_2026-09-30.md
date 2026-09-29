# ANPOS Batch 2 — Partner and Regulatory Evidence Gate

Date: 2026-09-30 (Asia/Karachi)
Repository: `Vertex-Systems-Network/vsn-klyvesta`
Related blocker: Issue #20 — P0-T1: Resolve pyPSX public rollout-state contradiction and obtain partner contract
Status: **BLOCKED — direct partner/regulatory evidence not yet available**

## Purpose

Batch 2 is the evidence gate between documentation reconciliation and any credential, sandbox, adapter, KYC, funding, market-data or production work. Public marketing material and the supplied API document are not sufficient evidence for this batch.

Batch 2 is not complete until the required answers are received from an authoritative partner/legal source, their provenance is recorded, contradictions are resolved, and the affected Klyvesta contracts and acceptance gates are updated.

## Current evidence position

- Batch 1 supplied documentation provides documented capability claims only.
- No current partner contract, signed technical agreement, sandbox access confirmation, or authoritative written response is recorded in this repository.
- Issue #20 remains open.
- Sandbox availability, exact licensed broker identity, custody structure, KYC responsibility, funding/withdrawal rails, market-data rights, commercial terms and production approval remain unverified.
- No real-money adapter, live KYC flow, customer PII, funding flow or production authority is enabled.

## Acceptance matrix

| Gate | Required evidence | Current status | Close condition |
| --- | --- | --- | --- |
| Rollout state | Written confirmation of production-live, limited-live or early-access status | **UNVERIFIED** | Named partner owner and dated written answer |
| Sandbox | Current sandbox URL, onboarding, credentials process and certification steps | **UNVERIFIED** | Non-live access confirmed and tested under approved controls |
| Licensed broker | Exact legal entity, SECP licence and PSX/TREC details | **UNVERIFIED** | Primary-source/licence evidence plus partner confirmation |
| Client relationship | Customer agreement, broker/pyPSX role and responsibility split | **UNVERIFIED** | Written legal/commercial responsibility map |
| KYC/AML | KYC owner, CDC/UIN/NCCPL flow, PII retention/region/deletion | **UNVERIFIED** | Written RACI and approved data-flow treatment |
| Custody/settlement | Custodian, account structure, cash/securities ownership and T+1 flow | **UNVERIFIED** | End-to-end written custody and settlement flow |
| Orders/executions | Order types, IDs, idempotency, timeout, retry, cancel/replace and UNKNOWN semantics | **UNVERIFIED** | Technical contract plus sandbox tests |
| Events | Webhook/SSE auth, signing, ordering, replay, retry and schema versioning | **UNVERIFIED** | Technical contract plus adversarial/replay tests |
| Funding/withdrawals | Deposit/withdrawal rails, beneficiary, approval, reversal and reconciliation | **UNVERIFIED** | Written flow and testable API contract |
| Market data | Source, real-time/delayed status, licensing and redistribution rights | **UNVERIFIED** | Written rights and technical source evidence |
| SLA/support | Rate limits, uptime, maintenance, incidents, escalation and RTO/RPO | **UNVERIFIED** | Current commercial/operational agreement |
| Commercial/data terms | Fees, revenue share, minimums, branding, customer/data ownership, termination/portability | **UNVERIFIED** | Signed or authoritative commercial terms |
| Securities Manager path | Licence holder, minimum investment, IPS, suitability, custody and disclosures | **UNVERIFIED** | Written partner/legal classification |
| AI-assisted model | Regulatory treatment, disclosures and responsible licensed party | **UNVERIFIED** | Written partner/legal classification |
| Production approval | Security, audit, certification and launch sign-off requirements | **UNVERIFIED** | Named approvers and documented go-live gate |

## Authoritative evidence request

Send the following as one written request to the pyPSX partner contact and retain the response outside this public repository if confidential:

1. Identify the exact SECP-licensed broker legal entity, licence number and PSX/TREC details.
2. Confirm whether Broker API is production-live, limited-live or early access, and whether sandbox access is currently available.
3. Provide the current API/OpenAPI/Postman contract, authentication model, environment separation and sandbox onboarding process.
4. Define customer legal relationship, KYC/AML ownership, UIN/CDC/NCCPL responsibilities, custody and T+1 settlement.
5. Provide order, execution, idempotency, timeout, retry, cancellation, event-signing and reconciliation semantics.
6. Define deposit and withdrawal architecture, beneficiary controls, approvals, reversals and settlement timing.
7. Identify market-data sources, freshness, licensing and redistribution rights.
8. Provide SLA, rate limits, support escalation, maintenance, incident and business-continuity obligations.
9. Provide commercial, white-label, customer-data, termination and portability terms.
10. Confirm Securities Manager/advisory/discretionary-management licensing and the allowed Manual, AI-assisted and Guarded Auto product boundaries.
11. State required security review, certification, audit and production launch approvals.

## Evidence handling rules

- Record source, author/role, date, scope, version and integrity reference for every response.
- Keep confidential contracts, credentials and raw PII outside the public repository.
- Commit only a sanitized evidence index and redacted decisions.
- A public claim is never upgraded to contractually or legally confirmed without authoritative written evidence.
- Conflicting answers remain unresolved until the named authority reconciles them in writing.
- No implementation branch may bypass this gate by treating docs, screenshots or assumed credentials as approval.

## Batch 2 exit criteria

Batch 2 can be marked **COMPLETE** only when:

- every acceptance-matrix gate is either verified or explicitly rejected/not applicable by the responsible authority;
- Issue #20 has traceable evidence and its contradiction is resolved;
- `docs/P0_PYPSX_OPERATING_MODEL_DUE_DILIGENCE.md`, broker contract, RACI, funding/custody/data flows and threat model are updated from evidence;
- P0 acceptance gates are re-evaluated;
- unresolved legal/provider risks have an owner and an explicit go/no-go decision;
- no live implementation is started ahead of the approved boundary.

Until then, the correct status is **BLOCKED / INCOMPLETE**, not complete.

## Next action

Obtain the written partner response and add a sanitized evidence index. After that, re-run the matrix and only then decide whether Batch 3 credential/environment work can begin.
