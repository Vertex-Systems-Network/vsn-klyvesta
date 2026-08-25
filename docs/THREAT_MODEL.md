# Klyvesta Threat Model

Status: Baseline threat model for design and implementation.

## Trust boundaries

1. Customer devices/browser — always untrusted.
2. CDN/WAF/public edge.
3. Identity/session boundary.
4. Customer API/BFF.
5. Financial core: ledger, OMS, portfolio, funding, reconciliation.
6. Policy boundary: suitability, Risk Governor, Compliance Gate.
7. AI/quant zone — probabilistic/untrusted output until validated.
8. Broker integration zone — external dependency, untrusted responses until validated/reconciled.
9. Admin/operations plane — separate high-trust zone.
10. Notification providers — external low-trust delivery dependencies.
11. Data stores/audit evidence.

## Primary assets

- customer identity/KYC data,
- sessions/passkeys/device trust,
- bank/beneficiary details,
- customer funds/cash state,
- securities positions,
- orders/fills,
- ledger entries,
- mandates/suitability profiles,
- broker credentials,
- market data and model features,
- AI/model/prompt artifacts,
- risk/compliance policies,
- audit/evidence records,
- signing keys and release pipeline.

## STRIDE + financial threats

### Spoofing
- stolen password/session/refresh token,
- SIM swap/email takeover,
- fake mobile/Windows app,
- malicious device replay,
- forged broker webhook,
- service identity compromise.

Controls: passkeys, device approvals, attestation signals, short sessions, rotation/revocation, signed webhooks, mTLS/workload identity, signed app packages.

### Tampering
- order-intent modification,
- client price/balance tampering,
- webhook replay/reordering,
- market-data poisoning,
- risk-policy modification,
- model/prompt tampering,
- package/update supply-chain compromise.

Controls: server-side truth, schemas, idempotency, sequence/timestamp checks, policy versioning, maker-checker, signed artifacts, provenance/SBOM.

### Repudiation
- customer disputes mandate/trade,
- staff disputes override,
- model decision cannot be reproduced.

Controls: immutable/tamper-evident audit, consent/version records, correlation IDs, model/prompt/data/policy versions, actor/device records, approval history.

### Information disclosure
- KYC/CNIC leakage,
- bank data leakage,
- portfolio leak across users,
- secrets in logs/prompts,
- notification content leakage.

Controls: least privilege, field minimization/tokenization, object-level authorization, redaction, separated PII zone, secure templates, logging policy.

### Denial of service / economic exhaustion
- market-open request flood,
- WebSocket reconnect storm,
- notification bombing,
- AI prompt/cost exhaustion,
- broker retry storm,
- oversized queries/uploads.

Controls: rate limits, queues/backpressure, bounded payloads, circuit breakers, retry budgets, AI quotas, workload isolation, degraded mode.

### Elevation of privilege
- user accesses another account,
- support agent becomes financial operator,
- admin wildcard bypass,
- compromised AI agent gains broker tool,
- service account gains broader scopes.

Controls: RBAC+ABAC, deny-by-default, no wildcard admin, machine-principal scopes, maker-checker, break-glass, automated authorization tests.

## Financial-domain abuse cases

### Account takeover -> withdrawal
Kill chain:
credential/session compromise -> new device -> bank/beneficiary change -> withdrawal.

Required interruption points:
- device trust challenge,
- strong step-up,
- recovery/new-device risk state,
- beneficiary whitelist,
- cool-off,
- out-of-band notification,
- withdrawal risk rules.

### Duplicate trade
Causes: retry, timeout ambiguity, client double-submit, webhook/order race.

Controls:
- idempotency key,
- server-generated immutable order intent ID,
- atomic reservation/state transition,
- broker external reference mapping,
- reconciliation before retry where status unknown.

### Overspending cash / overselling position
Causes: concurrent orders, stale portfolio cache.

Controls:
- authoritative reservation/available-to-trade state,
- transaction/locking strategy,
- deterministic pre-trade check,
- broker reconciliation.

### AI prompt injection -> financial action
Controls:
- external text is data only,
- no broker tool in LLM context,
- typed proposal schema,
- deterministic risk/compliance/execution validation,
- source allowlist for executable market facts.

### Market data poisoning/staleness
Controls:
- source timestamps,
- sanity/deviation checks,
- market session/calendar validation,
- optional independent comparison source,
- stale threshold -> fail closed/Auto pause.

### Insider manipulation
Controls:
- role separation,
- maker-checker,
- no direct DB edits,
- immutable adjustments,
- just-in-time privileged access,
- alerts/review for sensitive admin actions.

### Model promotion failure
Controls:
- offline evaluation,
- backtest leakage checks,
- shadow mode,
- canary/limited cohort,
- risk committee approval,
- rollback to known model,
- drift monitoring.

## Client-specific threats

### Web
- XSS, CSRF, supply-chain script injection, session theft, clickjacking.

### Android
- repackaging, overlay/accessibility abuse, rooted device, token extraction, malicious deep links.

### iOS
- jailbroken/tampered device, keychain misuse, malicious universal/deep links, runtime hooking.

### Windows
- local malware/token theft, fake installer/update, DLL/plugin injection, desktop automation/social engineering.

No client is trusted to enforce final financial authorization.

## 3 AM failure modes

- broker unavailable while order status unknown,
- market feed stale but app still live,
- AI provider down,
- DB failover mid-financial transaction,
- Redis loss,
- notification provider outage,
- signing key compromise,
- KMS unavailable,
- reconciliation mismatch,
- mass account-takeover pattern.

Required behavior:
- prefer pause/read-only over unsafe guessing,
- preserve ledger/order truth,
- circuit-break dependent automation,
- expose operational status internally,
- retain enough evidence for reconciliation/recovery,
- global and per-account kill switches.
