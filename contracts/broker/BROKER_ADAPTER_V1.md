# Broker Adapter Contract V1

Status: Internal Klyvesta contract. pyPSX mapping is **UNVERIFIED** until partner technical documentation/sandbox access is received.

## Purpose

Prevent pyPSX or any future broker payload/protocol from leaking into Klyvesta domain logic. The adapter translates external capabilities/states into normalized contracts and must surface unsupported/unknown behavior explicitly.

## Authority boundary

The Broker Adapter:
- receives only already-authorized normalized commands from the Execution Validator or approved account/funding workflow;
- owns the narrow broker credential/integration boundary;
- cannot originate customer business intent;
- cannot bypass Risk Governor/Compliance Gate/Execution Validator;
- cannot write arbitrary ledger balances;
- returns external facts/evidence for domain services and reconciliation.

## Required capability discovery

Every adapter exposes a `Capabilities` document containing at least:
- broker code/version;
- account-opening support;
- supported order types;
- supported time-in-force values;
- modify/replace support;
- cancel semantics;
- idempotency/client-reference support;
- execution/fill identifiers;
- streaming/webhook/polling support;
- balance/position endpoints;
- funding/deposit support;
- withdrawal support;
- statements support;
- market data support and freshness metadata;
- sandbox/production environment identity;
- documented rate limits if known.

Unsupported capability is `UNSUPPORTED`, never silently simulated.

## Normalized result envelope

Every call returns/throws through a normalized boundary containing:

```text
request_id
broker_code
environment
operation
observed_at
broker_timestamp? 
external_correlation_id?
result_state: SUCCESS | REJECTED | RETRYABLE_FAILURE | UNKNOWN
reason_code?
raw_evidence_ref? (redacted/retention-controlled)
```

`UNKNOWN` means the financial side effect may have occurred and must trigger query/reconciliation rather than blind retry.

## Account operations

### OpenAccount
Input:
- approved customer/broker onboarding reference
- minimum required broker/KYC data or provider tokens according to contract
- internal idempotency/correlation reference

Output:
- normalized account state
- external broker account identifiers/references
- UIN/CDC/NCCPL-related references where contract legally/technically exposes them
- restrictions/capabilities

### GetAccountStatus
Returns normalized:
`PENDING | ACTIVE | RESTRICTED | SUSPENDED | CLOSED | REJECTED | UNKNOWN`

Exact pyPSX states must be mapped and documented.

## Balance / position operations

### GetBalances
Return exact-decimal strings/typed decimal values with:
- currency
- cash/available/reserved/unsettled fields only if external semantics are known
- observed timestamp
- source reference

Never map an unknown broker field into `available_cash` by assumption.

### GetPositions
Return:
- instrument external/internal mapping
- quantity
- settled/unsettled semantics if known
- cost basis only if reliable/defined
- observed timestamp

Klyvesta reconciliation determines whether projections agree.

## Order submission

### SubmitOrder
Input:
- internal broker order ID
- immutable internal order intent ID
- external/client reference/idempotency key
- account reference
- instrument reference
- side
- normalized order type
- exact quantity
- exact limit price if applicable
- time in force

Precondition: caller is Execution Validator path; adapter does not perform product/business authorization itself.

Output:
- normalized result envelope
- broker external order ID/reference if known
- normalized order state

If timeout occurs after request could have reached broker, return `UNKNOWN`, not `RETRYABLE_FAILURE`, unless broker contract provides a safe idempotent retry guarantee.

## GetOrder
Must support lookup by strongest available reference:
1. broker external order ID;
2. broker-supported client reference/idempotency key;
3. other documented correlation method.

Output normalized state:
`PENDING_SUBMIT | SUBMITTED | OPEN | PARTIALLY_FILLED | FILLED | CANCEL_PENDING | CANCELLED | REJECTED | UNKNOWN`

External statuses are retained for evidence but domain code uses normalized state.

## CancelOrder
Cancel is race-prone. Output does not imply order had zero fills unless broker evidence confirms it.

If cancel acknowledgement is ambiguous:
- normalized `UNKNOWN` or `CANCEL_PENDING`;
- query order/executions;
- reconcile before releasing full reservation.

## ModifyOrReplaceOrder
Only implemented if broker semantics are explicitly documented and tested.

