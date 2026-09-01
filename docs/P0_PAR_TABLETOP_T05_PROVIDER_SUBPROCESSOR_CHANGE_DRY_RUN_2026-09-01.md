# P0-PAR Tabletop T-05 — Provider / Subprocessor Change Dry Run

Status: **BLOCKED — NON-LIVE / SYNTHETIC / NOT PRODUCTION AUTHORITY**

- Parent gate: `ABD-73` / GitHub Issue #8
- Scenario: `T-05 — Provider/subprocessor change`
- Scenario version: `v1`
- Execution date: `2026-09-01`
- Repository base: `0f1882df23c7ed9e8b12837b46b3607cbc8c01ea`
- Data used: **synthetic scenario facts only; no customer PII, credentials, provider secrets or real funds**
- Selected production provider: **NONE / NOT APPROVED**
- Production-authorizing: `false`

This record instantiates T-05 from `docs/P0_PAR_PRODUCTION_ACCEPTANCE_EVIDENCE_PLAN.md` against the non-live vendor/privacy design in `contracts/privacy/P0_PROCESSOR_SUBPROCESSOR_AND_AI_DATA_USE_V1.yaml`. It uses fictional provider/subprocessor identifiers and does not select, approve or represent any actual provider, subprocessor, region, AI service or production processing path.

## Participants and roles

- Repository AI execution operator — scenario recorder and evidence cross-check only; no provider, legal, privacy, security or production-approval authority.
- Provider/vendor owner — **NOT PRESENT / UNVERIFIED**.
- Privacy/compliance owner — **NOT PRESENT / UNVERIFIED**.
- Security reviewer — **NOT PRESENT / UNVERIFIED**.
- Legal/counsel reviewer — **NOT PRESENT / UNVERIFIED**.
- Product/data owner — **NOT PRESENT / UNVERIFIED**.

Absence of required accountable participants is a blocking finding, not implied approval.

## Injected synthetic facts

A fictional provider path `SYNTHETIC-PROVIDER-A / SERVICE-X / REGION-1` is assumed solely for the scenario to have a prior non-production review record. The fictional provider announces two proposed changes:

1. a new subprocessor `SYNTHETIC-SUBPROCESSOR-B` in `REGION-2`; and
2. a revised AI/model-improvement term that may allow retained customer-derived inference content to be used for service improvement unless contractually disabled.

No actual provider is selected, no existing production approval is claimed, and no real contract, DPA, order form, infrastructure account or restricted customer data is involved.

## Expected decisions and controls

Per T-05, the exercise must prove:

1. existing approval does not silently extend to the changed path;
2. affected production processing fails closed until re-review where required;
3. the data-flow/provider register is updated;
4. privacy/legal/security review is retriggered;
5. approval expiry/recheck state is visible.

## Evidence references reviewed

- `docs/P0_PAR_PRODUCTION_ACCEPTANCE_EVIDENCE_PLAN.md`
- `contracts/privacy/P0_PROCESSOR_SUBPROCESSOR_AND_AI_DATA_USE_V1.yaml`
- `docs/P0_PROVIDER_EVIDENCE_AND_ONBOARDING_DATA_FLOW_REGISTER_V1.md`
- `docs/P0_DATA_INVENTORY_AND_PII_BOUNDARY_V1.md`
- `docs/P0_PRIVACY_DATA_GOVERNANCE_BASELINE.md`
- `docs/P0_PRIVACY_LEGAL_RECHECK_2026-08-30.md`
- `.ai/state.json`

Only repository design/evidence artifacts were evaluated. No real provider or production evidence is inferred.

## Decision log and observations

### O-01 — Prior approval must not silently extend

Design control: provider approval is path-specific. Legal entity, service role, data categories, purpose, hosting/transfer regions, subprocessors, retention/deletion, incident terms, AI training/model-improvement terms, assurance and exit behavior are required fields. Unknown destination or subprocessor behavior blocks restricted-data transfer.

