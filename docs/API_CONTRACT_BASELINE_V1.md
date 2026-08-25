# API Contract Baseline V1

Status: Implementation-ready planning baseline for Klyvesta-owned APIs. pyPSX/broker API details remain external and unverified.

## Principles

- Server is authoritative for identity, ownership, entitlements, permissions, balances, market eligibility, risk/compliance and execution state.
- APIs are versioned and schema-first.
- Financial commands are idempotent.
- Financial decimals are encoded as strings to avoid JavaScript/JSON floating-point ambiguity.
- Timestamps use RFC 3339 / UTC.
- IDs are opaque UUIDv7 strings; IDs are not authorization secrets.
- Errors use stable machine-readable codes and safe human messages.
- Client UI state never proves a financial action occurred.

## Transport

- HTTPS only.
- TLS policy follows current platform/security requirements.
- HTTP/2 or HTTP/3 where supported operationally; correctness does not depend on transport version.
- Compression disabled/restricted for endpoints where secret-reflection side channels are relevant.

## API families

### Customer API
`/api/v1/...`

Used by Web BFF and native clients according to their authentication model.

### Admin API
Separate origin/application and policy:
`https://admin-api.../api/v1/...`

Not reachable through customer role/session.

### Internal service API
Private authenticated service boundary. No public routing by default.

### Broker Adapter
Internal contract only. pyPSX payloads are translated at the adapter boundary.

## Request metadata

For commands:
- `Idempotency-Key` required for financial mutations.
- `X-Request-Id` accepted/generated for observability.
- `traceparent` used for distributed tracing when supported.

Server stores normalized request hash with idempotency record. Same key + different payload => `409 IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_REQUEST`.

## Response metadata

- `X-Request-Id`
- resource `ETag` where optimistic concurrency is useful (mandates/preferences/etc.)
- rate-limit headers where policy allows revealing them

## Decimal representation

Example:

```json
{
  "currency": "PKR",
  "cashAvailable": "125000.50",
  "quantity": "100",
  "limitPrice": "42.35000000",
  "portfolioReturnRate": "0.071500000000"
}
```

Do not expose authoritative money as JSON number.

## Error envelope

Use RFC 9457 Problem Details-compatible responses with Klyvesta extensions:

```json
{
  "type": "https://errors.klyvesta.example/security/step-up-required",
  "title": "Additional verification required",
  "status": 403,
  "code": "STEP_UP_REQUIRED",
  "requestId": "...",
  "detail": "Verify your identity to continue.",
  "retryable": false
}
```

Never include stack traces, SQL, secrets, provider tokens or another customer's existence/details.

## Pagination

Cursor-based pagination for large mutable collections.

```text
GET /api/v1/orders?limit=50&cursor=opaque
```

Response:

```json
{
  "items": [],
  "nextCursor": "opaque-or-null"
}
```

Maximum limits enforced server-side.

## Optimistic concurrency

Versioned resources such as mandates use ETag/version.

```text
GET /api/v1/portfolios/{id}/mandate
ETag: "mandate-v7"

PUT ...
If-Match: "mandate-v7"
```

Mismatch => `412 PRECONDITION_FAILED` / stable domain code. High-risk mandate changes still require step-up and confirmation; ETag is not authorization.

## Customer endpoint baseline

### Identity/security

- `GET /api/v1/me`
- `GET /api/v1/security/sessions`
- `DELETE /api/v1/security/sessions/{sessionId}`
- `GET /api/v1/security/devices`
- `DELETE /api/v1/security/devices/{deviceId}`
- `POST /api/v1/security/step-up/challenge`
- passkey registration/authentication primarily handled through selected identity authority/BFF integration, not custom undocumented endpoints.

### Customer/onboarding

- `GET /api/v1/onboarding/status`
- `GET /api/v1/risk-profile`
- `POST /api/v1/risk-profile/assessments`
- `GET /api/v1/consents`
- `POST /api/v1/consents/{type}/accept`

KYC endpoint details depend on broker/approved provider flow and data minimization decision.

### Funding

- `GET /api/v1/funding/accounts`
- `GET /api/v1/deposits`
- `GET /api/v1/deposits/{id}`
- `GET /api/v1/beneficiaries`
- `POST /api/v1/beneficiaries` — step-up required
- `DELETE /api/v1/beneficiaries/{id}` — step-up required
- `POST /api/v1/withdrawals` — idempotency + step-up required
- `GET /api/v1/withdrawals/{id}`
- `GET /api/v1/withdrawals`

### Market

- `GET /api/v1/market/instruments`
- `GET /api/v1/market/instruments/{id}`
- `GET /api/v1/market/status`
- quote/streaming API defined separately after market-data rights/protocol are confirmed.

### Portfolio

- `GET /api/v1/portfolios`
- `GET /api/v1/portfolios/{id}`
- `GET /api/v1/portfolios/{id}/holdings`
- `GET /api/v1/portfolios/{id}/performance`
- `GET /api/v1/portfolios/{id}/risk`
- `GET /api/v1/portfolios/{id}/timeline`

### Mandate / Guarded Auto

- `GET /api/v1/portfolios/{id}/mandates/current`
- `POST /api/v1/portfolios/{id}/mandates` — create draft
- `POST /api/v1/portfolios/{id}/mandates/{mandateId}/confirm` — step-up, legal feature gate
- `POST /api/v1/portfolios/{id}/mandates/{mandateId}/pause`
- `POST /api/v1/portfolios/{id}/mandates/{mandateId}/resume` — may require step-up/re-evaluation
- `POST /api/v1/portfolios/{id}/mandates/{mandateId}/revoke`

