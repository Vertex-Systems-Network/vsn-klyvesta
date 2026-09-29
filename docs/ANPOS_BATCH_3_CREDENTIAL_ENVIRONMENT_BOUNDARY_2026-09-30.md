# ANPOS Batch 3 — Credential and Environment Boundary

Date: 2026-09-30 (Asia/Karachi)
Repository: `Vertex-Systems-Network/vsn-klyvesta`
Status: **IMPLEMENTED — SANDBOX-ONLY ACCEPTANCE; PRODUCTION FAIL-CLOSED**

## Scope

Batch 3 establishes the repository boundary between local/non-live, pyPSX sandbox and future production environments. It does not activate live credentials or production trading.

## Implemented controls

- Only `sandbox` is a successful workflow target.
- A `production` target fails immediately.
- Sandbox base URL is fixed to `https://brokerapi-paper.pypsx.com`.
- Production URL is comparison-only and cannot be selected.
- Dedicated secrets are used: `PYPSX_SANDBOX_KEY_ID` and `PYPSX_SANDBOX_SECRET`.
- Secrets are masked before use and never printed.
- Workflow permission is `contents: read`.
- Secret variables are unset during cleanup.
- No KYC, PII, funding, withdrawal, live order or production adapter is enabled.

## Acceptance tests

The workflow must prove:

1. Sandbox target succeeds.
2. Sandbox health returns `status=ok`.
3. Sandbox credentials exist without exposing values.
4. Production target fails closed.
5. Sandbox and production URLs cannot be equal.

## Not claimed

Production credentials, idempotency/retry/timeout, rate limits/SLA, KYC/CDC, legal broker/custody responsibility and live trading remain unverified.

## Rotation checklist

1. Revoke the old sandbox key.
2. Create a minimum-scope replacement.
3. Update the GitHub environment secret only.
4. Run the boundary workflow.
5. Confirm the old key no longer authenticates.
6. Store the dated evidence URL privately.

The acceptance run URL must be added here after execution.
