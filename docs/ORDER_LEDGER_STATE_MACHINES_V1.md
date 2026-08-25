# Order, Ledger & Withdrawal State Machines V1

Status: Implementation-ready planning baseline. Broker-specific status mapping remains unverified until pyPSX partner documentation arrives.

## Design principle

External dependencies can time out, duplicate, reorder, partially succeed or return ambiguous states. Financial workflows therefore use explicit state machines, immutable events, idempotency, reservations and reconciliation.

No code may infer success from HTTP status alone when external execution/funding truth is still uncertain.

---

# 1. Order intent state machine

An `OrderIntent` represents Klyvesta's authorized business intent before broker execution.

Sources:
- manual customer command,
- approved AI-assisted recommendation,
- approved Guarded Auto rebalance/recurring allocation.

States:

```text
CREATED
  -> VALIDATING
      -> REJECTED
      -> APPROVED
          -> EXECUTION_PENDING
              -> EXECUTION_CREATED
              -> CANCELLED
              -> EXPIRED
```

### CREATED
Normalized request is persisted with:
- source,
- resource ownership,
- portfolio/account,
- quantity/value/order type,
- market data reference/time,
- relevant mandate/profile/recommendation versions,
- idempotency key,
- request hash.

No cash/position side effect yet.

### VALIDATING
Deterministic checks include:
- identity/session/resource authorization,
- account/KYC/compliance state,
- entitlement/legal availability,
- mandate/suitability where applicable,
- market session/freshness,
- quantity/lot/price rules,
- cash/position availability,
- hard risk policy,
- duplicate/idempotency check.

### REJECTED
Terminal. Stores stable reason codes and policy versions. No broker call.

### APPROVED
Required cash/position reservation exists atomically with approval.

### EXECUTION_PENDING
Eligible for execution worker. Re-check time-sensitive invariants immediately before broker submission.

### EXECUTION_CREATED
BrokerOrder exists. OrderIntent business intent cannot be reused to create another broker order except through an explicit replace/modify workflow.

### CANCELLED / EXPIRED
Terminal before broker submission. Reservation is released transactionally.

---

# 2. Broker order state machine

Broker-specific states are mapped into the following normalized model:

```text
PENDING_SUBMIT
  -> SUBMITTING
      -> SUBMITTED
          -> OPEN
              -> PARTIALLY_FILLED
                  -> FILLED
                  -> CANCEL_PENDING -> CANCELLED
              -> FILLED
              -> CANCEL_PENDING -> CANCELLED
          -> REJECTED
      -> UNKNOWN
```

Additional terminal/operational states may be added only with documented mapping.

### PENDING_SUBMIT
Internal order record and external idempotency/reference are persisted before the external call.

### SUBMITTING
At most one execution lease/worker owns submission. Crash/restart recovery must not blindly resubmit.

### SUBMITTED
Broker accepted the request at transport/API level and provided enough identity/reference to query it.

### OPEN
Broker reports active/working order.

### PARTIALLY_FILLED
At least one immutable execution received; remaining quantity still live/unknown according to broker state.

### FILLED
Cumulative confirmed executions satisfy final quantity/state.

### CANCEL_PENDING
Cancel request accepted/issued but final broker state unresolved. Original order may still fill during cancel race.

### CANCELLED
Broker confirms no remaining live quantity. Existing fills remain immutable.

### REJECTED
Broker conclusively rejected order. Release unused reservation.

### UNKNOWN
Critical safety state used after timeout, malformed response, contradictory broker state or lost acknowledgement where execution may have occurred.

Rules for UNKNOWN:
- do not blindly resubmit;
- do not release reservation as if rejected;
- query broker by external/client reference;
- reconcile orders/executions/positions/cash;
- pause affected Auto account when risk threshold requires;
- only leave UNKNOWN based on evidence.

---

# 3. Execution/fill processing

Every fill is immutable and de-duplicated by broker + external execution ID (or documented equivalent composite identity).

Processing transaction:
1. validate broker source/authenticity/schema;
2. resolve internal broker order;
3. enforce unique execution identity;
4. append execution;
5. consume/reprice reservation as appropriate;
6. post related ledger journal(s) according to trade/settlement model;
7. update position projection;
8. append audit/domain event;
9. enqueue user timeline/notification outbox if material;
10. commit atomically.

Duplicate fill events return idempotent success with no second financial effect.

Out-of-order fills/order-status events update projections only through version/time/sequence-safe logic; reconciliation is the final external consistency check.

---

# 4. Modify/replace orders

Do not mutate an already-submitted order request in-place as if history changed.

Use explicit workflow:

```text
Existing broker order
 -> ReplaceIntent/ModifyIntent
 -> validate new terms
 -> broker modify/replace operation
 -> new broker version/reference if broker semantics require
```

Maintain complete before/after audit and external references.

If broker implements cancel-replace, model the race explicitly; a fill may arrive between cancel and replacement.

---

# 5. Cash/position reservation state machine

```text
ACTIVE
 -> PARTIALLY_CONSUMED
 -> CONSUMED
 -> RELEASED
 -> EXPIRED
```

Rules:
- reservation creation is atomic with approved OrderIntent;
- reservation amount/quantity cannot go negative;
- only executions consume reservation;
- rejection/cancellation releases unused reservation after broker state is conclusive;
- UNKNOWN order keeps required reservation/hold until reconciliation policy resolves ambiguity;
- every transition is reason-coded and auditable.

---

# 6. Ledger journal lifecycle

Ledger entries are append-only financial facts.

```text
DRAFT/BUILDING (inside transaction only)
 -> POSTED
```

Persisted business state should normally expose only valid POSTED journals; partially constructed entries must not survive transaction failure.

