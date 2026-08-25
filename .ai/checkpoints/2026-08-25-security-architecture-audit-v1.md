# Checkpoint — Security Architecture Audit V1

Date: 2026-08-25

## Repository state
- Branch: `main`
- Product status: `PLANNING`
- Active product task remains: `P0-T1 — Confirm regulatory and pyPSX Broker API operating model`
- Product roadmap changed: No phase was skipped or accepted.
- Guarded Auto production remains blocked.

## Completed
- Audited existing product vision, architecture, AI authority boundaries and security baseline.
- Performed current official-source research across OWASP ASVS/API Security/MASVS, NIST CSF/AI RMF, Android, Apple, Microsoft Windows/.NET, Next.js/Node and current SECP/PSX/pyPSX materials.
- Added `docs/SECURITY_ARCHITECTURE_AUDIT_V1.md`.
- Added `docs/AUTHORIZATION_PRIVILEGE_MODEL.md`.
- Added `docs/THREAT_MODEL.md`.
- Added `docs/PLATFORM_STACK_V2.md`.
- Added accepted planning ADR `ADR-002` for native clients + .NET financial core.
- Updated `docs/ARCHITECTURE.md` and `docs/SECURITY.md`.
- Created release-blocking remediation Issues #3, #4 and #5.

## Approved implementation stack
- Web: TypeScript + supported patched Next.js/Node LTS.
- Android: Kotlin + Jetpack Compose.
- iOS: Swift + SwiftUI.
- Windows: C# + .NET 10 LTS + WinUI 3 + signed MSIX.
- Backend financial core: C# + .NET 10 LTS / ASP.NET Core.
- AI/quant: Python 3.13 initially, isolated from financial authority.
- Database: PostgreSQL transactional source of truth; fixed-precision financial math.

## Major audit findings
Critical:
- authorization/privilege model was missing,
- recovery/new-device security required financial controls,
- withdrawal/beneficiary controls underspecified,
- third-party broker/data trust needs fail-closed validation/reconciliation,
- AI/model/data supply chain requires explicit governance.

High:
- native client trust/attestation/storage/signing,
- admin plane separation,
- notification phishing/data leakage,
- API BOLA/BFLA/business-flow abuse,
- supply-chain/release signing,
- DR/incident runbooks.

## Validation
- Verified repo is planning-only; no application code exists to execute runtime/unit/E2E security tests against.
- Verified no open PRs before audit changes.
- Verified current active task/Guarded Auto regulatory gates remained unchanged.
- Verified official .NET 10 support status and Windows MSIX/WinUI guidance.
- Verified Android Kotlin-first and security/Keystore guidance.
- Verified Apple SwiftUI/Keychain/App Attest guidance.
- Verified OWASP ASVS 5.0, API Security and MASVS baselines.
- Verified current SECP securities adviser/manager framework and PSX online-broker technology expectations.

## Security status
**PARTIALLY COMPLETE — architecture audit complete; implementation security not yet verifiable.**

No claim is made that Klyvesta is secure/production-ready because application code, infrastructure, CI, broker sandbox and penetration-test evidence do not yet exist.

## Known blockers
- pyPSX Broker API partner technical/commercial documentation still pending.
- regulatory model for personalized AI advice/Guarded Auto still unresolved.
- Issue #1: main branch protection.
- Issue #2: repository visibility/licensing.
- Issue #3: authorization/recovery/withdrawal security.
- Issue #4: native client trust/signed releases.
- Issue #5: broker/data/reconciliation trust controls.

## Exact next action
Continue P0-T1 while using this audit as the mandatory security architecture baseline. Once pyPSX partner docs arrive, perform a Broker API contract/security review and update BrokerAdapter, data-flow, threat model and acceptance gates before any real-money integration.