Guarded Auto production endpoints remain feature-disabled until acceptance gates unlock them.

### AI recommendations

- `GET /api/v1/portfolios/{id}/recommendations`
- `GET /api/v1/recommendations/{id}`
- `POST /api/v1/recommendations/{id}/approve` — idempotency, version/hash validation, legal gate
- `POST /api/v1/recommendations/{id}/reject`
- `POST /api/v1/ai/conversations` / message endpoints may exist for assistant UX but cannot directly mutate financial state.

### Orders

- `POST /api/v1/orders/intents` — Manual order intent, idempotency required
- `GET /api/v1/orders`
- `GET /api/v1/orders/{id}`
- `POST /api/v1/orders/{id}/cancel` — idempotency required

The public client never posts `approved=true`, `riskPassed=true`, `balance`, `role`, `brokerOrderId`, or execution status as authoritative fields.

### Statements/reports

- `GET /api/v1/statements`
- `GET /api/v1/statements/{id}`
- time-limited authenticated download flow; URLs are not permanent public objects.

### Notifications

- `GET /api/v1/notifications/preferences`
- `PUT /api/v1/notifications/preferences`
- `GET /api/v1/notifications`

Mandatory security/compliance notifications cannot be disabled by ordinary preference.

## Command shape example — Manual OrderIntent

```json
{
  "portfolioId": "019...",
  "instrumentId": "019...",
  "side": "BUY",
  "orderType": "LIMIT",
  "quantity": "100",
  "limitPrice": "42.35000000",
  "timeInForce": "DAY",
  "clientContext": {
    "source": "MANUAL"
  }
}
```

Server resolves:
- customer/user identity,
- broker account,
- ownership,
- current market data,
- cash/position,
- entitlements,
- account/KYC state,
- policy/risk/compliance.

Response initially represents Klyvesta intent, not fabricated execution:

```json
{
  "orderIntentId": "019...",
  "state": "VALIDATING",
  "createdAt": "2026-08-25T14:00:00Z"
}
```

Later state is fetched/streamed from authoritative order model.

## Command shape example — Withdrawal

```json
{
  "portfolioId": "019...",
  "beneficiaryId": "019...",
  "amount": "50000.00",
  "currency": "PKR"
}
```

The client cannot choose internal approval/security state.

## Event/stream baseline

Customer real-time channel may deliver sanitized events:
- order state changed,
- fill received,
- portfolio projection updated,
- risk/Auto status changed,
- deposit/withdrawal state changed,
- notification/timeline update.

Streaming events are hints/projections. Clients must be able to refetch authoritative state after reconnect.

Event envelope:

```json
{
  "eventId": "019...",
  "type": "order.state.changed",
  "occurredAt": "...Z",
  "resourceId": "019...",
  "version": 7,
  "data": {}
}
```

Clients de-duplicate by event ID/version.

## Webhook ingress baseline

External broker/provider webhooks use dedicated endpoints, for example:
`POST /internal/webhooks/broker/{brokerCode}`

Mandatory processing:
1. authenticate/signature validate;
2. validate timestamp/nonce/replay window;
3. enforce body-size/content-type limits;
4. persist raw minimal evidence/hash according to policy;
5. schema validate;
6. de-duplicate event/external ID;
7. enqueue/process through idempotent domain handler;
8. return provider-appropriate response.

Provider-specific signature rules remain unverified until pyPSX docs arrive.

## BrokerAdapter internal contract

Klyvesta domain model depends on normalized operations, not pyPSX payload shapes:

```text
OpenAccount / GetAccountStatus
GetBalances
GetPositions
SubmitOrder
GetOrder
CancelOrder
ModifyOrReplaceOrder (only if supported)
ListExecutions
GetFundingStatus
RequestWithdrawal / GetWithdrawalStatus (only if broker contract supports)
GetStatements
Health/Capabilities
```

Every adapter response includes:
- normalized state,
- external identifiers,
- broker timestamp if available,
- observed-at timestamp,
- raw/provider correlation reference,
- capability/version metadata,
- explicit unknown/unsupported state rather than invented defaults.

## Idempotency response semantics

For same key + same normalized request:
- return original/current command resource and status;
- do not repeat financial side effect.

For same key + different request hash:
- reject 409.

Idempotency records retain enough lifetime to cover broker retry/reconciliation windows and applicable audit requirements.

## API inventory/security

Maintain generated OpenAPI inventory for every exposed endpoint.

Release checks:
- no undocumented public/admin endpoint;
- authentication requirement explicit;
- authorization policy explicit;
- request/response bounds explicit;
- sensitive fields classified/redacted;
- rate-limit policy explicit;
- financial POST/command idempotency classified;
- admin endpoints on separate plane;
- deprecated API has removal/migration plan.

## Testing requirements

Contract/integration tests cover:
- invalid/missing auth;
- own vs another customer's IDs;
- malformed decimals/overflow/precision;
- duplicate idempotency keys;
- replayed command;
- stale ETag/version;
- invalid state transition;
- rate limiting/abuse;
- broker timeout/UNKNOWN behavior;
- duplicate/out-of-order webhook;
- pagination bounds;
- response redaction;
- API schema backward compatibility.

## References verified 2026-08-25

- OpenID FAPI 2.0 Security Profile final.
- OAuth Security BCP as referenced by FAPI 2.0.
- ASP.NET Core 10 passkey, Data Protection and rate-limiting guidance.
- OWASP API Security Top 10 2023 / ASVS 5.0 from security audit.
