# P0 Provider Evidence & Onboarding Data-Flow Register V1

Status: **Draft non-live P0 operating artifact for ABD-68 / GitHub Issue #8.** No provider or partner is approved for production by this document. Unverified contract semantics remain fail-closed.

## Purpose

Define the provider/service categories Klyvesta may depend on for onboarding, KYC/AML, bank/funding verification, sanctions screening and broker account operation; document what data may flow to/from each category; define evidence, credential, callback, retention and outage requirements; and explicitly record unknowns that require direct contract evidence.

Canonical inputs:

- `contracts/broker/PYPSX_PARTNER_EVIDENCE_CHECKLIST_V1.yaml`
- `contracts/broker/BROKER_ADAPTER_V1.md`
- `docs/P0_DIGITAL_ONBOARDING_AML_OPERATING_BASELINE.md`
- `docs/P0_PRIVACY_DATA_GOVERNANCE_BASELINE.md`
- `docs/P0_DATA_INVENTORY_AND_PII_BOUNDARY_V1.md` (ABD-66 draft)
- `docs/P0_AML_OBLIGATIONS_AND_REGULATED_PERSON_RACI_V1.md` (ABD-67 draft)
- GitHub Issue #8 and P0-T1 / Issue #20

## Governing rules

1. Public marketing claims never satisfy production contract fields.
2. Direct partner/provider evidence is required for production verification.
3. Unknown or contradictory external financial/compliance semantics fail closed.
4. Provider secrets remain server-side and never enter web/mobile/desktop bundles or AI contexts.
5. Side-effecting operations are not blindly retried after ambiguous outcomes.
6. Provider evidence must be referenced immutably and retained only to the extent legally/operationally required.
7. Klyvesta does not infer that a provider's generic privacy policy is sufficient for regulated financial/identity data.

## Provider register

| Provider / service category | Current named provider | Data sent | Evidence returned | Credential boundary | Callback / replay expectations | Retention / deletion | Accountable owner | Current status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Broker / account / execution API | pyPSX Broker API candidate | approved onboarding refs, minimum required KYC/account fields, normalized broker/funding/order commands | account status, broker IDs, UIN/CDC/NCCPL refs if exposed, orders/fills/balances/positions/funding/withdrawal evidence | narrow server-side Broker Adapter credential; environment-scoped; no AI/client access | signature/auth, event ID, timestamp/nonce, dedupe, ordering/retry semantics must be contractually verified | broker/partner schedule unknown; Klyvesta stores minimum normalized evidence refs | exact RP/broker legal entity **UNKNOWN** | `CONTRADICTORY / DIRECT PARTNER EVIDENCE REQUIRED` |
| Identity proofing / document verification | UNKNOWN | minimum identity attributes/document refs required by approved flow | verification result, provider case/ref, method, timestamps, reason codes | server-to-server; scoped env credentials | signed/authenticated callbacks; schema validation; replay protection; dedupe | retain reference/result by default; raw docs only if required | RP responsibility **UNKNOWN** | `UNKNOWN / CONTRACT REQUIRED` |
| Biometric verification | UNKNOWN | minimum biometric challenge/evidence required by current policy | match/liveness/result reference, method/version, timestamps | server-to-server; no raw biometric secret in client config | authenticated callback; anti-replay; correlation ID | no biometric template/image retention unless explicitly required and approved | RP/provider allocation **UNKNOWN** | `UNKNOWN / CONTRACT REQUIRED` |
| Bank / IBAN / e-wallet ownership verification | UNKNOWN | customer/funding-account identifiers required for ownership verification | ownership/status result, provider reference, verified account token/reference | server-to-server; credentials isolated from funding UI/client | authenticated callback/query; idempotency and correlation | tokenized/hashed identifiers where possible; raw evidence only by requirement | RP/funding owner **UNKNOWN** | `UNKNOWN / CONTRACT REQUIRED` |
| Sanctions / PEP / TFS screening | UNKNOWN | minimum identity/beneficial-owner attributes required for screening | list/source version, match candidates, risk indicators, provider ref | server-to-server; scoped compliance credential | event/query contract; source/list version must be preserved | retain result/reference and disposition evidence according to AML schedule | accountable RP **UNKNOWN** | `UNKNOWN / CONTRACT REQUIRED` |
| Source-of-funds / source-of-wealth evidence | UNKNOWN / manual + provider mix | evidence refs or documents required by risk policy | verification/reference/review result | protected compliance/PII boundary | if provider callback exists, same auth/replay controls apply | high-risk restricted evidence; legal retention policy required | accountable RP **UNKNOWN** | `UNKNOWN / POLICY + CONTRACT REQUIRED` |
| Transaction monitoring / AML analytics | UNKNOWN / internal deterministic baseline | normalized transaction/activity facts and customer risk context | alerts, rule/version, evidence refs | server-side compliance boundary | event ingestion must be idempotent, ordered or reconciliation-safe | alerts/dispositions retained per AML schedule | accountable RP **UNKNOWN** | `UNKNOWN / PARTNER ALLOCATION REQUIRED` |
| Customer communications / verification messaging | UNKNOWN | sanitized OTP/link/template payloads and destination reference | delivery result/provider ref | notification credential only; no financial mutation authority | delivery webhook must be authenticated/deduped | short operational retention; no raw KYC/financial payload | Klyvesta operational owner | `PROVIDER NOT SELECTED` |
| Cloud / storage / model subprocessors touching restricted data | UNKNOWN | only approved minimum data under ABD-70 policy | service/audit/incident evidence | workload identity / scoped service credentials | provider-specific event/audit contracts | contract-specific retention/deletion; no external AI training by default | Klyvesta + RP legal review | `UNKNOWN / CONTRACT REVIEW REQUIRED` |

