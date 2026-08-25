# Domain & Database Model V1

Status: Implementation-ready planning baseline. This document does not unlock real-money trading and does not replace pyPSX partner contract requirements.

## Goals

Design a PostgreSQL transactional model that preserves financial correctness, customer isolation, auditability, reconciliation, reversibility through compensating events, and safe evolution.

## Database baseline

- PostgreSQL 18.x supported patched release.
- Primary keys: UUIDv7, generated application-side with `.NET Guid.CreateVersion7()` unless a migration/import path requires database generation.
- Timestamps: `timestamptz`, stored/handled as UTC.
- Money/prices/quantities: exact `numeric`, never binary floating point.
- Application financial math: C# `decimal`.
- Status values: constrained text/lookup values, not unconstrained strings.
- Critical foreign keys, uniqueness and check constraints enforced in PostgreSQL.
- Redis is never source of financial truth.
- Historical financial/audit facts are append-only; corrections use compensating/reversal records.

## Precision policy

Initial physical types:

- cash amount: `numeric(24,8)`
- price: `numeric(24,8)`
- quantity: `numeric(24,8)`
- rates/percentages: `numeric(20,12)`

Instrument/currency metadata defines permitted display/settlement precision. The API transmits financial decimals as strings.

Final precisions must be validated against pyPSX/broker field limits before production migrations are frozen.

## Schema boundaries

Use PostgreSQL schemas to make ownership and review boundaries explicit:

- `identity` — application identity references, devices, sessions, recovery/security state.
- `customer` — customer profile, contacts, KYC references, suitability/risk profile, consents.
- `broker` — broker accounts, external identifiers, integration state.
- `funding` — funding sources, deposits, beneficiaries, withdrawals.
- `ledger` — chart of accounts, journals, journal lines, reservations.
- `market` — instruments, sessions, reference metadata, licensed market snapshots where retained.
- `trading` — order intents, broker orders, executions/fills, reservation linkage.
- `portfolio` — portfolios, mandates, target allocations, snapshots and performance projections.
- `ai` — model versions, decision runs, evidence references, recommendations and structured proposals.
- `notification` — user timeline projections, outbox and delivery attempts.
- `ops` — reconciliation runs/breaks, incidents, feature flags, operational controls.
- `audit` — immutable security/compliance/privileged action evidence.

Schemas are organizational boundaries, not a substitute for application authorization.

## Core entity model

### Identity

`identity.app_user`
- `id uuid primary key`
- `auth_subject text unique not null` — stable external/central identity subject
- `status text not null`
- `created_at timestamptz not null`
- `disabled_at timestamptz null`

`identity.device`
- `id uuid primary key`
- `user_id uuid not null`
- `platform text not null`
- `display_name text null`
- `device_public_key_ref text null`
- `trust_state text not null`
- `attestation_state text null`
- `first_seen_at`, `last_seen_at`
- `revoked_at`

`identity.security_session`
- opaque server-side session record; never store raw browser cookie/token
- `id uuid primary key`
- `user_id`, `device_id`
- `session_handle_hash`
- `auth_time`
- `assurance_level`
- `risk_state`
- `expires_at`, `revoked_at`, `last_seen_at`

`identity.recovery_case`
- recovery method, risk state, re-verification status, opened/closed timestamps, mandatory cool-off end.

### Customer / compliance

`customer.customer`
- one customer aggregate per regulated user/person.
- `user_id` unique link to identity.
- lifecycle: `pending -> onboarding -> active | restricted | suspended | closed`.

`customer.contact_method`
- email/phone references, verified state, changed timestamp.
- sensitive values encrypted/tokenized where appropriate.

`customer.address`
- versioned address records; do not silently overwrite legally relevant historical values.

`customer.kyc_case`
- external provider/broker case references, status, reason codes, timestamps.
- retain minimum required PII; store provider reference rather than duplicating identity documents where possible.

`customer.suitability_profile`
- versioned questionnaire result and source answers hash/evidence.

`customer.risk_profile`
- versioned risk classification, horizon, capacity/tolerance, approved restrictions.

