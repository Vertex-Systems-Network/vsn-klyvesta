# Authentication & Session Architecture V1

Status: Implementation-ready planning baseline. Final identity provider/vendor choice remains open.

## Security objective

Authentication must resist phishing, replay, token theft, account takeover and unsafe recovery while remaining usable across Web, Android, iOS and Windows.

Klyvesta will not invent a custom authentication protocol. Use standards-based OAuth 2.0 / OpenID Connect / WebAuthn components from supported implementations.

For high-value API/security boundaries, target the OpenID Foundation FAPI 2.0 Security Profile where ecosystem/IdP support allows. FAPI adoption must come through standards-compliant components and conformance testing, not home-grown approximations.

## Identity architecture

Use one central identity authority with separate application clients/policies for:
- customer web app,
- Android,
- iOS,
- Windows,
- staff/admin plane,
- service-to-service identities.

Customer and staff identities are logically separated. A customer credential cannot authenticate to the admin plane; an internal staff session cannot become a customer transactional session through impersonation.

## Authentication methods

### Primary
- Passkeys/WebAuthn as the preferred phishing-resistant method where supported.

### Fallback
- Strong MFA with risk controls.
- Password fallback, if offered, is secondary and must be protected by rate limits, breach-password screening where supported, and step-up controls.

SMS alone is not sufficient for high-risk transaction authorization or privileged staff authentication.

## Web authentication

Preferred pattern: Backend-for-Frontend (BFF).

Flow:
1. Browser visits Klyvesta web app.
2. BFF starts Authorization Code flow using OIDC and PKCE.
3. Authentication occurs at the trusted identity authority.
4. Tokens are retained server-side or in a protected server-side session context.
5. Browser receives only an opaque session cookie.

Cookie requirements:
- `HttpOnly`
- `Secure`
- restrictive `SameSite` appropriate to the selected OIDC/BFF flow
- narrow path/domain scope
- short idle lifetime and bounded absolute lifetime
- rotation on authentication/privilege change

Do not place long-lived bearer/refresh tokens in `localStorage`, `sessionStorage` or ordinary browser JavaScript state.

State-changing browser requests require CSRF protection and origin validation.

## Native app authentication

Android, iOS and Windows use Authorization Code + PKCE through the system browser / platform authentication session. Embedded WebViews are not used for primary OAuth login.

Token handling:
- access tokens short-lived;
- refresh-token rotation/reuse detection when supported;
- sender-constrained tokens (for example DPoP or mTLS where appropriate and supported) preferred for high-value API clients;
- sensitive local token/key material stored only in platform secure storage;
- server can revoke device/session independently.

Platform storage:
- Android: Keystore-backed material.
- iOS: Keychain/Secure Enclave where appropriate.
- Windows: Windows Hello/TPM/DPAPI/Credential Locker appropriate to threat model.

Device integrity/attestation (Play Integrity, App Attest/DeviceCheck, Windows posture signals) is a fraud/risk signal, not a standalone authorization decision.

## Server-side session model

Each authenticated session has an authoritative `identity.security_session` record:
- session ID (UUIDv7)
- user ID
- device ID
- opaque handle hash / token family reference
- authentication time
- authentication method
- assurance level
- risk state
- IP/network risk metadata (minimal/appropriate retention)
- created, last seen, idle expiry, absolute expiry
- revoked timestamp/reason
- recovery-sensitive flag

Browser cookies/native tokens are credentials referencing server-side authorization state; they do not permanently encode current entitlements, roles or risk status.

## Device model

A device record tracks:
- platform/app version
- first/last seen
- public device key reference where used
- attestation/integrity state
- trusted/untrusted/restricted/revoked status
- user-friendly name
- recent security events

Trust is revocable server-side.

## Step-up authentication

Require fresh phishing-resistant or strong MFA authentication for high-risk actions, including at minimum:
- add/change withdrawal beneficiary/bank destination,
- withdrawal above configured threshold or first withdrawal,
- activate/increase Guarded Auto mandate risk,
- change critical identity/contact details,
- account recovery completion,
- register/remove high-value authentication factors,
- sensitive API/security settings,
- privileged staff actions.

Step-up authorization is action-specific and short-lived. A successful login hours earlier is not sufficient evidence for every financial action.

## Account recovery state machine

`Normal -> RecoveryStarted -> IdentityReverification -> SecurityHold -> RecoveredRestricted -> Normal`

