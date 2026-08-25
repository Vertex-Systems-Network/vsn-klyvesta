# Security Architecture Audit V1

Date: 2026-08-25
Status: Architecture/specification audit. No production application code exists yet, so this is not a code-level penetration test.

## Executive result

Klyvesta has a strong early financial-safety direction: AI cannot directly execute broker orders, ledger/accounting is intended to be deterministic and immutable, broker integration is abstracted, stale critical data fails closed, and Guarded Auto is gated by mandate/regulatory controls.

The largest gaps are not the basic AI idea; they are the security boundaries around identity, authorization, account recovery, internal staff privileges, client-device trust, third-party dependencies, model/data supply chain, withdrawal/funding controls, incident containment, and disaster recovery.

**No real-money production launch should occur until all Critical and High items below have acceptance evidence.**

## Existing strengths verified in repository

- LLM/agent cannot directly call broker execution.
- Risk Governor, Compliance Gate and Execution Validator are deterministic authorities.
- Broker API credentials are excluded from LLM context.
- Portfolio/KYC/money truth must come from systems of record, not chat memory.
- Double-entry immutable ledger is planned.
- Financial commands are intended to be idempotent.
- Signed webhooks, replay prevention, rate limits, structured audit and reconciliation are already requirements.
- Guarded Auto remains disabled until regulatory/licensing acceptance.
- Product explicitly rejects guaranteed-profit, zero-loss and risk-free claims.

## Critical findings

### C1 — No complete authorization/privilege model existed

Risk: broken object/function-level authorization, insider abuse, support/admin privilege escalation, package-entitlement confusion.

Required:
- RBAC + resource ownership/ABAC checks enforced server-side.
- Customer subscription tier must be an **entitlement**, never a security role.
- No wildcard `admin=*` production role.
- Separate roles for Support, KYC, AML, Risk, Reconciliation, Security, Platform Admin, DevOps and Auditor.
- Break-glass access must be time-bound, dual-approved, reason-coded and fully audited.
- Maker-checker/four-eyes approval for risk-policy changes, model promotion, withdrawal-bank changes, emergency manual corrections and other high-impact operations.

See `docs/AUTHORIZATION_PRIVILEGE_MODEL.md`.

### C2 — Account recovery is a financial transaction risk, not just an auth feature

Risk: attacker changes email/phone/device, resets credentials, then withdraws or changes mandate.

Required:
- Passkey-first authentication.
- Strong recovery with identity re-verification for high-risk recovery.
- New/untrusted device restrictions.
- Cool-off after recovery/contact/bank-detail changes before withdrawals or material profile changes.
- Out-of-band notification to previously trusted channels where possible.
- Risk-based step-up authentication for withdrawal, mandate activation, bank change and sensitive profile changes.

### C3 — Withdrawal/funding controls are underspecified

Risk: account takeover becomes direct loss of customer funds.

Required:
- Beneficiary/bank whitelist.
- First-time bank/beneficiary change must require strong step-up and cool-off.
- Withdrawal state machine with deterministic authorization checks.
- Limits by verified identity/device/risk state.
- Dual control for any internal override.
- No support agent may manually redirect customer money.

### C4 — Third-party broker/market-data compromise can become trade compromise

Risk: pyPSX/broker API, webhook or data source returns malicious, stale, duplicated, reordered or inconsistent data.

Required:
- Treat all third-party data as untrusted.
- Strict response schemas and contract tests.
- Certificate/TLS validation, key rotation, scoped service credentials.
- Webhook signature + timestamp + nonce/replay protection.
- Sequence/out-of-order handling.
- Independent market-state/freshness checks before execution.
- Circuit breaker and Auto pause on broker/data disagreement.
- Continuous reconciliation against broker/execution/custody state.

### C5 — AI/data/model supply chain needs a production threat model

Risk: prompt injection, data poisoning, compromised model/provider, malicious news content, model drift, unsafe tool use.

Required:
- Approved/allowlisted executable market-data sources only.
- Arbitrary web/news text can inform research but cannot be execution price/source of truth.
- Model outputs must be schema-validated and policy-gated.
- Model/prompt/tool versions recorded per decision.
- Model promotion requires regression, adversarial, risk and shadow-mode acceptance.
- AI provider outage must never corrupt order/ledger truth.
- AI cannot change its own limits, mandate, policy or tool permissions.

## High findings

### H1 — Client applications need platform-specific trust controls

Website, Android, iOS and Windows clients are untrusted presentation/interaction layers. No client may be authoritative for balances, limits, permissions, risk decisions or order status.

Required controls:
- Android: Kotlin/Compose, Play Integrity as a risk signal, Android Keystore, passkeys/biometrics, no secrets in app package.
- iOS: Swift/SwiftUI, App Attest as a risk signal, Keychain/Secure Enclave where appropriate, passkeys/biometrics.
- Windows: C#/.NET 10 + WinUI 3, signed MSIX, Windows Hello/WebAuthn, TPM-backed keys when available, secure OS credential storage.
- Web: strict CSP, secure cookies/BFF session pattern, CSRF protection, origin separation, no financial secrets in browser storage.

Root/jailbreak/tamper detection must be risk input, not the sole authorization control.

### H2 — Admin plane must be physically/logically separated from customer plane

Required:
- separate admin origin and authentication policy,
- hardware/passkey MFA mandatory,
- Zero Trust/VPN/device posture where practical,
- no customer password reuse,
- no public support impersonation with transaction capability,
- read-only customer-view mode by default,
- high-risk actions require approval workflow.

