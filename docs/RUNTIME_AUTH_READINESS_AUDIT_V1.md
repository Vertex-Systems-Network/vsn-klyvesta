# Runtime Auth Readiness Audit v1

Status: Evidence-based audit complete. This document does not claim production authentication is implemented.

## Verified on main

| Area | Evidence | Truth status |
|---|---|---|
| EF Core context | `src/Klyvesta.Infrastructure/Persistence/KlyvestaDbContext.cs` | Present |
| Persistence migration | `src/Klyvesta.Infrastructure/Persistence/Migrations/20260825153544_F2InitialPersistence.cs` | Present |
| Operational records | `src/Klyvesta.Infrastructure/Persistence/Records/` | Idempotency, inbox, and outbox only |
| PyPSX broker boundary | `src/Klyvesta.Infrastructure/Broker/PyPsx/` | Present as a broker boundary |
| Runtime auth routes | `src/Klyvesta.Api/Program.cs` | Not present |
| User/admin identity records | No user, role, credential, or session entity is present in the verified persistence tree | Not present |
| Production identity provider | No provider configuration or callback route is present | Not present |
| Demo authentication | `/api/demo/login` with a demo cookie | Development/Demo preview only |
| Production behavior | Production maps only the foundation root and health endpoints | Fail-closed |

## Completion decision

The Admin/User authentication baseline is documented in `docs/ADMIN_USER_AUTH_IMPLEMENTATION_BASELINE_V1.md`, but it is not runtime-complete. The current repository has persistence foundations, not an identity model.

It would be false to mark any of these as complete:

- production user registration/login/logout;
- password recovery;
- server-side opaque sessions;
- admin/user RBAC;
- provider-backed identity callbacks;
- PyPSX account provisioning linked to an authenticated user;
- live KYC or live trading authorization.

## Required next implementation slice

Before production auth routes can be implemented safely, the repository needs:

1. An approved identity strategy and provider configuration (OIDC/OAuth or a dedicated password credential service).
2. User, role, session, recovery-token, and audit entities plus a migration.
3. Server-side session creation, rotation, revocation, expiry, and CSRF policy.
4. Admin/user authorization policies and route tests.
5. A provider-backed sandbox account-linking workflow; no broker or live action may be authorized from a UI-only flag.
6. CI integration tests against the configured PostgreSQL service.

Until these exist, demo mode remains the only intentionally runnable login flow and production remains fail-closed.

## Evidence boundary

This audit was derived from the current `main` tree and exact file paths above. It is an implementation-readiness result, not a substitute for runtime tests or provider certification.
