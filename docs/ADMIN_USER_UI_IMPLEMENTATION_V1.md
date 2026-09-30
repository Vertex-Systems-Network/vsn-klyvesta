# Admin/User UI Structure V1

Status: implemented as a truthful non-live UI structure preview on branch `codex/admin-user-ui-structure`.

## What is implemented

- Separate admin control-plane preview at `/demo/admin.html`.
- Separate investor workspace preview at `/demo/user.html`.
- Explicit Sandbox/Live status separation.
- User-facing account mapping states: Klyvesta, sandbox, KYC, and live mapping.
- Admin-facing provider, commission, user-readiness, audit and kill-switch areas.
- Fail-closed labels for unavailable provider/live capabilities.
- No PyPSX or CDC credentials are embedded in browser code.
- Existing demo dashboard remains unchanged.

## Truth boundary

This is UI structure, not production authentication or live integration. The buttons are intentionally disabled or preview-only where backend/provider evidence is absent. The following are not claimed complete:

- real admin authentication and authorization;
- real user signup, password recovery or session management;
- PyPSX account provisioning;
- CDC/KYC API integration;
- live account linking;
- live trading;
- production audit persistence.

## Intended production flow

```text
Klyvesta user login
  -> automatic sandbox account provisioning
  -> demo portfolio/trading

Klyvesta user login
  -> PyPSX KYC API
  -> CDC account create/link
  -> broker approval
  -> live account mapping
  -> live trading
```

The owner environment selector must not grant live access by itself. Live enablement is per-user and requires all provider, legal, KYC, broker and governance gates.
