# Protected Main Governance

Status: **IMPLEMENTATION READY — LIVE READ-BACK REQUIRED**

Linear: `ABD-44`  
GitHub: Issue #1  
Repository: `Vertex-Systems-Network/vsn-klyvesta`  
Protected target: `main`

## Current evidence

The repository currently exposes no repository rulesets through the accessible ruleset read (`[]`). Direct branch-protection read is not available to the connected integration in this execution environment, so absence/presence of classic branch protection must be certified by an authenticated repository admin using the retained live verifier.

Current `main` publishes three established product/security checks:

- `Build and architecture verify`;
- `Analyze C#`;
- `PostgreSQL migration and constraints`.

This governance package adds a fourth stable check:

- `Repository governance`.

The persistence check is now part of the required baseline because current implementation includes database migrations/constraints and that check is active on `main`; the older Issue #1/ABD-44 text predates this repository state.

## Target protection

`scripts/apply_main_protection.ps1` configures:

- PR-only integration;
- strict/up-to-date required status checks;
- all four required checks bound to the GitHub Actions app identity;
- admin enforcement;
- stale-review dismissal;
- required conversation resolution;
- force-push denial;
- branch-deletion denial;
- no implicit push restrictions/bypass actors beyond the documented PR policy.

## Review modes

The applicator has no silent review default.

### `independent`

Use when a qualified second reviewer is available:

- at least one approving review is required;
- stale approvals are dismissed;
- latest-push approval protection is required.

### `solo-self-review`

Explicit exception for genuine solo operation:

- repository approval count is zero;
- CI, PR-only integration, conversation resolution, admin enforcement, force-push denial and deletion denial remain mandatory;
- every sensitive promotion must state `SELF REVIEW` honestly;
- independent review must never be claimed.

## Apply

From an authenticated repository-admin workstation:

```powershell
pwsh scripts/apply_main_protection.ps1 -ReviewMode independent
```

or, only where the solo exception is deliberately selected:

```powershell
pwsh scripts/apply_main_protection.ps1 -ReviewMode solo-self-review
```

## Certify

Immediately after application, run the matching live verifier:

```powershell
pwsh scripts/verify_main_protection.ps1 -ReviewMode independent
```

or:

```powershell
pwsh scripts/verify_main_protection.ps1 -ReviewMode solo-self-review
```

`ABD-44` / Issue #1 cannot be closed from documentation, a successful PUT response, or green CI alone. Acceptance requires live read-back showing the effective `main` policy.

## Relationship to P0-PAR / Issue #8

Protected-main acceptance is an independent prerequisite referenced by the P0-PAR live-onboarding gate. Landing this tooling does not authorize live PII, broker/pyPSX operation, real-money execution or P1+ advancement.
