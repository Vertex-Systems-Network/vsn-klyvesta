# Development Foundation

Status: F1 scaffold candidate on a feature branch. This file does not activate real-money trading or pyPSX integration.

## Toolchain

- .NET SDK: `10.0.400` via `global.json`
- Target framework: `net10.0`
- C#: 14
- NuGet source: official `nuget.org` only until a reviewed private registry is introduced

The pinned SDK was selected from the current .NET 10 LTS download line on 2026-08-25. Security patches must be reviewed regularly; upgrading the SDK is a controlled change, not a permanent freeze.

## Project boundaries

```text
Klyvesta.Domain
      ^
      |
Klyvesta.Application
      ^
      |
Klyvesta.Infrastructure
      ^
      |
Klyvesta.Api
```

The actual allowed references are enforced by `tools/Klyvesta.ArchitectureVerifier`:

- Domain -> nothing inside Klyvesta
- Application -> Domain
- Infrastructure -> Application + Domain
- API -> Application + Infrastructure

## Current scaffold commands

```bash
dotnet --version
dotnet restore src/Klyvesta.Api/Klyvesta.Api.csproj
dotnet restore tools/Klyvesta.ArchitectureVerifier/Klyvesta.ArchitectureVerifier.csproj
dotnet format src/Klyvesta.Api/Klyvesta.Api.csproj --verify-no-changes --no-restore
dotnet build src/Klyvesta.Api/Klyvesta.Api.csproj -c Release --no-restore
dotnet build tools/Klyvesta.ArchitectureVerifier/Klyvesta.ArchitectureVerifier.csproj -c Release --no-restore
dotnet run --project tools/Klyvesta.ArchitectureVerifier/Klyvesta.ArchitectureVerifier.csproj -c Release --no-build
```

## Security defaults

- warnings are errors;
- .NET analyzers enabled;
- NuGet vulnerability audit enabled for direct and transitive dependencies;
- checked arithmetic enabled by default;
- unsafe code disabled;
- CI third-party actions pinned to immutable commit SHAs;
- checkout credentials are not persisted;
- no production secrets belong in repository/appsettings/client code;
- only liveness/readiness and a non-financial service marker exist in the API scaffold.

## Health endpoints

- `GET /health/live` — process liveness only.
- `GET /health/ready` — readiness health checks. External dependencies will be added later and must fail/read-degrade according to policy.

## Not implemented yet

- identity provider integration;
- database/EF Core/Npgsql;
- migrations;
- ledger;
- OMS;
- risk/compliance engines;
- broker adapter implementation;
- pyPSX integration;
- AI/quant runtime;
- production observability;
- deployment/container manifests.

These omissions are intentional. They belong to later gated foundation tasks.
