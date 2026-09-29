# ANPOS Batch 1 — PyPSX Documentation Reconciliation

Date: 2026-09-30 (Asia/Karachi)
Repository baseline: `7cbf8c6db4ded3c028d641000d5c710f83f25485`
PyPSX document: `pypsx-broker-api-documentation.md` (1,202 lines; read in full from Library)
Internal references: `contracts/broker/BROKER_ADAPTER_V1.md`, `contracts/openapi/klyvesta.v1.yaml`, `docs/P0_PYPSX_OPERATING_MODEL_DUE_DILIGENCE.md`
Raw partner documentation is not copied into this public repository.

## Classification

- **D** = documented by supplied PyPSX documentation.
- **K** = required or represented by Klyvesta internal contract.
- **T** = tested by an executed sandbox/automated test.
- **C** = contractually confirmed by current authoritative partner evidence.
- **L** = legally/regulatorily confirmed.
- **U** = unknown or not proven.

The supplied document provides D evidence only. It does not provide T, C or L evidence.

## Endpoint and capability reconciliation

| Surface | Documented endpoints/capabilities | Klyvesta mapping | Evidence | Required disposition |
| --- | --- | --- | --- | --- |
| Portal auth | `POST /v1/partner/auth/login`, `/register`; portal JWT | Management authority is separate from trading adapter | D, K; T/C/L = U | Confirm token TTL, scopes, rotation, owner/member model |
| Trading keys | `POST/GET/DELETE /v1/partner/keys`; key ID + secret headers | Server-side scoped credentials; no client/AI exposure | D, K; T/C = U | Confirm secret issuance, scopes, revocation and environment isolation |
| Effective config | `GET /v1/partner-api/config`; config version, fees, limits | Fee/tax values must be evidence-backed before ledger mapping | D, K; T/C/L = U | Verify rounding, version invalidation, live vs sandbox semantics |
| Accounts | `POST/GET /v1/partner-api/accounts`, `GET .../{id}` | `OpenAccount`, `GetAccountStatus`, idempotent account references | D, K; T/C/L = U | Map exact status, duplicate and account-number semantics |
| Portfolio | `GET .../{id}/portfolio`, `/positions` | Exact decimal balances/positions with observed timestamps | D, K; T/C = U | Confirm reserved/available/settled/unsettled/cost-basis meanings |
| Fees/taxes | `GET /v1/partner-api/fees`; advance fee, commission, SST, CGT | Known fee meanings only; reconciliation required | D, K; C/L = U | Confirm live schedule, tax responsibility, rounding and settlement |
| KYC credentials | Separate KYC keys; KYC calls real CDC portal with no CDC sandbox | KYC is independent high-risk provider boundary | D, K; T/C/L = U | Written KYC RACI, PII region/retention/deletion, production approval |
| KYC credentials/link | `POST .../credentials/link`, job polling and state transitions | No live PII or CDC call allowed from current code path | D; K boundary exists; T/C/L = U | Confirm CDC contract, job retry/replay, consent and data lifecycle |
| KYC registration/sign-in | Registration, OTP, password reset, identity/biometric, form sections | Must remain outside sandbox trading assumption | D; T/C/L = U | Provider certification and legal/privacy approval |
| KYC submission/status | `POST .../applications/{id}/submit`, `GET .../{id}`, catalogs/rules | Adapter contract has onboarding placeholder, not CDC implementation | D, K partial; T/C/L = U | Exact schemas, document storage, retention and rejection mapping |
| Orders | `POST/GET /v1/partner-api/orders`, `GET .../{id}`, `DELETE .../{id}` | Submit/Get/Cancel with UNKNOWN and reconciliation on ambiguity | D, K; T/C = U | Map complete statuses, timeout, idempotency, cancel/fill race |
| Order types | MARKET, LIMIT, STOP, STOP_LIMIT, STOP_LOSS, TAKE_PROFIT; SIMPLE/BRACKET/OCO | Internal normalized order types require explicit support map | D, K partial; T/C = U | Confirm time-in-force, modify/replace and conditional semantics |
| Fills | Partial fills, VWAP/order-book fields, fill source/levels | Stable execution de-duplication identity required | D, K; T/C = U | Confirm unique execution ID, correction/bust, ordering, settlement |
| Market quotes | `GET /market/quote/{symbol}`, `/market/quotes` | Quote source/observed timestamps and freshness policy | D, K; T/C = U | Confirm source, timestamp, market session and redistribution rights |
| Market depth | `GET /market/depth/{symbol}` | Level-2 data requires source/freshness/rights evidence | D, K; C/L = U | Confirm live feed ownership, fallback and redistribution terms |
| Historical/reference data | `/market/klines`, `/instruments`, `/indices`, `/sectors` | Market-data adapter scope not yet selected | D; K partial; C/L = U | Confirm data licensing, symbol mapping, retention and freshness |
| SSE streaming | `GET /v1/partner-api/stream?channels=prices,fills`; ready/price/fill events; probe mode | Adapter requires event auth, replay, ordering and reconnect controls | D, K; T/C = U | Obtain event schema, heartbeat, retry, replay and signing contract |
| Statements | `GET .../accounts/{id}/statement`; reports endpoints | Statement integrity and ledger reconciliation required | D, K; T/C = U | Verify equation, timezone, pagination, retention and correction behavior |
| Funding/deposits | Sandbox balance claims; live rails not established | Funding only after approved reconciliation rule | D partial, K gate; C/L = U | Obtain custody, deposit, reversal and reconciliation contract |
| Withdrawals | No sufficient live withdrawal authority established | Withdrawal remains independently fail-closed | K gate; D/C/L = U | Obtain beneficiary, approval, idempotency, reversal and custody evidence |
| Team/admin | `/v1/partner/team`, invite/remove; owner immutable | Operational access requires least privilege/audit evidence | D, K security boundary; T/C = U | Confirm roles, offboarding, audit logs and support access |
| Config/network | Sandbox config, allowed origins, IP allowlist, app authorizations | Backend-only operations; no secrets in browser | D, K; T/C = U | Confirm admin authorization, wildcard/CIDR semantics and auditability |
| Support/reports | Support tickets and tenant-scoped full reports | Not part of broker financial execution contract | D; K = not mapped; T/C = U | Define whether these are operational-only or required |
| Live feed | Document mentions LIVE_FEED with AhleTrade and fallback | No production data provider accepted in Klyvesta | D; C/L = U | Resolve provider identity, rights, SLA and fallback safety |
| Errors/rate limits | Stable error codes, 401/403/404/409/422/429/5xx, Retry-After; default 300 rpm/key | Adapter retry classification and fail-closed behavior | D, K; T/C = U | Execute negative/rate-limit tests after sandbox access |
| Production/live | “Sandbox now, live later”; key swap claim | Production authority explicitly disabled | D only; C/L = U | Resolve Issue #20 with direct partner evidence |