POSTED invariant:
- debits == credits per currency;
- all accounts/currency relationships valid;
- external/domain source reference present;
- immutable thereafter.

Correction:

```text
Incorrect POSTED entry
 -> REVERSAL entry (new POSTED journal)
 -> optional corrected POSTED entry
```

Never UPDATE/DELETE historical posted lines to hide a mistake.

---

# 7. Deposit state machine

Broker/funding-specific mapping remains provisional.

```text
EXPECTED/INITIATED
 -> DETECTED
 -> RECONCILING
     -> RECONCILED
     -> MISMATCHED
     -> REJECTED/FAILED
```

Only RECONCILED funds become investable according to approved settlement/funding rules.

Duplicate external funding events do not create duplicate cash.

---

# 8. Beneficiary state machine

```text
PENDING_VERIFICATION
 -> VERIFIED_COOLING_OFF
 -> ACTIVE
 -> BLOCKED
 -> REVOKED
```

Sensitive beneficiary change creates a new version and restarts verification/cool-off. Historical withdrawal references continue to point to the exact beneficiary version used.

---

# 9. Withdrawal state machine

```text
REQUESTED
 -> SECURITY_CHECK
     -> SECURITY_HOLD
     -> POLICY_CHECK
         -> REJECTED
         -> APPROVAL_PENDING (when thresholds/policy require)
             -> APPROVED
         -> APPROVED
             -> SUBMISSION_PENDING
                 -> SUBMITTED
                     -> PROCESSING
                         -> COMPLETED
                         -> FAILED
                     -> UNKNOWN
```

Customer may cancel only in explicitly permitted pre-submission states.

### SECURITY_CHECK
Verify:
- fresh required step-up;
- session/device/recovery state;
- beneficiary active and outside cool-off;
- velocity/anomaly/fraud rules;
- account ownership/status.

### POLICY_CHECK
Verify:
- sufficient withdrawable cash distinct from reserved/unsettled amount;
- KYC/AML/compliance state;
- daily/period limits;
- broker/funding availability;
- required approvals.

### SECURITY_HOLD
No submission. Requires documented resolution; support cannot bypass.

### APPROVAL_PENDING
Thresholded/internal exception path uses maker-checker. Approver cannot equal requester/maker.

### SUBMITTED/PROCESSING
External payment/broker operation is in flight. Do not duplicate on timeout.

### UNKNOWN
External withdrawal may or may not have processed. Freeze duplicate retry and reconcile first.

### COMPLETED
Conclusive external and internal evidence recorded; related ledger entries/reconciliation complete.

### FAILED
Conclusive failure; any reserved/held cash released according to documented rules.

---

# 10. Mandate state machine

```text
DRAFT
 -> PENDING_CONFIRMATION
 -> ACTIVE
     -> PAUSED_BY_USER
     -> PAUSED_BY_POLICY
     -> PAUSED_BY_SECURITY
     -> SUPERSEDED
     -> REVOKED
     -> EXPIRED
```

Rules:
- activated mandate version is immutable;
- material change creates a new version and confirmation;
- higher-risk change requires fresh step-up and applicable cool-off/policy;
- Auto order intent must reference exactly one ACTIVE mandate version at decision time;
- any configured kill switch prevents new Auto OrderIntents immediately;
- pause does not falsify or delete previously submitted broker orders; those must be managed/reconciled explicitly.

---

# 11. Recommendation state machine

```text
CREATED
 -> PRESENTED
     -> APPROVED
     -> REJECTED
     -> EXPIRED
     -> SUPERSEDED
```

Approval requirements:
- recommendation belongs to customer/portfolio;
- evidence/portfolio/mandate inputs are still within freshness/change rules;
- exact recommendation version/hash approved;
- legal feature available;
- customer has required entitlement;
- approval occurs before expiry.

An approved recommendation still goes through OrderIntent validation; recommendation approval is not direct broker authorization.

---

# 12. Reconciliation break state machine

```text
OPEN
 -> INVESTIGATING
     -> RESOLVED_NO_ADJUSTMENT
     -> ADJUSTMENT_PROPOSED -> ADJUSTMENT_APPROVED -> RESOLVED
     -> ESCALATED
```

Critical breaks may trigger:
- account freeze,
- Auto pause,
- withdrawal hold,
- global circuit breaker,

depending on category/severity.

---

# 13. Retry policy

Retries are safe only when operation semantics are known.

Automatic retry allowed for:
- idempotent reads;
- explicitly idempotent broker commands with verified broker semantics/reference;
- internal outbox delivery with unique delivery IDs.

Automatic blind retry forbidden for ambiguous financial commands where first attempt may have succeeded.

Use bounded exponential backoff + jitter for retryable dependency failures. Respect broker/provider rate limits.

---

# 14. State transition enforcement

Each aggregate exposes explicit transition methods; generic `status = X` mutations are forbidden outside migration/approved repair tooling.

Tests must verify:
- every allowed transition;
- every forbidden transition;
- duplicate events;
- concurrent cancel/fill;
- timeout to UNKNOWN;
- recovery from UNKNOWN through reconciliation;
- crash between internal commit and external call using outbox/worker design;
- crash after external success before local acknowledgement;
- maker-checker separation;
- reservation/ledger invariants.

## pyPSX dependency

Before production implementation freeze, map every pyPSX/broker:
- order status,
- cancel/replace response,
- execution ID,
- timeout/retry guarantee,
- idempotency/client reference behavior,
- webhook ordering/duplication guarantee,
- settlement/funding state,

into this normalized model. Any unmappable ambiguity is a Phase 0/P2 blocker, not something to guess around.
