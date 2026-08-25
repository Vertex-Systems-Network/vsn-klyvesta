# Security Requirements

See also:
- `docs/SECURITY_ARCHITECTURE_AUDIT_V1.md`
- `docs/AUTHORIZATION_PRIVILEGE_MODEL.md`
- `docs/THREAT_MODEL.md`
- `docs/PLATFORM_STACK_V2.md`

## Security baseline

Klyvesta must be designed and verified against:
- OWASP ASVS 5.0 for web/API controls,
- OWASP API Security Top 10,
- OWASP MASVS/MASTG for Android/iOS,
- NIST CSF 2.0 for organizational security governance,
- NIST AI RMF / NIST AI 600-1 for AI/model risk,
- platform-native Android/Apple/Microsoft security guidance.

## Identity

- passkeys preferred as primary phishing-resistant authentication where supported,
- strong fallback MFA; SMS is not the preferred high-assurance factor,
- device/session inventory and remote revocation,
- new/untrusted device approval/risk handling,
- step-up authentication for withdrawal, mandate activation, bank/beneficiary changes, recovery and other sensitive actions,
- account recovery treated as a high-risk financial workflow,
- cool-off/restriction after high-risk recovery/contact/bank changes where policy requires,
- secure server-side session revocation and rotating tokens,
- no password/security implementation from scratch when standards-based identity capability is available.

## Authorization

- deny by default,
- RBAC + resource ownership/ABAC + entitlement + risk/security context,
- object-level authorization on every customer/account/order/portfolio resource,
- function-level authorization for every privileged endpoint,
- package/subscription tier is never a privileged role,
- no standing wildcard super-admin,
- maker-checker for high-impact changes,
- time-bound audited break-glass access,
- automated authorization-matrix tests are release gates.

## Customer financial security

- verified/whitelisted bank/beneficiary model,
- strong step-up and risk review for beneficiary changes/withdrawals,
- no support agent may redirect customer funds,
- new/recovered/untrusted device may have restricted sensitive operations,
- mandatory security notifications cannot be disabled,
- manual trading still passes hard account/security/risk/compliance/order-state controls.

## API

- OAuth/OIDC/WebAuthn-capable identity architecture,
- short-lived access/session credentials,
- scopes/least privilege,
- mTLS/workload identity for sensitive internal calls where appropriate,
- signed webhooks,
- timestamp/nonce/replay prevention,
- idempotency keys for financial commands,
- strict request/response schemas,
- bounded uploads/pagination/batch sizes,
- rate limiting per user/device/IP/risk/business flow,
- WAF/DDoS controls,
- API/version inventory,
- no public debug/internal admin endpoints,
- third-party API data is validated as untrusted input.

## Web

- HttpOnly/Secure/SameSite session cookies where browser session architecture uses cookies,
- CSRF protections for state-changing actions,
- strict CSP and anti-clickjacking/framing policy,
- output encoding/XSS controls,
- no long-lived financial bearer tokens in browser localStorage/sessionStorage,
- separate public/customer/admin origins where practical.

## Android

- Kotlin/Jetpack Compose,
- Android Keystore for device-bound key material,
- passkeys/Credential Manager and biometrics for local UX/step-up,
- Play Integrity as server-evaluated risk signal,
- secure app/deep links,
- minimal encrypted local persistence,
- no broker/production service secrets in APK/AAB.

## iOS

- Swift/SwiftUI,
- Keychain for small secrets/tokens,
- Secure Enclave/CryptoKit where appropriate,
- passkeys/LocalAuthentication,
- App Attest/DeviceCheck as server-evaluated risk signal,
- secure Universal Links,
- no broker/production service secrets in app bundle.

## Windows

- C#/.NET 10 + WinUI 3,
- signed MSIX/MSIX bundle for production,
- package integrity/update trust chain,
- Windows Hello/WebAuthn passkeys,
- TPM/device-bound keys when available,
- Credential Locker/DPAPI for appropriate local secrets,
- no broker/production service secrets on endpoint.

## Data

- encryption in transit,
- encryption at rest,
- field-level protection/tokenization for highly sensitive PII where appropriate,
- separate PII access boundary,
- immutable/tamper-evident audit trail,
- backup/restore testing,
- retention/deletion policy,
- data minimization,
- no historical financial record edits; corrections are compensating events.

## Secrets and keys

- KMS/HSM/Vault-class storage,
- no secrets in code/logs/prompts/client apps,
- no broker credentials available to LLM runtime,
- environment separation,
- key rotation,
- least-privilege workload identities,
- emergency secret/key compromise procedure.

## Financial safety

- double-entry ledger,
- atomic transactions,
- deterministic fixed-precision decimal money math,
- unique external/internal reference IDs,
- duplicate-order prevention,
- cash/position reservation/concurrency controls,
- reconciliation,
- broker unknown-state handling before retry,
- account freeze/Auto pause controls,
- global Auto kill switch,
- stale/invalid critical market state => do not trade.

## AI/model security

- prompt injection/adversarial tests,
- tool-permission tests,
- untrusted-context isolation,
- allowlisted executable market-data sources,
- output schema enforcement,
- model fallback/abstention behavior,
- no side effects on malformed/partial AI output,
- sensitive-data redaction,
- no training-provider data sharing unless contractually approved,
- model/prompt/tool/data lineage recorded,
- model promotion requires offline regression + adversarial evaluation + shadow/canary acceptance,
- AI cannot change its own permissions, risk rules, compliance policy or mandate.

## Notification security

- Email/WhatsApp/SMS are delivery channels, not financial truth,
- no unnecessary PII/CNIC/bank data in messages,
- avoid sensitive actionable login/withdrawal links in SMS/WhatsApp,
- SPF/DKIM/DMARC for email domain,
- optional anti-phishing phrase for official email,
- provider compromise cannot authorize financial action,
- notification failures are retried/observed without blocking financial truth.

## Admin / internal operations

- separate admin plane/origin,
- phishing-resistant MFA mandatory,
- stronger device/network posture where practical,
- read-only customer impersonation by default,
- no transaction capability through support impersonation,
- just-in-time/break-glass privileged access,
- high-impact actions require maker-checker.

## Observability / incident response

- structured security events and correlation IDs,
- login/device/recovery/withdrawal/admin/model/policy events monitored,
- fraud/account-takeover anomaly detection,
- runbooks for broker/data/AI/notification/KMS/DB outages,
- per-account/cohort/global circuit breakers,
- defined RPO/RTO,
- encrypted backups and recurring restore drills,
- audit access monitoring.

## SDLC / supply chain

- protected main branch,
- required reviews,
- required CI checks,
- signed/traceable releases,
- secret scanning,
- dependency/SCA scanning,
- SAST,
- DAST,
- mobile app security testing against MASVS/MASTG,
- API authorization/BOLA tests,
- container/image scanning where containers are used,
- SBOM,
- build provenance,
- dependency lockfiles,
- supported stable/LTS runtimes only,
- patch quickly for announced critical/high vulnerabilities,
- production access logs.