## Internal contract comparison

Klyvesta's `BROKER_ADAPTER_V1.md` already requires capability discovery, environment identity, idempotency, normalized states, UNKNOWN handling, execution de-duplication, market-data timestamps, webhook verification, secret isolation, reconciliation and sandbox contract tests. The PyPSX document supplies useful candidate mappings but does not prove those requirements.

Klyvesta's customer OpenAPI is a planning contract for Klyvesta-owned APIs, not a PyPSX Broker API schema. It must not be treated as a generated provider client contract.

## Confirmed contradictions/risks

1. The document says KYC always uses the real CDC portal and has no CDC sandbox; therefore trading sandbox does not make KYC safe for test PII.
2. “API contract does not change when live” is a partner claim, not a contractual guarantee until written agreement/version policy is obtained.
3. SSE documentation describes an internal algo feed and future direct broker-feed primary; source ownership, continuity and redistribution rights remain unknown.
4. The document describes live PSX/AhleTrade feed behavior and sandbox synthetic behavior; the exact provider and fallback semantics are not accepted by Klyvesta.
5. Fee/tax examples and default rates are documented values, not legal/tax advice or production accounting authority.
6. Funding, custody, withdrawals, customer legal relationship, licensed broker identity and production approval remain unresolved.

## Batch 1 exit status

**COMPLETE — documentation reconciliation completed.**

This means the full supplied document was reviewed, endpoint/capability claims were mapped to the internal broker contract, and every unproven claim was classified as unknown. It does not mean sandbox access, partner contract, legal approval, production readiness or live integration is complete.

## Next batch

Batch 2 — Partner and regulatory gate. Obtain direct written evidence for Issue #20 and update the matrix from D/U to T/C/L only where evidence supports it. No live adapter or KYC implementation should start from documentation alone.
