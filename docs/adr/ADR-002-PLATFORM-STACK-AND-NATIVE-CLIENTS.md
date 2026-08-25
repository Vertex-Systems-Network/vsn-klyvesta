# ADR-002 — Native Clients + .NET Financial Core

Status: Accepted for implementation planning
Date: 2026-08-25

## Context

Klyvesta requires Website, Android, iOS and Windows applications plus a security-critical financial backend and isolated AI/quant runtime. The previous planning document proposed React Native mobile and TypeScript/NestJS core API, but no production code exists yet.

The security audit found that platform-native credential storage, passkeys/biometrics, device/app attestation, signed desktop packaging and strict separation of probabilistic AI from authoritative financial state are central requirements.

## Decision

- Web: TypeScript + React/Next.js supported patched LTS.
- Android: Kotlin + Jetpack Compose.
- iOS: Swift + SwiftUI.
- Windows: C# + .NET 10 LTS + WinUI 3, distributed as signed MSIX/MSIX bundle.
- Backend financial core: C# + .NET 10 LTS / ASP.NET Core.
- AI/quant/research: Python 3.13 initially, isolated behind typed internal contracts.
- PostgreSQL remains transactional system of record.

## Reasons

1. Native Android/iOS reduces cross-platform framework attack surface and maps directly to platform security controls.
2. .NET 10 is current active LTS and provides a mature high-performance financial backend runtime.
3. C# can serve both authoritative backend and Windows client, reducing backend/runtime fragmentation.
4. WinUI 3 + MSIX gives Windows a signed, installable, updateable package with platform identity/integrity.
5. Python remains best suited to pyPSX-compatible research/quant/AI, but does not own ledger/risk/authorization/execution truth.
6. No code rewrite is required because implementation has not started.

## Consequences

- More native client code than a single cross-platform UI framework.
- API contracts/design tokens/test vectors must be shared to reduce duplication.
- Mobile/Windows clients remain untrusted; server is authoritative.
- CI/release pipelines must independently sign/test web, Android, iOS and Windows artifacts.
- Team requires Kotlin, Swift and C# capability in addition to TypeScript/Python.

## Non-goals

This ADR does not decide:
- final identity provider,
- cloud vendor,
- pyPSX Broker API protocol/SDK shape,
- final database managed provider,
- regulatory permission for AI Assisted/Guarded Auto.

Those remain gated decisions.
