# Data Flow

## 1. Onboarding

```text
User
 -> Klyvesta Identity
 -> KYC request
 -> pyPSX/Broker onboarding
 -> broker/CDC/NCCPL-related status returned
 -> Klyvesta stores reference + status
 -> suitability questionnaire
 -> risk profile
 -> account activation
```

Klyvesta should store only the minimum required regulated/PII data and tokenize/reference external identifiers where practical.

## 2. Funding

```text
User
 -> approved broker funding rail
 -> broker/custody account
 -> broker funding event/API
 -> Klyvesta reconciliation
 -> ledger reference
 -> available investing cash
```

Customer investment money must not be inferred from ordinary platform-company bank balance.

## 3. AI Assisted Investment

```text
User goal + risk profile
 -> Investor Understanding Agent
 -> eligible investment universe
 -> market/fundamental data
 -> signal/features
 -> portfolio constructor
 -> Risk Governor
 -> Compliance Gate
 -> recommendation
 -> user confirmation
 -> Order Intents
 -> Execution Validator
 -> pyPSX Adapter
 -> broker
 -> fills
 -> reconciliation
 -> holdings/ledger
 -> explanation + notification
```

## 4. Guarded Auto

```text
Active Mandate
 + reconciled cash
 + fresh market state
 + model signals
 -> portfolio target
 -> delta/rebalance calculation
 -> risk checks
 -> compliance checks
 -> order plan
 -> execution validator
 -> pyPSX
 -> fills
 -> reconciliation
 -> continuous monitoring
```

If any required input is stale/unknown, the safe default is **do not trade**.

## 5. Manual

```text
User Order
 -> normalize/validate
 -> account and market checks
 -> risk/compliance checks
 -> explicit confirmation where required
 -> pyPSX adapter
 -> broker
 -> fills/reconciliation
```

## 6. AI evidence flow

AI recommendations must reference:
- source IDs
- data timestamps
- feature/model version
- portfolio state version
- mandate version
- risk policy version
- compliance policy version

The user-facing explanation may be simplified, but the internal evidence chain cannot be discarded.

## 7. Reconciliation

Broker state is external truth for execution/custody; Klyvesta ledger is internal truth for platform accounting references.

Reconciliation compares:
- submitted orders
- broker order status
- executions/fills
- positions
- cash
- settlement
- deposits/withdrawals

Any mismatch enters an exception queue. Auto trading for affected account may be paused based on severity.
