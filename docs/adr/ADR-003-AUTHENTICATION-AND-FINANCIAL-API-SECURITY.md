# ADR-003 — Authentication & Financial API Security Architecture

Status: Accepted for implementation planning.
Date: 2026-08-25

## Context

Klyvesta serves high-value financial workflows across Web, Android, iOS and Windows. Token theft, phishing, unsafe embedded login, weak recovery and account takeover can become direct financial loss.

The project must avoid building custom authentication protocols while retaining server-side control over sessions, devices, risk state and high-risk action authorization.

## Decision

1. Use a standards-based central identity authority supporting OAuth 2.0 / OpenID Connect / WebAuthn.
2. Prefer passkeys/WebAuthn as the primary phishing-resistant customer authentication method where supported.
3. Web uses a Backend-for-Frontend pattern with server-side token/session handling and an opaque secure HttpOnly cookie.
4. Android, iOS and Windows use Authorization Code + PKCE through system-browser/platform authentication sessions; no embedded WebView for primary OAuth login.
5. High-value APIs target OpenID FAPI 2.0 Security Profile principles/conformance where selected IdP/client ecosystem supports it, including sender-constrained tokens.
6. Account recovery creates an explicit restricted financial security state with re-verification/cool-off controls.
7. High-risk actions require action-specific fresh step-up authentication.
8. Admin/staff authentication is a separate application/origin/security policy with phishing-resistant MFA.
9. Session/device authorization remains server-revocable; client trust/attestation is a signal, never sole authorization.

## Alternatives considered

### Long-lived bearer tokens in browser storage
Rejected due to XSS/token-exfiltration risk and weak revocation/context control.

### Embedded mobile/desktop WebView login
Rejected for primary OAuth because system-browser authorization provides safer origin/user-agent handling and better standards alignment.

### Custom JWT/password/passkey protocol
Rejected. Authentication protocol complexity and cryptographic risk are not justified.

### SMS-first MFA
Rejected as the high-assurance default. SMS may be a fallback/recovery signal but not the sole control for privileged/high-value actions.

## Consequences

Positive:
- stronger phishing/token-theft resistance;
- consistent device/session revocation;
- native passkey/security primitive use;
- easier conformance/security review;
- recovery risk becomes explicit financial policy.

Costs:
- IdP/vendor capability evaluation is required;
- FAPI 2.0 support can constrain provider/client choices;
- BFF adds server-side session infrastructure;
- multi-platform OAuth testing is mandatory.

## Verification

Do not claim FAPI conformance until the actual deployed authorization server/client configuration passes relevant OpenID Foundation conformance tests.

Implementation/testing requirements are defined in `docs/AUTH_SESSION_ARCHITECTURE_V1.md`.

## References verified at decision time

- OpenID Foundation — FAPI 2.0 Security Profile Final (February 2025).
- OpenID Foundation — FAPI 2.0 conformance/certification tests.
- Microsoft ASP.NET Core 10 — WebAuthn/passkeys.
- Platform-native Android, Apple and Windows credential/security guidance recorded in `PLATFORM_STACK_V2.md`.