## Required evidence envelope

Every material external onboarding/provider assertion should be represented internally with at least:

```text
provider_category
provider_name?
environment
operation_or_evidence_type
internal_customer_id
internal_case_id?
provider_reference
request_correlation_id
observed_at
provider_timestamp?
result_state
reason_code?
schema_or_api_version
verification_method?
policy_version
reviewer_or_approver?
expiry_or_revalidation_at?
raw_evidence_ref?  # protected/redacted/retention-controlled
```

The evidence envelope stores a protected reference or hash where possible, not an uncontrolled copy of raw identity/biometric/bank material.

## Onboarding data-flow model

### 1. Contact and identity initiation

```text
Customer client
  -> Klyvesta onboarding API
  -> customer/contact + identity case references
  -> identity proofing provider (only minimum approved attributes/evidence)
  <- provider verification result/reference
  -> PII vault / evidence reference
  -> deterministic onboarding state
```

Rules:

- identity proofing is not login authentication;
- raw identity evidence does not flow into trading/portfolio/AI services;
- provider failure or unknown result cannot promote onboarding state.

### 2. KYC / AML / sanctions path

```text
PII/evidence refs + customer profile
  -> Klyvesta Compliance Workflow
  -> sanctions/PEP/TFS provider or approved source
  <- source/version + candidate/result evidence
  -> Human Compliance / deterministic Compliance Gate
  -> APPROVED | REVIEW | RESTRICT | REJECT
```

Rules:

- AI may summarize but cannot clear a match or alert;
- authoritative RP and final approval ownership remain partner-evidence dependent;
- uncertain/positive material match fails closed.

### 3. Funding-account ownership path

```text
Customer-provided funding account reference
  -> funding service / PII boundary
  -> bank/IBAN/e-wallet ownership verification provider
  <- verified ownership result/reference
  -> funding_account verification state
```

A provider `success` result alone does not create investable cash. Investable eligibility still requires customer identity/compliance/broker status and reconciliation evidence.

### 4. Broker account-opening path

```text
Approved internal onboarding reference
  + minimum contract-required KYC/provider tokens
  -> Execution/account workflow
  -> Broker Adapter
  -> pyPSX / underlying broker API
  <- account status + external identifiers/evidence
  -> normalized broker account state
```

Rules:

- exact account/KYC schema is `UNVERIFIED`;
- Broker Adapter exposes normalized states and retains external evidence without leaking provider payload semantics into domain code;
- external unknown state maps to `UNKNOWN` / restricted onboarding state.

### 5. Callback / webhook ingress

