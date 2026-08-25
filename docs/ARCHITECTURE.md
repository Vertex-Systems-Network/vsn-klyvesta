# Architecture

## Architectural style

Start as a **modular monolith + isolated AI/quant services**, not premature microservices. Preserve explicit module/trust boundaries so high-load or security-sensitive components can be extracted later only when evidence justifies it.

See:
- `docs/PLATFORM_STACK_V2.md`
- `docs/SECURITY_ARCHITECTURE_AUDIT_V1.md`
- `docs/AUTHORIZATION_PRIVILEGE_MODEL.md`
- `docs/THREAT_MODEL.md`
- `docs/adr/ADR-002-PLATFORM-STACK-AND-NATIVE-CLIENTS.md`

## Approved implementation stack

### Clients
- Web: TypeScript + React/Next.js supported patched LTS
- Android: Kotlin + Jetpack Compose
- iOS: Swift + SwiftUI
- Windows: C# + .NET 10 LTS + WinUI 3, signed MSIX/MSIX bundle

All customer clients are untrusted. They present state and collect intent but never authoritatively decide balances, permissions, risk, compliance or executed-order state.

### Financial Core API
- C# / .NET 10 LTS / ASP.NET Core
- REST for commands/queries
- WebSocket/SSE for live customer updates where appropriate
- OpenAPI contracts
- modular-monolith boundaries around Identity/Customer, KYC, Funding, Ledger, OMS, Portfolio, Risk, Compliance, Reconciliation, Audit and Notifications

### AI / Quant
- Python 3.13 initially
- FastAPI or equivalent internal typed service boundary where needed
- pandas/polars/numpy/scipy and approved model libraries
- model registry
- agent orchestrator with typed tool contracts
- framework/provider replaceable behind internal interfaces

Python/AI cannot own authoritative ledger, permissions, final risk/compliance decisions or executed-order truth.

### Data
- PostgreSQL: transactional system of record
- fixed-precision numeric/decimal financial math; never floating-point money
- PostgreSQL outbox pattern for reliable domain event publication
- Redis: cache/locks/ephemeral state only; never financial source of truth
- S3-compatible object store for reports/model artifacts/evidence with retention controls
- Kafka/Redpanda/NATS only when measured scale/decoupling justifies it
- analytics warehouse later

### Observability
- OpenTelemetry
- structured metrics + logs + traces
- request/correlation IDs
- alerting
- audit/evidence pipeline separate from ordinary application logs
- no secrets or unnecessary PII in telemetry

### Secrets/Security
- KMS/HSM/Vault-class secret management
- broker/API secrets never exposed to LLM or customer clients
- separate production credentials and least-privilege workload identities
- mTLS/service identity where appropriate for sensitive internal boundaries
- signed webhooks + replay protection
- signed release artifacts

## Trust-zone architecture

```text
Website / Android / iOS / Windows
              |
        CDN / WAF / Edge
              |
       Identity / Session
              |
         API / BFF Layer
              |
+------------------------------------------------+
|            .NET Financial Core                 |
| Identity/Customer | KYC | Funding | Ledger     |
| OMS | Portfolio | Reconciliation | Audit       |
| Notifications | Entitlements                  |
+------------------------------------------------+
        |              |              |
        |              |              +--> Notification providers
        |              |
        |        AI / Quant Zone (Python)
        |              |
        |        proposals / research only
        |              |
        +--> Suitability Policy Engine
        +--> Risk Governor
        +--> Compliance Gate
        +--> Execution Validator
                         |
                  Broker Adapter
                         |
                       pyPSX
                         |
                Broker / PSX rails
```

Admin/Risk/Compliance interfaces are a separate high-trust plane with stronger authentication, restricted network/device posture and explicit role separation.

## Order authority boundary

Only `Execution Validator -> Broker Adapter` may create broker-side orders.

AI cannot bypass:
1. identity/account status,
2. authorization/resource ownership,
3. entitlement/product availability,
4. mandate,
5. suitability,
6. market/session/data freshness,
7. cash/position/reservation checks,
8. risk limits,
9. compliance policies,
10. security holds/device risk,
11. idempotency/order-state validation.

Manual mode may bypass AI recommendation but cannot bypass these hard controls.

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

`PyPSXBrokerAdapter` implements this contract once partner technical documentation is received.

No domain code should depend directly on pyPSX payload shapes.

If pyPSX ultimately requires a Python-only Broker SDK, use a narrow isolated Python connector behind an authenticated internal contract; do not move ledger/risk/authorization authority into that connector.

## Ledger principle

Use an immutable double-entry ledger for money movements. Derived balances may be cached but never treated as the accounting source of truth.

Historical ledger/fill records are never edited. Corrections use compensating/adjustment entries with actor, reason, approval and external reference.

## Authorization principle

Use server-side deny-by-default RBAC + resource ownership/ABAC + feature entitlement + risk/security context.

`subscription_tier` is not a security role.

See `docs/AUTHORIZATION_PRIVILEGE_MODEL.md`.

## Event model examples

- UserRegistered
- DeviceTrusted
- SecurityHoldPlaced
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
- ReconciliationExceptionRaised
- RebalanceTriggered
- RiskLimitBreached
- AutoModePaused
- WithdrawalRequested
- WithdrawalCompleted

Every material event carries correlation ID, actor/machine principal, timestamp, source, and applicable model/prompt/policy/mandate versions.