`customer.consent`
- `consent_type`, document/version hash, accepted_at, revoked_at, channel/evidence.

### Broker

`broker.broker_account`
- `customer_id`
- `broker_code`
- external account references/UIN/CDC/NCCPL references as permitted
- status and restrictions
- timestamps and last synchronization state

`broker.external_reference`
- maps internal resource IDs to broker IDs.
- unique on `(broker_code, resource_type, external_id)`.

### Funding

`funding.funding_account`
- verified customer bank/funding rail reference.
- stores tokenized/hashed identifiers where possible.

`funding.deposit`
- expected/detected/reconciled state, external reference, gross/net amount, timestamps.

`funding.beneficiary`
- verified withdrawal destination.
- status: `pending_verification | active | cooling_off | blocked | revoked`.
- bank/contact changes create new version rather than silently rewriting approved history.

`funding.withdrawal`
- requested amount/currency, beneficiary version, security/risk state, approvals, broker/payment references.
- follows explicit state machine documented in `ORDER_LEDGER_STATE_MACHINES_V1.md`.

### Ledger

`ledger.account`
- internal accounting account with customer/system ownership and currency.
- account types include customer cash, reserved cash, settlement receivable/payable, fees, tax, clearing/suspense and other approved accounts.

`ledger.journal_entry`
- immutable after `posted_at`.
- `id`, `entry_type`, `correlation_id`, `external_reference`, `effective_at`, `posted_at`, `reverses_entry_id`, `created_by_actor`.

`ledger.journal_line`
- references journal entry and ledger account.
- debit/credit side plus exact amount and currency.
- no negative line amounts.

Posting invariant:
- for every posted journal entry and currency, total debits == total credits.
- posted entry/lines are never edited/deleted.
- mistakes are corrected with a new reversal/adjustment entry.

Implement posting through a single transaction boundary/service; consider a deferred database constraint trigger or controlled database routine to verify balancing at commit. Runtime DB roles must not have unrestricted mutation rights on posted rows.

`ledger.cash_reservation`
- reserves investable cash for an approved order intent.
- unique active reservation per reservation key/order intent.
- lifecycle: active -> partially_consumed -> consumed | released | expired.

### Market

`market.instrument`
- broker/PSX symbol, ISIN where available, currency, instrument type, trading status, lot/minimum increment metadata.

`market.market_session`
- exchange calendar/session status reference.

No executable order may rely on a client-provided or arbitrary web-derived price as authoritative market state.

### Trading

`trading.order_intent`
- normalized internal request from Manual, AI Assisted or Guarded Auto source.
- source reference, portfolio/account, side, order type, quantity/value, limit price, expiry, mandate/profile versions.
- stores policy/risk/compliance decisions and immutable input hash.

`trading.order_policy_check`
- check type, policy version, result, reason codes, evidence IDs.

`trading.broker_order`
- internal order ID, broker external ID, idempotency key, latest known broker state, submitted/acknowledged timestamps.
- unique idempotency scope prevents duplicate submissions.

`trading.execution`
- immutable broker fill/execution fact.
- unique `(broker_code, external_execution_id)`.
- exact quantity, price, fees/taxes where supplied, trade/settlement timestamps.

`trading.position_projection`
- derived projection for fast reads; not independent source of truth.
- reconstructable from reconciled executions/corporate actions.

### Portfolio

`portfolio.portfolio`
- owner/customer, broker account, base currency, status and strategy/profile references.

`portfolio.mandate`
- versioned customer-approved Guarded Auto permissions and constraints.
- immutable version after activation; changes create a new version.
- lifecycle: draft -> pending_confirmation -> active -> paused | revoked | expired | superseded.

`portfolio.target_allocation`
- decision/version target weights and constraints.

`portfolio.snapshot`
- time-stamped derived holdings/cash/risk/performance snapshot for reporting; contains source version/reconciliation marker.

### AI / model governance

`ai.model_version`
- provider/model/version/config hash, approval status, evaluation evidence references.

`ai.decision_run`
- agent/model inputs hash, evidence IDs, prompt/tool/model versions, structured output hash, created_at.