If broker uses cancel-replace, expose that semantic rather than pretending it is an atomic mutation.

## Executions

### ListExecutions / GetExecutions
Each execution requires a stable de-duplication identity.

Preferred:
- unique broker execution/trade ID.

If absent, a documented composite key strategy must be reviewed before production.

Return:
- external execution ID/key
- external order ID
- instrument
- side if supplied/derivable safely
- exact quantity/price
- fee/tax fields only with known meaning
- trade timestamp
- settlement date/timestamp if known
- observed time

Duplicate execution must never produce a duplicate internal financial effect.

## Funding

### GetFundingStatus / ListDeposits
Mapping must define:
- external funding reference
- detected/confirmed/reconciled semantics
- amount/currency
- reversals/chargeback semantics if applicable

Klyvesta makes cash investable only after its approved reconciliation rule; broker `success` wording alone is not enough unless contract defines it.

### RequestWithdrawal / GetWithdrawalStatus
Only implement if pyPSX/broker contract authorizes/supports this flow.

External withdrawal state maps to the Klyvesta withdrawal state machine. Ambiguous timeout => `UNKNOWN`.

## Statements

### GetStatements
Returns reference/stream to broker statement with:
- account
- statement period
- generated timestamp
- integrity/content metadata if available

Do not permanently expose broker-authenticated URLs to clients.

## Market data

If adapter also supplies market data, every quote/update requires:
- instrument mapping
- source timestamp
- received/observed timestamp
- market/session indicator where available
- sequence/version where available

Execution-time freshness checks are outside the adapter and remain deterministic domain policy.

Arbitrary internet/news prices never implement this contract.

## Webhooks/events

Provider-specific ingress must verify:
- signature/authentication method
- timestamp freshness
- nonce/replay protection if supported
- event/external ID de-duplication
- content type/body limits
- schema/version

The adapter maps provider event into normalized broker event before domain processing.

Out-of-order events are expected unless provider contract guarantees ordering and that guarantee is verified.

## Retry classification

`RETRYABLE_FAILURE` only when repeating the operation cannot duplicate/alter a financial side effect, or when provider's idempotency guarantee is contractually verified.

Examples likely safe:
- read-only GET/query;
- provider-confirmed idempotent request with same client key.

Examples default unsafe until verified:
- submit order after network timeout;
- withdrawal after network timeout;
- cancel/replace with ambiguous response.

## Secrets

- Broker credentials remain server-side in KMS/Vault/HSM-backed secret infrastructure.
- Never in mobile/desktop/web bundles.
- Never in LLM prompts/context.
- Scoped per environment/operation where provider supports it.
- Rotation procedure and dual-control access documented.

## Observability

Record without leaking secrets:
- latency
- success/rejected/unknown/retryable outcome
- broker/provider correlation IDs
- rate-limit status
- schema/version errors
- circuit-breaker state
- reconciliation mismatch metrics

## Contract tests required when pyPSX sandbox is available

- account open/status mapping
- balances/positions exactness
- market session/quote timestamps
- order submit happy path
- invalid/rejected order
- duplicate idempotency/client reference
- timeout before send vs timeout after possible send
- query recovery from UNKNOWN
- partial fill
- fill de-duplication
- cancel/fill race
- cancel timeout
- modify/replace semantics if supported
- out-of-order/duplicate webhook
- rate limits
- malformed response
- expired/invalid credential
- funding/deposit mapping
- withdrawal mapping if supported
- reconciliation against broker snapshots

## Phase 0 information request checklist

Obtain from pyPSX before final mapping:
1. exact API/protocol and authentication method;
2. sandbox credentials/environment;
3. base URLs and versioning policy;
4. account/KYC schema;
5. broker legal entity/regulated role;
6. UIN/CDC/NCCPL account flow;
7. order types/time-in-force/status list;
8. client order ID/idempotency semantics;
9. timeout/retry guarantees;
10. execution/fill unique IDs;
11. webhook signature/order/retry behavior;
12. market-data timestamps/sequence/freshness;
13. balances/positions semantics;
14. funding/deposit/withdrawal flow;
15. fee/tax/settlement fields;
16. rate limits/SLA;
17. maintenance/outage behavior;
18. test certification requirements.