Dry-run decision: the fictional prior review for `SERVICE-X / REGION-1` does not authorize `SYNTHETIC-SUBPROCESSOR-B / REGION-2` or the changed AI/model-use term.

Result: `PASS` for the synthetic decision only; `BLOCKED` for production proof because no real approved baseline exists.

### O-02 — Restricted-data processing fails closed during change review

Design control: default external restricted-data sharing is `DENY`; unverified terms are `CONTRACT_REVIEW_REQUIRED`; live restricted-data sharing remains false until the production gate is satisfied.

Dry-run decision: any fictional restricted-data flow affected by the new region/subprocessor or ambiguous model-improvement term remains disabled until the changed path is reviewed and explicitly accepted.

Result: `PASS` for the synthetic fail-closed decision; `BLOCKED` for runtime/configuration proof.

### O-03 — Subprocessor chain and region review

Design control: subprocessors, hosting regions and transfer regions are required contract fields. Cross-border review must record source/destination regions, data categories, purpose, legal/sectoral review, encryption, provider/subprocessor chain, retention/deletion, incident/government-request handling and termination/portability.

Observation: the synthetic change introduces a new region and subprocessor, therefore prior evidence would be stale by design. No authoritative contract, technical region evidence, transfer mechanism or subprocessor terms exist in this exercise.

Result: `BLOCKED`.

### O-04 — AI/model-use term change

Design control: customer-data external model training is prohibited by default; customer conversations, KYC/identity/biometric evidence, financial history and customer-specific recommendation history are prohibited from general model training. Provider training/model-improvement terms must be reviewed before production approval.

Dry-run decision: an ambiguous new service-improvement/model-use clause is treated as non-approval. Restricted/customer-derived content must not be sent under the changed term until explicit contractual and legal/privacy approval exists.

Result: `PASS` for the synthetic fail-closed decision; `BLOCKED` for provider-specific proof.

### O-05 — Data-flow/provider register update

T-05 requires the provider/data-flow register to reflect the changed path rather than preserving stale evidence.

Observation: the repository defines provider categories and evidence fields, but this synthetic exercise deliberately does not write a fictional provider into the canonical production provider register because no provider is actually selected. A real change would require an evidence-backed register update naming the exact legal entity, service, region, subprocessor and data classes.

Result: `PASS` for refusing to contaminate canonical provider truth with fictional data; `BLOCKED` for production exercise completion.

### O-06 — Review retrigger

Design control: provider/service/region/subprocessor/material contract term, AI training/retention/data-use term and production data-flow changes retrigger acceptance review.

Dry-run decision: mark the fictional prior approval stale and require fresh security plus legal/privacy review before any affected restricted-data processing resumes.

Observation: no authorized human security/privacy/legal reviewers participated.

Result: `PASS` for the synthetic retrigger decision; `BLOCKED` for accepted human review evidence.

### O-07 — Retention, deletion, backup and exit implications

Design control: production approval requires known retention/deletion, backup disposition, subprocessor deletion chain, portability/export, termination assistance and dependency-exit plan.

Observation: a new subprocessor/region can alter deletion propagation, backup residence and exit behavior. None of those changed semantics are established by authoritative evidence in this exercise.

Result: `BLOCKED`.

### O-08 — Incident and government-request terms

Design control: incident-notification and government/law-enforcement request handling are required provider evidence areas. Cross-border or new-subprocessor changes cannot assume the old path's obligations remain identical.

Observation: no executed terms exist for the fictional new subprocessor/region.

Result: `BLOCKED`.

### O-09 — Deployment/configuration enforcement

A successful production T-05 exercise would need to prove the unapproved new path cannot receive restricted data through deployment/configuration drift, routing changes or fallback behavior.

Observation: this documentation-only dry run does not exercise provider routing, egress controls, feature flags, secret configuration, environment policy or runtime data-flow enforcement.

Result: `BLOCKED`.

### O-10 — Broker/pyPSX dependency

