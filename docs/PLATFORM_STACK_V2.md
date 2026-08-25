# Platform Stack V2

Status: Architecture decision for implementation planning. Production versions must be pinned to supported patched releases at implementation time.

## Decision summary

Klyvesta will prefer **native client applications** for Android, iOS and Windows, with a standards-based web application and a .NET financial core. AI/quant remains isolated in Python.

The goal is not to minimize the number of languages at any cost. The goal is to use the strongest platform-native security primitives for customer devices while keeping authoritative financial logic centralized and deterministic.

## Website / Web App

### Language/framework
- TypeScript in strict mode
- React + Next.js Active LTS
- Node.js Active LTS for the Next.js runtime when self-hosted

### Security architecture
- Marketing site and authenticated investment app should be separate deployable/origin concerns where practical.
- Prefer BFF/session-cookie pattern for browser authentication rather than exposing long-lived bearer tokens to JavaScript/browser storage.
- HttpOnly + Secure + SameSite cookies.
- CSRF defenses for state-changing requests.
- Strict CSP, framing restrictions, secure headers and dependency/SRI controls where applicable.
- No authoritative balances, permissions, limits or risk calculations in browser state.

### Current-version note
As of 2026-08-25, Next.js has announced a security release for 2026-08-26 affecting supported lines including 16.3/15.5. Therefore the production rule is **use the latest patched Active/Maintenance LTS, never an unpatched known-vulnerable build**. CI must include security/dependency gating.

## Android App

### Language/framework
- Kotlin
- Jetpack Compose

Google's Android documentation remains Kotlin-first and recommends Kotlin for new Android apps.

### Required native security
- Credential Manager/passkeys where supported
- BiometricPrompt for local step-up UX
- Android Keystore for device-bound cryptographic material
- Play Integrity API as a server-evaluated fraud/tamper signal
- secure deep/app links
- encrypted/minimal local persistence
- no broker/API production secrets shipped in APK/AAB
- no long-lived bearer token in ordinary preferences/files

Root/tamper/integrity verdicts are signals, not sole authorization controls.

## iOS App

### Language/framework
- Swift
- SwiftUI

### Required native security
- passkeys/AuthenticationServices where appropriate
- LocalAuthentication/Face ID/Touch ID for local step-up UX
- Keychain for small secrets/tokens
- Secure Enclave/CryptoKit for device-bound keys where appropriate
- App Attest / DeviceCheck as server-evaluated anti-fraud/app-authenticity signals
- Universal Links rather than insecure custom-link assumptions where possible
- no broker/API production secrets embedded in app bundle

Jailbreak/tamper detection is a signal, not a security boundary.

## Windows App

### Language/framework
- C#
- .NET 10 LTS
- WinUI 3 / Windows App SDK

Microsoft's current .NET support policy lists .NET 10 as active LTS through November 2028. WinUI 3 documentation supports .NET 10.

### Distribution
Primary recommendation:
- **signed MSIX / MSIX bundle**
- Microsoft Store distribution where commercially suitable, because Store handles signing/update delivery
- alternatively direct signed MSIX with a trusted organization code-signing path and App Installer update channel

For the user's desired experience, the customer should receive an installable signed package/app-store install — no developer tooling, runtime commands or manual dependency installation.

### Packaging choice
Prefer a packaged **self-contained MSIX** when installer size is acceptable so runtime dependencies ship with the application. This provides predictable installation and avoids asking customers to install SDK/runtime prerequisites.

MSIX provides clean install/uninstall, package identity, update channels and package-integrity enforcement. Production packages must be signed and timestamped.

### Windows security
- Windows Hello / WebAuthn passkeys
- TPM-backed/device-bound keys when available
- OS credential locker/DPAPI for appropriate local secrets
- no broker keys on endpoint
- no authoritative financial logic dependent on local machine integrity
- signed update chain; reject unsigned/untrusted packages

## Backend / Financial Core

### Language/framework
- C#
- .NET 10 LTS / ASP.NET Core

### Why this supersedes the earlier TypeScript/NestJS proposal
No application code exists yet, so this is a low-cost architecture correction rather than a rewrite.

Benefits:
- current LTS support and security servicing,
- high performance and mature concurrency/runtime,
- strong type system and `decimal` support for financial calculations,
- mature transaction, API, authentication/authorization, rate-limiting and observability ecosystem,
- same language/runtime family as the Windows desktop client,
- fewer authoritative backend languages than splitting financial core across TypeScript + additional services.

