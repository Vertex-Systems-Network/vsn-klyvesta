# ANPOS Batch 1 — PyPSX Documentation Reconciliation

Date: 2026-09-30 (Asia/Karachi)
Repository baseline: `7cbf8c6db4ded3c028d641000d5c710f83f25485`
Source documentation: Library file `pypsx-broker-api-documentation.md`, read on 2026-09-30
Raw partner documentation is not copied into this public repository.

## Status vocabulary

- **Documented:** stated in the supplied PyPSX documentation.
- **Repository contract:** required by Klyvesta's internal adapter/OpenAPI contract.
- **Tested:** verified by an executed sandbox or deterministic automated test.
- **Contractually confirmed:** confirmed by a current authoritative partner agreement/response.
- **Legally confirmed:** confirmed by appropriate legal/regulatory evidence.
- **Unknown:** not proven by the available evidence.

## Reconciliation matrix

| Capability/claim | PyPSX documentation | Klyvesta contract alignment | Evidence status | Required next action |
| --- | --- | --- | --- | --- |
| Sandbox base URL | `https://brokerapi.paper.pypsx.com` | Adapter requires environment identity and endpoint evidence. | Documented only | Obtain credentials and run non-live connectivity check. |
| Trading authentication | Org key ID + secret headers | Adapter requires scoped, rotated server-side credentials. | Documented; not tested | Confirm scopes, rotation, expiry and secret-storage path. |
| Portal management auth | Separate portal JWT | Adapter separates management from trading authority. | Documented; not tested | Confirm JWT lifetime, scopes and operational ownership. |
| CDC KYC auth | Separate KYC keys; real CDC portal, no CDC sandbox | Klyvesta treats KYC/live PII as independently gated. | Documented; not contractually/legal confirmed | Obtain written KYC responsibility, retention, region and production approval. |
| Tenant isolation | Partner-scoped accounts and 404 cross-tenant behavior | Adapter requires account ownership and normalized authorization boundaries. | Documented; not tested | Negative cross-tenant tests in approved sandbox. |
| Accounts | Create/list/retrieve sub-accounts; sandbox funded ACTIVE | Adapter requires normalized account states and idempotency. | Documented; not tested | Map exact statuses, duplicate semantics and account identifiers. |
| Portfolio/positions | Cash, equity, positions and reserved cash | Adapter forbids mapping unknown fields to available cash. | Partial alignment | Confirm settled/unsettled/reserved semantics and decimal precision. |
| Fees/taxes | Advance fee, commission, SST and CGT formulas; effective config endpoint | Klyvesta ledger requires known fee/tax meaning and reconciliation. | Documented; not contractually confirmed | Confirm live/sandbox differences, rounding and settlement ownership. |
| Orders | Market/limit/conditional/bracket/OCO; pending/partial/fill/cancel states | Adapter explicitly models partial fills, UNKNOWN and cancel races. | Partial alignment | Map complete status/error enum and client idempotency behavior. |
| Execution/fills | Order-book/VWAP claims, fill IDs/levels and slippage fields | Adapter requires stable execution de-duplication identity. | Documented; not tested | Confirm unique execution ID, correction/bust and out-of-order behavior. |
| Streaming/events | Documentation references SSE/streaming and event updates | Adapter requires auth/signature, replay and ordering controls. | Documented; not contractually confirmed | Obtain stream schema, heartbeat, reconnect, replay and signing contract. |
| Rate limits | Default 300 requests/minute per key and Retry-After | Adapter records rate-limit status and retry classification. | Documented; not tested | Confirm contractual limit, burst behavior and production values. |
| Statements | Paginated orders and account statements | Adapter supports statements with integrity metadata. | Documented; not tested | Verify ledger equation, timezone, pagination and retention semantics. |
| Funding/withdrawals | Documentation is not sufficient for live rails/ownership | Adapter keeps funding/withdrawal gated. | Unknown | Obtain rail, custody, beneficiary, approval and reversal contract. |
| Production/live | Docs say sandbox now/live later and key swap | Klyvesta requires explicit production authority and partner evidence. | Unknown / blocked | Resolve Issue #20 with written rollout and legal/commercial evidence. |

## Security and privacy observations

1. Trading keys, portal JWTs and CDC KYC keys must remain separate and backend-only.
2. KYC documentation describes real CDC interaction even when the trading environment is sandbox; sandbox KYC must not be assumed.
3. API documentation is not proof of licence, custody, customer relationship, production approval, market-data redistribution rights, SLA or commercial terms.
4. Order timeout after possible submission must map to `UNKNOWN`; blind retry is unsafe.
5. Partial fills and cancellation races require reconciliation before releasing reservations.
6. Raw partner documentation remains outside this public repository unless licensing/confidentiality permits sanitized excerpts.

## Batch 1 acceptance

**PARTIALLY COMPLETE — reconciliation baseline recorded.**

The matrix is an evidence classification, not sandbox test evidence. Batch 1 can close only after repository mapping is complete for all relevant endpoints and contradictions/unknowns are dispositioned. Batch 2 remains blocked on partner/regulatory/commercial evidence.

## Next action

Complete the remaining endpoint-level mapping, then request/record direct partner answers for Issue #20. Do not implement a live adapter from this document alone.
