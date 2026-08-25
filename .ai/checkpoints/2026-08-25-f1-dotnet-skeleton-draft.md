# Checkpoint — F1 .NET Foundation Draft

Date: 2026-08-25
Branch: `foundation/f1-dotnet-skeleton`
Draft PR: #6

## Status

**PARTIALLY COMPLETE — F1 scaffold is implemented and CI-verified on a feature branch, but it is not merged or production-authorized.**

The canonical active product task on `main` remains `P0-T1 — Confirm regulatory and pyPSX Broker API operating model`.

## Completed

- Created isolated feature branch from main checkpoint `3baf840ea72d4e67ecc791d8c73108a9b0c00a16`.
- Added .NET 10 / C# 14 repository build baseline.
- Pinned SDK to `10.0.400` with patch roll-forward only.
- Added Domain / Application / Infrastructure / API projects.
- Added strict nullable/analyzer/warnings-as-errors/checked-arithmetic/unsafe-off defaults.
- Enabled NuGet audit for direct/transitive dependencies.
- Restricted NuGet source baseline to official nuget.org pending reviewed private-registry design.
- Added minimal non-financial API liveness/readiness endpoints.
- Added architecture project-reference verifier.
- Added SHA-pinned GitHub Actions dependencies and non-persistent checkout credentials.
- Added GitHub Actions Dependabot maintenance.
- Added development instructions.
- Opened draft PR #6; no merge performed.

## Research performed

Verified current official/current sources before implementation:
- .NET 10 download line: SDK `10.0.400`, runtime/security patch `10.0.11` current on 2026-08-25.
- `actions/checkout` latest release `v7.0.1`, pinned commit `3d3c42e5aac5ba805825da76410c181273ba90b1`.
- `actions/setup-dotnet` latest release `v6.0.0`, pinned commit `a98b56852c35b8e3190ac28c8c2271da59106c68`.
- .NET 10 NuGet audit behavior remains transitive (`NuGetAuditMode=all`).

No application dependency package was introduced.

## Executed validation

GitHub Actions run `32859136202`, job `97838461025` completed successfully for commit `14d9ddf285a2414126a406818c8212414331c144`.

Passed:
- checkout
- .NET SDK setup
- API project graph restore
- architecture-verifier restore
- `dotnet format --verify-no-changes`
- Release build of API project graph
- Release build of architecture verifier
- architecture project-reference verification

Local .NET validation was not executed because the current local engineering runtime did not have the .NET SDK installed. CI is therefore the executable evidence for this checkpoint.

## Security review

- No secrets, broker credentials, customer logic, financial strategy logic, or pyPSX implementation added.
- Workflow permissions are `contents: read` only.
- Third-party GitHub Actions are pinned to immutable commits.
- Checkout credentials are not persisted.
- Unsafe C# code is disabled.
- checked arithmetic is enabled.
- NuGet vulnerability audit is enabled.
- Only generic scaffold is present because repository visibility/licensing is unresolved.

## Known blockers

- Issue #1: `main` branch remains unprotected.
- Issue #2: repository is public/GPL-3.0 and commercial/proprietary model is unresolved.
- Issue #3: authorization/recovery/withdrawal implementation remains open.
- Issue #4: native client trust/signed releases remain open.
- Issue #5: broker/data/reconciliation implementation remains open.
- pyPSX partner technical/commercial contract remains pending.
- No database, identity provider, ledger, OMS, risk/compliance, AI or broker runtime exists yet.

## Merge policy

Do not merge PR #6 until:
1. CI remains green on the PR head,
2. Issue #1 is resolved or explicitly risk-accepted,
3. Issue #2 is resolved or explicit owner/legal direction permits the repository model,
4. review confirms only approved generic foundation content is present.

## Exact next action

Maintain PR #6 as a draft. Resolve F0 repository governance (#1 and #2). In parallel, continue `P0-T1` broker/regulatory discovery. After F0 approval, merge the generic scaffold and activate the next permitted foundation implementation task; do not begin proprietary investment logic on the public branch before the repository model is decided.