```text
Provider callback
  -> dedicated ingress boundary
  -> auth/signature verification
  -> timestamp/nonce/replay validation
  -> body/schema/size validation
  -> event ID dedupe / inbox
  -> normalized provider event
  -> domain state transition only if authoritative and policy-valid
```

If ordering is not contractually guaranteed, out-of-order events are expected and domain processing must reconcile rather than assume order.

## Credential and transport baseline

All external onboarding/provider integrations must define before production:

- sandbox and production environment identity;
- separate environment credentials;
- authentication method;
- credential scopes/roles;
- lifetime, rotation and revocation;
- TLS requirements and stronger sender-constraining/mTLS where supported;
- network allow-list/private networking where supported;
- webhook signing/authentication method;
- rate limits and timeout semantics;
- auditability of credential use.

No provider secret may be placed in:

- browser/mobile/desktop configuration;
- customer-visible payloads;
- logs/analytics;
- LLM prompts, tools or traces.

## Retry, idempotency and ambiguity baseline

Read-only queries may be retried according to provider policy.

Side-effecting operations such as account creation, order submission, withdrawal or beneficiary changes require:

- documented idempotency/client reference semantics; or
- post-timeout query/reconciliation before retry.

If a side effect may have occurred and cannot be proven either way, normalized state is `UNKNOWN`. Blind retry is prohibited.

## Retention and deletion responsibility

For each provider category, contract review must resolve:

- what evidence Klyvesta must retain;
- what evidence Klyvesta must not retain;
- provider-side retention period;
- deletion/termination process;
- legal-hold behavior;
- location/region and subprocessors;
- incident/breach notification obligation;
- ability to retrieve evidence for regulatory/complaint purposes;
- portability/exit procedure.

Until resolved, the register field remains `UNKNOWN / CONTRACT REQUIRED` and the provider is not production-approved.

## Outage / manual-review fallback

| Dependency | Fail-closed behavior |
| --- | --- |
| Identity proofing unavailable | onboarding remains pending/restricted; no manual bypass without approved controlled process |
| Biometric/verification provider unavailable | required verification remains incomplete; no silent downgrade to weaker evidence |
| Sanctions/PEP/TFS screening unavailable/stale | compliance-dependent activation/action denied or held |
| Bank/IBAN ownership verification unavailable | funding account remains unverified; withdrawal/funding capability restricted |
| Broker account API unavailable | account state remains pending/unknown; no fabricated active status |
| Provider callback ambiguous | query/reconcile; preserve evidence; no blind state advancement |
| Partner evidence unavailable | design may continue, production integration/approval remains blocked |

## pyPSX / broker-specific unresolved fields

The canonical `PYPSX_PARTNER_EVIDENCE_CHECKLIST_V1.yaml` remains authoritative for detailed direct-evidence fields, including:

- rollout/sandbox/onboarding/certification;
- legal broker/licence/TREC/customer-contract identity;
- KYC/AML responsibility;
- UIN/CDC/NCCPL/custody/cash-holder model;
- environment/auth/credential semantics;
- account/KYC schemas and retention;
- order/execution/event/retry/idempotency semantics;
- funding/withdrawal flows;
- market-data source/rights/freshness;
- SLA/support/incident responsibilities;
- commercial/data-processing terms.

This register must never override an `UNVERIFIED` or `CONTRADICTORY` checklist field with an assumption.

## Acceptance checklist for ABD-68

- [x] All known onboarding dependency categories represented.
- [x] Data sent/returned and evidence model defined.
- [x] Credential/authentication boundary defined.
- [x] Callback/replay/idempotency requirements defined.
- [x] Retention/deletion contract questions defined.
- [x] Outage/manual-review fail-closed behavior defined.
- [x] pyPSX/broker unknowns linked to the canonical partner evidence checklist.
- [ ] Named providers/contracts verified for identity, biometric, bank and screening services.
- [ ] Direct pyPSX/broker partner package received and reconciled.
- [ ] Region/subprocessor and processing terms approved for selected providers.
- [ ] Repository governance/acceptance permits merge to canonical main.

## Roadmap effect

This artifact advances non-live provider/data-flow preparation only. It does not authorize production provider use, live onboarding/PII, live pyPSX integration, or P1/P2/P3/P4 advancement.