`ai.recommendation`
- customer/portfolio recommendation with risk, rationale/evidence references, expiry and user decision.

`ai.order_intent_proposal`
- structured AI proposal only; never broker-executable by itself.

### Notifications / timeline

`notification.timeline_event`
- user-facing projection referencing authoritative source event IDs.

`notification.outbox`
- transactional outbox row created in the same commit as the authoritative event when notification is required.

`notification.delivery`
- channel/provider/template version, attempt, provider reference, delivery state, failure code.

Financial state never depends on notification delivery success.

### Operations / reconciliation

`ops.reconciliation_run`
- broker/account/time window and result.

`ops.reconciliation_break`
- category, severity, expected vs external reference, status and resolution evidence.
- Critical unresolved breaks can freeze the account or global Auto flow.

`ops.incident`
- operational/security incident tracking with severity and control actions.

### Audit

`audit.event`
- append-only event with actor, action, target, correlation/request IDs, reason, before/after hashes or approved redacted data, timestamp and integrity metadata.

Do not store passwords, secrets, raw tokens, full sensitive documents or unnecessary PII in audit payloads.

## Concurrency and integrity rules

### Financial command idempotency

Every externally retried financial command has an idempotency key scoped to actor + operation. The original normalized request hash is stored. Reusing the key with a different payload is rejected.

### Cash/position reservation

Before broker submission:
1. lock/validate the relevant cash or position reservation boundary;
2. create reservation and approved order intent atomically;
3. only then submit externally.

Duplicate requests must resolve to the existing command result rather than create another reservation/order.

### Transaction isolation

Do not set the entire application to SERIALIZABLE by default.

Use explicit transaction boundaries, row/advisory locking and uniqueness for normal flows. Use PostgreSQL SERIALIZABLE selectively for flows where serial execution semantics materially reduce risk, with bounded retry on `serialization_failure`.

### Projections

Balances, positions and performance views are projections. They may be cached, but must have a traceable source/reconciliation version. Rebuildability is a design requirement.

## Database authorization

- Separate migration owner from runtime application roles.
- Runtime services receive least-privilege schema/table rights.
- Read-only reporting roles cannot write.
- Audit/ledger append paths are more restrictive than ordinary domain tables.
- PostgreSQL Row Level Security may be used as defense-in-depth for selected customer-scoped read models, but application authorization remains mandatory and RLS must be tested against service-role bypass behavior.

## Index baseline

Index by measured query patterns, with initial mandatory indexes for:
- all foreign-key lookup columns used in hot paths,
- unique external broker references,
- active sessions by user/device,
- orders by customer/account/status/created time,
- executions by broker order/external execution ID,
- ledger lines by account/effective time,
- timeline events by customer/time,
- outbox by unpublished state/time,
- reconciliation breaks by severity/status/account.

Avoid speculative indexes on every column.

## Migration policy

- Expand/contract pattern for breaking schema evolution.
- Migrations are forward-tested against representative existing data.
- Destructive operations require explicit data migration/backup/rollback plan.
- No production migration silently rewrites posted ledger/execution/audit history.
- Database schema version and application version compatibility are documented for releases.

## Validation before implementation acceptance

Required automated invariants eventually include:
- debits equal credits per posted entry/currency;
- posted journals cannot be mutated;
- duplicate idempotency key cannot create duplicate financial command;
- duplicate broker execution cannot change holdings twice;
- position projection changes only from valid execution/corporate-action sources;
- active Auto order references an active mandate version;
- customer cannot reference another customer's portfolio/account/beneficiary;
- critical reconciliation break triggers configured freeze/pause behavior.

## External dependencies still unverified

The following remain placeholders until pyPSX partner documentation is received:
- exact account/UIN/CDC/NCCPL identifiers and field lengths,
- order types/status mappings,
- lot/precision semantics,
- fee/tax fields,
- settlement identifiers,
- funding/withdrawal references,
- webhook/event identifiers and ordering guarantees,
- market-data timestamps/freshness guarantees.