### H3 — Notification system can become a phishing/exfiltration channel

Required:
- Email/WhatsApp/SMS are notification channels, never financial source of truth.
- Do not place full CNIC, bank data, secrets or unnecessary balances in messages.
- Avoid actionable withdrawal/login links in SMS/WhatsApp.
- Domain SPF/DKIM/DMARC.
- Optional customer anti-phishing phrase in official email.
- notification preferences cannot suppress mandatory security alerts.
- provider compromise must not allow trade/order execution.

### H4 — API authorization and abuse protections require explicit verification

OWASP API Security Top 10 places Broken Object Level Authorization, Broken Authentication and Broken Function Level Authorization among the top risks. Every endpoint accessing customer/account/order/portfolio IDs must authorize the object and action server-side.

Required:
- deny by default,
- row/resource ownership checks,
- explicit function permissions,
- per-user/device/IP/risk rate limits,
- anti-automation protections on recovery/KYC/notification/withdrawal flows,
- bounded pagination/uploads/batch sizes,
- versioned API inventory,
- no hidden debug/admin endpoints.

### H5 — Supply-chain and release signing must become release gates

Required:
- protected main branch and required CI,
- dependency lockfiles,
- Dependabot/Renovate-style controlled updates,
- SCA/SAST/secret scanning,
- SBOM and build provenance,
- signed mobile/Windows artifacts,
- reproducible or traceable builds,
- artifact promotion rather than rebuilding production from source ad hoc.

Existing Issue #1 tracks branch protection. Existing Issue #2 tracks public/GPL licensing/visibility.

### H6 — Disaster recovery / 3 AM failure model is incomplete

Required:
- RPO/RTO targets,
- encrypted backups,
- restore drills,
- broker outage runbook,
- market-data outage runbook,
- notification outage runbook,
- AI-provider outage runbook,
- KMS/secret compromise runbook,
- global Auto kill switch,
- per-account freeze,
- read-only/degraded mode,
- reconciliation catch-up procedure.

## Medium findings

### M1 — Manual mode must not bypass safety

Manual trading may bypass recommendation/suitability advice where legally permitted, but it must not bypass account status, market state, cash/position checks, hard product restrictions, security restrictions, idempotency or compliance blocks.

### M2 — Package tiers create conflict-of-interest risk

Guide/Advisor/Pro/Autopilot may differ in analytics, automation and support, but cannot create different fundamental safety standards or imply "pay more for better/guaranteed returns".

Subscription entitlement and authorization must be separate concepts.

### M3 — Market-open resource spikes need dedicated load tests

Threats include quote fan-out storms, reconnect storms, notification spikes, repeated AI calls and order retries. AI workload must never starve order, risk, reconciliation or authentication paths.

### M4 — Audit logs need tamper-evidence and controlled access

Use append-only storage, restricted write/read roles, retention policy and periodic integrity verification. Application logs and compliance audit records should remain separate.

### M5 — Financial corrections require explicit adjustment events

Never edit historical ledger/fill records. Corrections must be compensating/adjustment entries with reason, actor, approval and external reference.

## Threat categories to continuously test

- Account takeover and SIM/email compromise
- Passkey/device recovery abuse
- IDOR/BOLA across users/accounts
- Internal privilege escalation
- Support impersonation abuse
- Unauthorized withdrawal/bank change
- Duplicate/replayed orders
- Race conditions around cash/position reservations
- Broker webhook replay/out-of-order delivery
- Market data poisoning/staleness
- Corporate-action accounting errors
- Time/calendar/market-session errors
- Prompt injection/data poisoning
- Model drift/unsafe model promotion
- Secret/key leakage
- Mobile root/jailbreak/tampering
- Windows malware/local token theft
- Web XSS/CSRF/session theft
- Dependency/build/update compromise
- Market-open DoS/cost exhaustion
- Notification bombing/phishing
- Backup/restore failure

## Standards baseline

Klyvesta security verification should be mapped to:
- OWASP ASVS 5.0 for web/API server controls
- OWASP API Security Top 10 2023
- OWASP MASVS + MASTG for Android/iOS
- NIST Cybersecurity Framework 2.0 for organizational security governance
- NIST AI RMF + NIST AI 600-1 for generative-AI/model risk
- Platform-native Android, Apple and Microsoft security guidance

Primary references verified during audit:
- https://owasp.org/www-project-application-security-verification-standard/
- https://owasp.org/API-Security/editions/2023/en/0x11-t10/
- https://mas.owasp.org/MASVS/
- https://www.nist.gov/publications/nist-cybersecurity-framework-csf-20
- https://www.nist.gov/publications/artificial-intelligence-risk-management-framework-generative-artificial-intelligence
- https://developer.android.com/privacy-and-security/security-tips
- https://developer.apple.com/documentation/devicecheck/assessing-fraud-risk
- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/packaging/

## Regulatory architecture note

SECP confirms securities advice is a licensed activity. Securities Managers Regulations 2024 define discretionary portfolio management and require explicit customer authorization/contractual controls. Production AI-assisted advice and Guarded Auto therefore remain blocked until the Phase 0 legal/partner model is accepted.

## Audit conclusion

**Planning architecture: viable, but not production-ready.**

The financial authority boundary is directionally strong. The next architecture work must close authorization, recovery, withdrawal, native-client, third-party trust, incident-response and model-governance gaps before implementation is allowed to claim production-grade security.