### Core modules
- Identity integration / customer profile
- KYC/broker onboarding orchestration
- Funding/withdrawal state
- Double-entry ledger
- OMS/order state machine
- Portfolio/positions
- Risk Governor
- Compliance Gate
- Execution Validator
- Broker Adapter
- Reconciliation
- Audit/evidence
- Notification policy/outbox
- Admin/risk APIs

Start as a modular monolith. Extract services only when scale, security isolation or operational ownership justifies it.

## AI / Quant

### Language/runtime
- Python 3.13 initially

Reason: pyPSX's current public developer documentation supports Python 3.9–3.13. Use an isolated Python environment for research, features, backtesting, model inference and agent orchestration.

### Authority boundary
Python/AI services do **not** own:
- authoritative ledger,
- cash availability,
- final permissions,
- final risk/compliance authority,
- broker execution credentials unless the Broker API ultimately forces an isolated connector design,
- executed order truth.

AI produces structured recommendations/order-intent proposals. .NET financial core validates and authorizes.

If pyPSX Broker API provides only a Python SDK rather than protocol-level REST/WebSocket access, use a narrow isolated Python broker-connector service behind an internal authenticated contract. Do not move risk/ledger authority into that service. This remains unverified until partner documentation is received.

## Data layer

- PostgreSQL as transactional system of record
- Redis only for cache/locks/ephemeral state; never source of financial truth
- PostgreSQL outbox pattern before introducing distributed event infrastructure
- S3-compatible immutable/retention-controlled evidence/report storage
- Kafka/Redpanda/NATS only when measured scale/decoupling requires it

Financial values:
- C# `decimal` / database fixed-precision `numeric`
- never IEEE floating point for money, quantities requiring exact settlement math, fees or ledger balances

## Identity

Do not build password cryptography/authentication protocols from scratch.

Use an OAuth/OIDC/WebAuthn-capable identity architecture with:
- passkeys primary where available,
- strong fallback MFA,
- device/session inventory,
- risk-based step-up,
- server-side session revocation,
- explicit account-recovery state.

Final IdP choice remains a deployment/data-residency decision.

## Shared code strategy

Do not force one UI framework across all platforms.

Share instead:
- OpenAPI schemas/generated clients,
- domain terminology,
- validation contracts where safe,
- design tokens/assets,
- telemetry/event taxonomy,
- test vectors,
- authorization/risk policy definitions on server.

This preserves native platform security while avoiding duplicated API contracts.

## Why not React Native / Flutter for the primary mobile apps

They are viable technologies, but Klyvesta is a security-sensitive financial application where platform-native attestation, credential storage, passkeys, biometrics, deep-link handling and lifecycle behavior are central. OWASP MASVS notes that cross-platform/hybrid frameworks may introduce additional framework-specific vulnerabilities. Native Kotlin/Swift reduces that layer and makes platform security guidance easier to apply directly.

## Why not Electron for Windows

Electron would add a large browser/Node attack surface and duplicate the web runtime for a Windows-only client. For a financial Windows client, native C#/.NET + WinUI 3 + MSIX offers a smaller, more platform-integrated trust/deployment model.

## Security/version policy

- Production uses supported LTS/stable releases only.
- Patch monthly or faster for security releases.
- CI blocks known critical/high vulnerabilities unless formally risk-accepted.
- Lock dependencies; generate SBOM; sign release artifacts.
- No canary/beta runtime in production without explicit architecture/security approval.

## Primary official references verified 2026-08-25

- .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy
- Windows packaging/MSIX: https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/packaging/
- MSIX signing: https://learn.microsoft.com/en-us/windows/msix/package/signing-package-overview
- WinUI 3: https://learn.microsoft.com/en-us/windows/apps/get-started/winui-get-started-overview
- Windows WebAuthn/passkeys: https://learn.microsoft.com/en-us/windows/security/identity-protection/hello-for-business/webauthn-apis
- Android Kotlin-first: https://developer.android.com/kotlin/first
- Android security: https://developer.android.com/privacy-and-security/security-tips
- Android Keystore: https://developer.android.com/privacy-and-security/keystore
- Apple SwiftUI: https://developer.apple.com/swiftui/
- Apple Keychain: https://developer.apple.com/documentation/security/storing-keys-in-the-keychain
- Apple App Attest risk: https://developer.apple.com/documentation/devicecheck/assessing-fraud-risk
- Next.js support/security: https://nextjs.org/support-policy and https://nextjs.org/blog
- Node.js releases: https://nodejs.org/en/about/previous-releases
