# Admin/User Authentication Implementation Baseline V1

Status: repository-owned implementation baseline; runtime identity provider integration remains pending.

## Decisions

- Admin/owner and investor/user are separate application clients/policies.
- A customer credential must never authenticate to the admin plane.
- The browser receives only an opaque server-side session cookie.
- PyPSX and KYC secrets are backend/service credentials; they never enter browser code.
- Sandbox access is allowed only through an explicitly marked non-live path.
- Live access is per-user and requires KYC, broker linkage, legal/provider acceptance and account mapping.
- Selecting Live in an admin control plane cannot grant a user live trading authority.

## Required routes

### Customer

- `/auth/user/login`
- `/auth/user/signup`
- `/auth/user/forgot-password`
- `/auth/user/logout`
- `/api/user/me`
- `/api/user/account-readiness`

### Admin

- `/auth/admin/login`
- `/auth/admin/logout`
- `/api/admin/me`
- `/api/admin/environment`
- `/api/admin/users`
- `/api/admin/provider-status`
- `/api/admin/audit`

These routes are a contract baseline, not a claim that runtime endpoints already exist.

## User state model

`UserActive -> DemoReady -> LiveKycRequired -> LiveKycPending -> BrokerApprovalPending -> LiveMapped -> LiveEnabled`

Any failed, restricted, expired, revoked or provider-unknown state must deny live trading. Existing demo users remain demo-only.

## Account mapping

A user record must support distinct references:

- `user_id`: Klyvesta identity
- `sandbox_account_id`: PyPSX sandbox sub-account
- `kyc_application_id`: KYC application, when live onboarding begins
- `cdc_link_status`: provider-reported state, never inferred locally
- `live_account_id`: PyPSX live account after broker approval
- `live_trading_status`: derived server-side gate

The client may display these states but cannot set them.

## Acceptance tests required before runtime completion

- customer session cannot access admin routes;
- admin session cannot access another customer's transactional resources;
- revoked/expired sessions fail closed;
- password recovery creates a restricted/review state;
- sandbox user cannot call live endpoints;
- live endpoint denies users without all approval gates;
- provider/API secrets are absent from browser responses and static assets;
- environment switching does not mutate user live status;
- cross-user account IDs return a safe not-found/forbidden result;
- all privileged changes produce an audit record.

## Current truth boundary

This baseline does not claim:

- production identity provider selected;
- real password storage or recovery implemented;
- runtime RBAC/ABAC implemented;
- PyPSX account provisioning implemented;
- CDC/KYC or broker approval integrated;
- live trading enabled.

Those require code, provider credentials/contracts, tests and acceptance evidence.