Rules:
- recovery creates a dedicated high-risk state;
- notify previously trusted channels where possible;
- identity re-verification is required for high-risk recovery;
- revoke or review existing sessions/factors according to recovery scenario;
- apply a configurable cool-off to withdrawals, beneficiary changes and mandate risk increases;
- security team can extend restriction based on fraud signal;
- support cannot unilaterally bypass recovery controls.

## Session revocation triggers

Immediately revoke or re-evaluate sessions after:
- user-initiated sign-out-all,
- credential/passkey compromise report,
- recovery completion,
- suspicious device/event,
- internal account suspension,
- KYC/AML restriction where required,
- staff role/privilege change,
- secret/identity-provider incident.

## Staff/admin authentication

Admin plane is a separate origin/application client and security policy.

Requirements:
- passkey/hardware-backed phishing-resistant MFA required;
- no SMS-only MFA;
- shorter session lifetimes;
- device posture/managed device policy where practical;
- IP/Zero Trust/VPN controls based on deployment;
- step-up for privileged changes;
- break-glass account(s) disabled/locked down except documented emergency procedure;
- all privileged actions are reason-coded and audited.

## Authorization separation

Authentication answers **who is this?**
Authorization answers **may this actor perform this action on this resource now?**

Never treat:
- a subscription package,
- an authenticated session,
- a trusted device,
- an AI agent identity,

as sufficient authorization by itself.

Authorization combines role/scope, resource ownership, entitlement, legal availability, account state, security state, risk state and policy checks.

## Service identities

Service-to-service calls use dedicated workload identities, not shared human/API keys.

Requirements:
- least-privilege scopes;
- short-lived credentials/tokens;
- mTLS or sender-constrained mechanisms for sensitive internal/external service calls where appropriate;
- rotation and revocation;
- no service credential available to client apps or LLM context;
- explicit audience validation.

## Cryptographic/session key management

ASP.NET Core Data Protection keys used for cookies/anti-forgery/protected state must be persisted across instances and encrypted at rest using deployment-approved KMS/HSM/Key Vault equivalent.

Do not rely on ephemeral local keys in production. Key loss can invalidate protected state; key compromise requires explicit revocation/incident procedure.

## Rate limiting / abuse controls

Apply partitioned rate limiting and abuse detection by endpoint cost and risk. Separate policies for:
- login/passkey registration,
- password reset/recovery,
- OTP/MFA sending/verification,
- KYC upload/submission,
- beneficiary changes,
- withdrawals,
- trading commands,
- AI requests,
- notification triggers.

Rate limits are not the sole DDoS defense and must be load-tested.

## API security target

Where selected identity/API components support it, customer/native high-value APIs should align to FAPI 2.0 principles:
- Authorization Code + PKCE;
- exact redirect URI handling;
- sender-constrained access tokens;
- strong client authentication for confidential/service clients;
- secure authorization request handling as required by profile;
- replay-resistant state/nonce handling;
- issuer/audience validation;
- OAuth Security BCP alignment.

Do not claim FAPI conformance until the deployed authorization server/client combination passes relevant conformance tests.

## Privacy/logging

Do not log:
- passwords,
- passkey private material,
- access/refresh tokens,
- OTP secrets,
- raw recovery secrets,
- full sensitive PII unnecessarily.

Security logs use pseudonymous IDs and minimal network/device metadata sufficient for investigation and policy.

## Test requirements

Automated/integration tests must prove:
- no session fixation across login;
- revoked session cannot continue sensitive access;
- cross-user resource access is denied;
- CSRF protections reject forged browser commands;
- refresh-token reuse/replay is handled according to IdP capability;
- new/recovered device cannot bypass withdrawal cool-off;
- step-up expires and is action/scenario constrained;
- staff session cannot access customer transactional endpoints;
- integrity/attestation failure never grants extra privilege;
- expired/invalid issuer/audience tokens fail closed.

## Vendor decision criteria

Before selecting an IdP, verify:
- OIDC/OAuth standards compliance;
- WebAuthn/passkeys support;
- native PKCE support;
- sender-constrained token/FAPI 2.0 support or roadmap;
- MFA/recovery policy controls;
- session/device revocation APIs;
- audit events;
- data residency/privacy;
- HA/DR and SLA;
- key management/HSM options;
- supported SDK quality for all four clients;
- pricing/lock-in/exit strategy.

## External references verified 2026-08-25

- OpenID Foundation FAPI 2.0 Security Profile — Final, February 2025.
- OpenID Foundation FAPI 2.0 conformance/certification program.
- Microsoft ASP.NET Core 10 passkey/WebAuthn support.
- Microsoft ASP.NET Core Data Protection key storage/key management guidance.
- Microsoft ASP.NET Core rate limiting guidance.
