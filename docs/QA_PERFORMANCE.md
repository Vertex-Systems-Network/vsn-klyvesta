# QA, Model Validation & Performance

## Quality gates

### Unit
- money calculations
- fees/taxes
- order state transitions
- mandate rules
- risk rules
- allocation rounding
- lot-size handling
- ledger entries

### Property-based / invariant
Examples:
- ledger debits = credits
- holdings cannot change without an execution/corporate-action event
- available cash cannot exceed reconciled cash under defined rules
- Auto order cannot exceed mandate
- duplicate idempotency key cannot create second order

### Integration
- pyPSX sandbox
- KYC/account opening
- deposits/funding
- order placement
- cancel/modify
- fills/partial fills
- positions
- statements
- webhooks/polling fallback

### Contract
Pin pyPSX schemas behind the BrokerAdapter and run automated provider/consumer contract tests.

### E2E
- register -> KYC -> fund -> invest -> fill -> portfolio -> statement
- failed KYC
- rejected order
- partial fill
- market closed
- stale data
- broker unavailable
- deposit mismatch
- withdrawal
- account freeze
- Auto pause

### Security
- auth bypass
- IDOR
- privilege escalation
- replay
- webhook forgery
- prompt injection
- secret exfiltration
- PII leakage
- SSRF/tool abuse
- rate limiting
- session hijack

## Quant/model validation

Never accept a strategy because a single backtest is profitable.

Required:
- out-of-sample testing
- walk-forward testing
- realistic transaction costs
- slippage
- taxes/fees where applicable
- lot sizes
- liquidity constraints
- market closures
- corporate actions
- survivorship bias controls
- look-ahead leakage tests
- benchmark comparison
- stress periods
- sensitivity analysis

Track:
- CAGR/return
- volatility
- Sharpe/Sortino where meaningful
- max drawdown
- downside deviation
- turnover
- hit rate
- tail loss
- concentration
- liquidity

## AI evaluation

Golden datasets + adversarial tests.

Zero-tolerance classes:
- unauthorized trade intent execution
- mandate breach execution
- fabricated executed order
- fabricated balance
- fabricated price presented as live
- policy/tool-permission bypass
- secret exposure

Measure:
- recommendation schema validity
- evidence correctness
- citation/evidence coverage
- explanation consistency with structured decision
- risk-label accuracy
- refusal/abstention when data is stale or insufficient

## Performance SLO targets

Initial targets, excluding external broker latency unless stated:

- Auth/API read p95: <= 300 ms
- Portfolio cached read p95: <= 500 ms
- Order-intent validation p95: <= 150 ms
- Deterministic risk gate p95: <= 50 ms
- Internal event propagation p95: <= 500 ms
- Live-market UI update: target <= 2 s from received source event
- Reconnect/recovery for streaming client: <= 5 s under normal transient failure
- AI explanation: target <= 5 s; never block execution-state correctness on an LLM response

Availability targets:
- read/dashboard path: 99.95% target
- trading command path: 99.9% target initially
- broker availability measured separately

## Load profile

MVP load test:
- 5,000 registered users
- 500 concurrent active users
- burst order submission tests
- market-open traffic spike
- reconnect storm
- portfolio refresh storm

Scale test:
- 50,000+ users
- 5,000 concurrent
- 10x expected order burst
- degraded broker response
- delayed market data

## Resilience

Must test:
- broker timeout
- broker returns 500
- duplicate webhook
- webhook out of order
- delayed fill
- lost WebSocket
- Redis unavailable
- AI provider unavailable
- model timeout
- partial DB failover
- reconciliation mismatch

Safe degradation:
AI failure must not corrupt portfolio/accounting state. Trading can fall back to manual or pause; financial truth must remain intact.

## Executable performance contract

The `parallel/performance-resilience` lane owns a **non-live performance contract**, not production performance claims.

Machine-readable evidence lives under `perf/`:
- `load-profile.json` defines deterministic warm-up, MVP steady-state, market-open burst, reconnect-storm and scale-gate inputs;
- `failure-injection.json` defines dependency failures and the required fail-safe behavior;
- `performance-baseline.json` mirrors the initial SLO/availability contract and explicitly marks it as **not a production measurement**.

`tools/Klyvesta.PerformanceVerifier` validates these files in canonical CI. It does not generate traffic, call a broker, contact a production endpoint, access customer data or claim measured latency/availability.

Promotion from this contract to real performance acceptance requires separately captured environment evidence with:
- exact source SHA and deployment identity;
- runner/load-generator identity;
- start/end time and workload profile hash;
- p50/p95/p99 latency plus error rate;
- saturation/resource telemetry;
- injected fault and recovery evidence;
- broker/external-provider latency reported separately;
- explicit statement of whether financial command paths remained fail-closed.

A green contract verifier means the test plan and thresholds are reproducible. It does **not** mean the product has achieved those SLOs in production.
