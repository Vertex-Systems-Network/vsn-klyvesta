# Architecture

## Architectural style

Start as a **modular monolith + isolated AI/market services**, not premature microservices. Preserve service boundaries so high-load or regulated components can be extracted later.

## Proposed stack

### Clients
- Web: Next.js + TypeScript
- Mobile: React Native + TypeScript
- Admin: Next.js + TypeScript

### Core API
- TypeScript service layer (NestJS or equivalent)
- REST for commands/queries
- WebSocket/SSE for live updates
- OpenAPI contracts

### AI / Quant
- Python services
- FastAPI or equivalent
- pandas/polars/numpy/scipy for analysis
- model registry
- agent orchestrator with typed tool contracts
- framework must remain replaceable behind internal interfaces

### Data
- PostgreSQL: transactional system of record
- PostgreSQL/TimescaleDB or equivalent: time-series features if required
- Redis: cache, locks, short-lived state
- Kafka/Redpanda/NATS: durable event stream when scale requires it
- S3-compatible object store: reports/model artifacts
- analytics warehouse later

### Observability
- OpenTelemetry
- metrics + logs + traces
- alerting
- audit pipeline separate from ordinary application logs

### Secrets/Security
- KMS/HSM/Vault-class secret management
- broker/API secrets never exposed to LLM context
- separate production credentials and least-privilege service identities

## Logical components

```text
Web / Mobile / Admin
        |
   API Gateway/BFF
        |
+------------------------------+
| Identity / Customer          |
| KYC & Broker Onboarding      |
| Funding                      |
| Portfolio                    |
| Orders / OMS                 |
| Ledger                       |
| Notifications                |
+------------------------------+
        |
        +---- Market Data Service
        |
        +---- AI Orchestrator
        |       |
        |       +-- Research Agent
        |       +-- Portfolio Agent
        |       +-- Rebalance Agent
        |       +-- Explanation Agent
        |
        +---- Suitability Policy Engine
        +---- Risk Governor
        +---- Compliance Gate
        |
        +---- Execution Validator
        +---- pyPSX Broker Adapter
                         |
                       pyPSX
                         |
                  Broker / PSX rails
```

## Order authority boundary

Only `Execution Validator -> pyPSX Broker Adapter` may create broker-side orders.

AI cannot bypass:
1. customer/account status,
2. mandate,
3. suitability,
4. market state,
5. cash/position check,
6. risk limits,
7. compliance policies,
8. idempotency/order-state validation.

## Broker abstraction

```text
BrokerAdapter
- openAccount()
- getAccountStatus()
- getCash()
- getPositions()
- placeOrder()
- modifyOrder()
- cancelOrder()
- getOrders()
- getExecutions()
- getStatements()
- getFundingStatus()
```

`PyPSXBrokerAdapter` implements this contract.

No domain code should depend directly on pyPSX payload shapes.

## Ledger principle

Use an immutable double-entry ledger for money movements. Derived balances may be cached but never treated as the accounting source of truth.

## Event model examples
- UserRegistered
- KycSubmitted
- KycApproved
- BrokerAccountActivated
- DepositDetected
- DepositReconciled
- RiskProfileChanged
- MandateActivated
- RecommendationCreated
- RecommendationApproved
- OrderIntentCreated
- RiskCheckPassed
- ComplianceCheckPassed
- OrderSubmitted
- OrderAccepted
- OrderPartiallyFilled
- OrderFilled
- OrderRejected
- PositionChanged
- RebalanceTriggered
- RiskLimitBreached
- AutoModePaused
- WithdrawalRequested
- WithdrawalCompleted

Every material event carries correlation ID, actor, timestamp, policy/model version, and source.
