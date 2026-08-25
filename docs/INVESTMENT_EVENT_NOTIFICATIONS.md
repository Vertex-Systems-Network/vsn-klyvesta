# Investment Event Timeline & Notification System

Status: Product/architecture planning.

## Goal

Every material customer investment event must be visible inside Klyvesta and, according to severity and user preference, delivered through Email, WhatsApp and/or SMS.

The system must distinguish:
- financial system-of-record events,
- compliance/audit evidence,
- user-facing timeline events,
- outbound notification deliveries.

These are related but separate records.

## Event categories

### Account
- registration
- identity verification
- KYC submitted/approved/rejected
- broker account created/activated/restricted
- risk profile created/changed
- mandate created/changed/paused/resumed/revoked

### Funding
- deposit initiated
- deposit detected
- deposit reconciled
- deposit failed/mismatched
- withdrawal requested
- withdrawal approved/rejected/completed

### Investing
- portfolio proposal created
- recommendation created
- recommendation approved/rejected/expired
- order intent created
- risk check passed/failed
- compliance check passed/failed
- order submitted
- order accepted/rejected
- partial fill
- full fill
- cancellation
- rebalance planned/executed
- allocation materially changed

### Portfolio/risk
- risk score changed materially
- concentration threshold approached
- drawdown threshold reached
- stale-data trading pause
- broker/system pause
- Auto mode paused by policy
- Auto mode paused by user
- portfolio recovery/update summary

### Income/corporate actions
Where data/support exists:
- dividend announced/received
- split/bonus/right issue
- other corporate action

### Security
- new device/login
- password/passkey/security change
- suspicious login
- account freeze/restriction
- withdrawal security event

## User-facing timeline

Each timeline item should answer:
1. What happened?
2. When?
3. Why?
4. What amount/assets were affected?
5. What did AI recommend/do?
6. Which risk/compliance checks mattered?
7. What is the current status?
8. Does the user need to do anything?

Example:

> 10:42 AM — Portfolio rebalanced
> Klyvesta reduced ABC exposure from 12% to 8% because your concentration limit was reached. Two sell orders were submitted after risk and compliance checks. One is filled; one remains pending.

The explanation must be derived from structured system evidence, never fabricated by an LLM.

## Notification Orchestrator

```text
Domain Event
   -> Notification Policy Engine
   -> Template Renderer
   -> Channel Fanout
      -> In-App
      -> Email Provider
      -> WhatsApp Provider
      -> SMS Provider
   -> Delivery Receipt
   -> Retry / Dead Letter
   -> Notification Audit
```

## Channel policy

### In-app
Primary source for all user-facing investment events.

### Email
Suitable for:
- KYC/account updates
- deposit/withdrawal receipts
- order/fill summaries
- daily/weekly/monthly reports
- mandate changes
- material portfolio/risk summaries

### WhatsApp
Opt-in only. Suitable for:
- material portfolio events
- action-required account/KYC events
- important Auto/risk status changes
- daily summary where enabled

### SMS
Reserve primarily for high-value/critical events to control cost and reduce alert fatigue:
- security alerts
- withdrawal confirmation/status
- critical account restriction
- urgent Auto pause/risk event

Marketing messages must be separated from transactional notifications and follow consent/legal requirements.

## User preferences

Users can configure allowed channels/frequency subject to mandatory security/regulatory communications.

Preferences should support:
- instant
- digest
- daily
- weekly
- off where legally/operationally allowed

Critical security or legally required notices may override ordinary preference rules, with the reason recorded.

## Delivery guarantees

Financial action must never depend on successful email/WhatsApp/SMS delivery.

Delivery flow should support:
- idempotency
- retries with backoff
- provider timeouts
- rate limits
- fallback policy
- dead-letter queue
- delivery receipts
- provider correlation IDs
- duplicate suppression
- template/version tracking

## Data model concepts

- `domain_events`
- `customer_timeline_events`
- `notification_preferences`
- `notification_messages`
- `notification_deliveries`
- `notification_templates`
- `notification_provider_events`

Do not put secrets or unnecessary sensitive portfolio data in provider logs.

## Auditability

Every outbound message should store:
- customer reference
- originating event ID
- template/version
- rendered content hash
- channel
- provider
- send timestamp
- delivery status
- retry count
- provider message/reference ID
- failure reason class

Sensitive content retention must follow privacy/regulatory policy.

## Availability behavior

If WhatsApp/SMS/email provider is unavailable:
- investment execution remains governed by trading/risk rules,
- in-app timeline remains authoritative for user-facing history,
- delivery retries occur according to policy,
- critical provider outage is observable/alerted,
- no false “delivered” state is shown.