If the changed provider/subprocessor path touches broker onboarding, customer legal relationship, funding, custody, market data or regulated operations, direct current partner evidence is required under P0-T1 / Issue #20.

Observation: current partner responsibilities and production semantics remain unresolved; this scenario cannot extend broker/pyPSX authority.

Result: `BLOCKED`.

### O-11 — Repository governance dependency

Current main remains unprotected in hosted read-back and Issue #1 remains open. Repository documentation/CI discipline does not substitute for effective protected-main enforcement.

Result: independent production gate remains `BLOCKED`.

## Timeline

1. Synthetic provider change notice injected.
2. New subprocessor, region and AI/model-use changes classified as material.
3. Prior fictional review explicitly prevented from extending to the changed path.
4. Affected restricted-data processing held fail-closed at paper-policy level.
5. Required cross-border/subprocessor evidence fields mapped.
6. Ambiguous AI/model-improvement term rejected for restricted-data use pending explicit review.
7. Canonical production provider register left untouched because the scenario is fictional.
8. Security, privacy/legal and provider evidence re-review requirements triggered.
9. Retention/deletion/backup/exit and incident/government-request gaps recorded.
10. Runtime configuration/data-flow enforcement and partner dependencies recorded as unproven.
11. Final state held `BLOCKED`.

## Control gaps and corrective actions

| Gap | State | Corrective action | Owner / dependency |
| --- | --- | --- | --- |
| No actual selected/approved provider baseline | `BLOCKED` | Instantiate B-01 only for a concrete production candidate with authoritative evidence | Issue #8 / B-01 |
| New subprocessor legal entity/terms unavailable | `BLOCKED` | Obtain contractual subprocessor identity, service role, data scope, notification and deletion terms | B-01 |
| New region/transfer basis unavailable | `BLOCKED` | Obtain technical/contractual region evidence and current legal/privacy review | B-01 / privacy-counsel gate |
| AI/model-improvement term ambiguous | `BLOCKED` | Require explicit no-training/model-use terms or separately approved bounded process before restricted data use | B-01 / AI data-use policy |
| Retention/deletion/backups chain unproven | `BLOCKED` | Verify changed path retention, backup and subprocessor deletion propagation | B-01 / retention integration |
| Portability/termination path unproven | `BLOCKED` | Validate export, exit, secret revocation and retained-copy disposition | B-01 |
| No runtime enforcement preventing unapproved path | `BLOCKED` | Prove routing/egress/configuration controls fail closed under provider change | production runtime acceptance |
| No authorized security/privacy/legal re-review | `BLOCKED` | Re-run with named accountable reviewers and immutable approval evidence | operating governance |
| Broker/pyPSX implications unresolved | `BLOCKED` | Obtain direct current partner evidence under Issue #20 before affected regulated processing | P0-T1 |
| Hosted protected-main enforcement unresolved | `BLOCKED` | Apply and verify effective main protection with repository-admin authority | Issue #1 |

No due date or human owner is invented where none has been authoritatively assigned.

## Final tabletop state

`BLOCKED`

Reason: the accepted non-live vendor/privacy design correctly prevents approval inheritance, requires fail-closed processing, retriggers security/privacy/legal review and rejects ambiguous AI/model-use expansion. But there is no actual selected provider baseline, changed contract/subprocessor/region evidence, accountable human approval, runtime path enforcement or resolved partner/repository governance evidence. Therefore T-05 cannot be a production PASS.

`production_authorizing=false` remains mandatory.

## Reviewer provenance

- Review classification: `SELF REVIEW / documentation-only dry run`
- Independent vendor/security/privacy/legal approval: **not claimed**
- Provider/subprocessor approval: **not claimed**
- Cross-border legal conclusion: **not claimed**
- Runtime enforcement acceptance: **not claimed**
- Broker/pyPSX authority: **not claimed**

A later successful T-05 tabletop must be a new exercise against the actual selected production path and authoritative change notice/contract evidence. This `BLOCKED` result must not be overwritten or retroactively relabeled.
