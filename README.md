# Klyvesta — AI-Native Investing Platform for PSX

Repository target: `Vertex-Systems-Network/vsn-klyvesta`

Klyvesta is an **AI Investment Operating System** designed around the pyPSX Broker API. It supports three customer modes:

1. **Manual** — the customer chooses and confirms each trade.
2. **AI Assisted** — AI researches, constructs recommendations, explains risk, and the customer confirms.
3. **Guarded Auto** — AI manages a portfolio only inside a customer-approved mandate and only after the required legal/regulatory structure is approved.

The product direction includes beginner-first investing, tiered AI-agent packages, a complete investment-event timeline, Email/WhatsApp/SMS notifications, advanced portfolio intelligence, and a full-featured manual + AI-assisted + Guarded Auto experience.

## Non-negotiable product principle

Klyvesta must never claim or imply that losses are impossible, that investing is risk-free, or that profit is guaranteed. Markets can decline and investments can lose value. The platform objective is to reduce avoidable risk, control drawdown, diversify appropriately, adapt exposure, and optimize risk-adjusted outcomes — not guarantee profit or capital preservation.

## Table of contents

- [PyPSX integration batch plan — `33%` `███░░░░░░░`](#pypsx-integration-batch-plan)
- [Current project status — `86%` `█████████░`](#current-project-status)
- [Demo UI preview](#demo-ui-preview--db--pypsx-bypass)
- [Module delivery table](#module-delivery-table)
- [Repository model](#repository-model)
- [Engineering governance](#engineering-governance)
- [Core planning documents](#core-planning-documents)
- [Implementation foundation V1](#implementation-foundation-v1)
- [Canonical AI engineering state](#canonical-ai-engineering-state)
- [ANPOS project adoption — `100%` `██████████`](#anpos-project-adoption)
- [ANPOS Batch 0 baseline — `100%` `██████████`](docs/ANPOS_BATCH_0_BASELINE_2026-09-30.md)
- [ANPOS Batch 1 reconciliation — `100%` `██████████`](docs/ANPOS_BATCH_1_PYPSX_RECONCILIATION_2026-09-30.md)
- [ANPOS Batch 2 evidence gate — `0%` `░░░░░░░░░░`](docs/ANPOS_BATCH_2_PARTNER_REGULATORY_GATE_2026-09-30.md)
- [ANPOS Batch 2 public API evidence — `D/T` sandbox evidence complete / `C-L` pending](docs/ANPOS_BATCH_2_PUBLIC_API_EVIDENCE_2026-09-30.md)
- [ANPOS Batch 3 credential/environment boundary — `██████████ 100%`](docs/ANPOS_BATCH_3_CREDENTIAL_ENVIRONMENT_BOUNDARY_2026-09-30.md)
- [ANPOS Batch 4 sandbox adapter — `█████░░░░░ 50%`](docs/ANPOS_BATCH_4_SANDBOX_ADAPTER_2026-09-30.md)
- [ANPOS Batch 4 order adapter slice](docs/ANPOS_BATCH_4_ORDER_ADAPTER_SLICE_2026-09-30.md)

## PyPSX integration batch plan

This plan separates PyPSX work into independently verifiable batches. It does not authorize live trading, customer KYC, custody, funding, or production promotion. Every batch must preserve the repository's fail-closed non-live boundary.

**Program progress:** `███░░░░░░░ 33%` — **3/9 batches complete**

| Batch | Progress | Scope | Status |
| --- | --- | --- | --- |
| 0. Evidence recovery | `██████████ 100%` | Repository state, governance, roadmap, issues, branches, CI and broker contracts baseline. | Complete — baseline recorded |
| 1. Documentation reconciliation | `██████████ 100%` | Map PyPSX docs to broker adapter, OpenAPI and capability matrix. | Complete — full endpoint reconciliation recorded |
| 2. Partner and regulatory gate | `████░░░░░░ 40%` | Public API evidence and controlled sandbox order/fill/reconciliation plus negative auth test. | Technical evidence recorded; invalid-order provider defect remains open — partner/legal gates blocked |
| 3. Credential and environment boundary | `██████████ 100%` | Sandbox-only acceptance workflow, URL allowlist, secret masking and production fail-closed guard. | Complete — sandbox passed; production target failed closed as designed |
| 4. Sandbox adapter | `█████░░░░░ 50%` | Sandbox-only adapter boundary, normalized read-only results and guarded HTTP/order client. | Read-only + order adapter merged; quotes, fees and streaming pending |
| 5. Reliability and reconciliation | `░░░░░░░░░░ 0%` | Idempotency, replay, stale data, outage, retry and reconciliation tests. | Planned |
| 6. Panel and operations workflow | `░░░░░░░░░░ 0%` | Backend-only panel operations, access matrix and audit trail. | Planned |
| 7. Security and CI acceptance | `░░░░░░░░░░ 0%` | Protected-main, review, security and promotion evidence. | Blocked — ruleset/admin evidence |
| 8. Sandbox certification and production decision | `░░░░░░░░░░ 0%` | Sandbox certification and separate production go/no-go decision. | Planned |

Implementation order is **0 → 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8**. A blocked batch cannot be marked complete by documentation alone.

## ANPOS project adoption

This repository adopts the **AI Native Project Operating System (ANPOS) 1.4.0** as a project-local engineering and governance protocol. The adoption aligns AI work around evidence-first planning, role-aware context routing, protected control-plane paths, traceability, security review and explicit release gates.

This is a child-project adoption of the canonical blueprint. It does **not** copy or activate the blueprint's commercial service, Marketplace assets, live PM mappings, external AI pools, credentials, GitHub Rules or canonical-source-only workflows.

Project-specific behavior remains governed by `AI-PLAN.md`, `.ai/MASTER_ENGINEERING_PROMPT.md`, `.ai/state.json`, `.ai/guardrails.md`, `.ai/acceptance-gates.yaml`, accepted ADRs, contracts, tests and Git history.

See [docs/ANPOS_ADOPTION.md](docs/ANPOS_ADOPTION.md), [.ai/manifest.json](.ai/manifest.json), [config/protocol/instance.json](config/protocol/instance.json) and [config/protocol/version.json](config/protocol/version.json).

ANPOS adoption does not authorize live trading, customer KYC, custody, funding, withdrawals, production deployment or regulatory claims. PyPSX integration remains governed by the evidence-gated batch plan above.

Batch 2 evidence-gate status: `████░░░░░░ 40%` — official public API evidence plus sandbox order/fill/reconciliation and invalid-auth evidence are recorded; invalid-order handling exposed a provider HTTP 500 defect; cancel execution, direct partner contract and legal/regulatory evidence remain pending. Batch 3 boundary implementation: `██████████ 100%` — sandbox acceptance passed and production-target fail-closed proof passed (expected guard failure). Batch 4 adapter: `█████░░░░░ 50%` — sandbox-only read + order adapter merged; provider-dependent market-data, fees and streaming remain unverified.

## Current project status

### Overall repository delivery

- Repository-owned non-live engineering: `█████████░ 86%`
- ANPOS/PyPSX batch program: `██░░░░░░░░ 22%` — 2 of 9 batches complete
- Live/real-money production authority: `░░░░░░░░░░ 0%` — intentionally fail-closed


**Last status update:** `2026-09-14 — demo UI preview added`

**Repository-owned non-live engineering:** `█████████░ 86%` — **12 of 14 canonical module lanes are accepted/integrated on `parallel/integration-staging`.**

**Overall delivery status:** **NON-LIVE STAGING ACTIVE / LIVE PRODUCTION BLOCKED**

API-independent, **non-live engineering is active and integrated through `parallel/integration-staging`**. The accepted staging baseline includes deterministic paper/simulation boundaries for core investing, brokerage trust, identity/authorization, risk, compliance, ledger, notifications, observability, performance/resilience and platform CI.

**Live/real-money pyPSX operation is not authorized.** Production brokerage remains fail-closed until direct pyPSX partner API/contract evidence, credentials and exact provider semantics are available; required legal/regulatory/provider approvals are complete; and repository governance permits production promotion.

The two remaining repository-owned canonical module lanes are:
- **Database Integration** — ready, not yet started/integrated.
- **Security Acceptance** — blocked pending production governance/provider/legal/runtime evidence.

The first production acceptance gate remains regulatory + broker fit:
- pyPSX Broker API production capabilities confirmed.
- Underlying regulated broker/custody structure confirmed.
- Regulatory treatment of AI recommendations confirmed.
- Regulatory treatment of discretionary/automatic portfolio management confirmed.
- Required adviser/securities-manager licence or licensed partner arrangement confirmed.

The implementation foundation and non-live safety boundaries may be validated independently, but no real-money capability is unlocked until the canonical acceptance gates are satisfied.

## Demo UI preview — DB + pyPSX bypass

A **demo-only investor UI** is available so the current product experience can be reviewed without PostgreSQL, pyPSX credentials, live market data, real customer records, or real-money execution.

The preview includes:
- demo login screen;
- portfolio summary and holdings;
- synthetic PSX watchlist data;
- paper-order activity;
- AI-insight panel;
- explicit demo-safety indicators.

### Demo credentials

- Email: `demo@klyvesta.local`
- Password: `demo`

These credentials are intentionally public and are **not** a security boundary. The demo session is only a UI preview gate.

### Safety rules

- Demo mode is allowed only when `ASPNETCORE_ENVIRONMENT` is `Development` or `Demo`.
- Enabling `DemoMode:Enabled=true` under `Production` causes startup to fail closed.
- Demo mode does not register or query the production database.
- Demo mode does not contain pyPSX credentials and performs no pyPSX network calls.
- All portfolio, order, quote and AI-insight values are synthetic.
- The preview has no production authentication, KYC, PII, funding, withdrawal or real-order authority.

### Local server installation

Prerequisite: **.NET SDK 10.0.400** (see `global.json`).

```bash
git clone https://github.com/Vertex-Systems-Network/vsn-klyvesta.git
cd vsn-klyvesta
dotnet restore
dotnet run --project src/Klyvesta.Api --launch-profile Klyvesta.Demo
```

Open:

```text
http://localhost:5080/
```

The local launch profile sets `ASPNETCORE_ENVIRONMENT=Development`, which enables the DB/pyPSX-free demo preview through `appsettings.Development.json`.

To verify the bypass state directly:

```text
http://localhost:5080/api/demo/status
```

Expected state includes `database=bypassed`, `pypsx=not-connected`, `realMoney=false` and `productionAuthority=false`.

### Live demo on a VPS / cloud server

This mode is for a **public or private UI preview only**, not live investing.

Publish the application:

```bash
dotnet publish src/Klyvesta.Api/Klyvesta.Api.csproj -c Release -o ./publish
```

Run the published build in the dedicated `Demo` environment:

```bash
cd publish
ASPNETCORE_ENVIRONMENT=Demo \
ASPNETCORE_URLS=http://0.0.0.0:5080 \
./Klyvesta.Api
```

On Windows PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Demo"
$env:ASPNETCORE_URLS="http://0.0.0.0:5080"
.\Klyvesta.Api.exe
```

For an internet-facing preview, place the app behind **HTTPS** using Nginx, Apache, IIS, Caddy or the hosting provider's reverse proxy. Do not expose port `5080` directly if the host can terminate HTTPS for you.

Optional demo credential override:

```text
DemoMode__Email=your-demo@example.com
DemoMode__Password=replace-this-demo-password
```

These variables only protect casual access to a fake preview. They must never be treated as production authentication.

### Shared hosting / Plesk / IIS hosting

The shared host must support ASP.NET Core / .NET 10, or allow a self-contained .NET executable.

For a Windows x64 shared host that permits self-contained ASP.NET Core apps:

```bash
dotnet publish src/Klyvesta.Api/Klyvesta.Api.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -o ./publish
```

For a Linux x64 shared host that permits a persistent .NET process:

```bash
dotnet publish src/Klyvesta.Api/Klyvesta.Api.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -o ./publish
```

Then:

1. Upload the contents of `publish/` to the application's web root or application directory.
2. Set `ASPNETCORE_ENVIRONMENT=Demo` in the hosting control panel.
3. Optionally set `DemoMode__Email` and `DemoMode__Password`.
4. Configure the host to start `Klyvesta.Api.exe` on Windows or `./Klyvesta.Api` on Linux.
5. Point the domain/subdomain to the ASP.NET Core application and enable HTTPS.
6. Open `/` or `/demo/login.html` and sign in with the demo credentials.

If the shared host only supports static PHP/HTML hosting and cannot run ASP.NET Core processes, this repository cannot run there directly; use a .NET-capable VPS/app service or a host with ASP.NET Core support.

### Real/live production installation

There is intentionally **no real-money production activation procedure yet**. A production deployment must not enable demo mode and must not fabricate pyPSX integration. Real production requires the unresolved security, governance, legal/provider and pyPSX partner gates to be completed first.

Production must use:

```text
ASPNETCORE_ENVIRONMENT=Production
DemoMode__Enabled=false
```

Database credentials, broker credentials and production secrets must be supplied only through an approved secret-management mechanism and must never be committed to this repository.

## Module delivery table

> Progress is for the **current repository-owned non-live engineering scope**. `100%` does **not** mean production/live/regulatory approval. Provider, legal and pyPSX-dependent work remains separately gated.

| Module | Progress | Start Date | End Date | Status |
| --- | --- | --- | --- | --- |
| Orders / OMS | `██████████ 100%` | 2026-09-01 | 2026-09-03 | Integrated — deterministic paper/non-live state machine |
| Portfolio & Reconciliation | `██████████ 100%` | 2026-09-01 | 2026-09-03 | Integrated — paper projection/reconciliation boundary |
| Risk | `██████████ 100%` | 2026-09-01 | 2026-09-03 | Integrated — deterministic paper risk governor |
| Compliance | `██████████ 100%` | 2026-09-01 | 2026-09-03 | Integrated — deterministic paper compliance gate |
| AI Shadow | `██████████ 100%` | 2026-09-01 | 2026-09-03 | Integrated — AI proposal/paper-shadow boundary; no direct execution authority |
| Brokerage Trust / Paper Broker | `██████████ 100%` | 2026-09-01 | 2026-09-14 | Integrated — API-independent broker trust + paper broker complete; live pyPSX adapter is a separate external gate |
| Identity & Authorization | `██████████ 100%` | 2026-09-03 | 2026-09-14 | Integrated — withdrawal, session/device revocation and maker-checker break-glass hardening |
| Ledger | `██████████ 100%` | 2026-09-03 | 2026-09-14 | Integrated — immutable non-live double-entry boundary |
| Notifications | `██████████ 100%` | 2026-09-03 | 2026-09-14 | Integrated — provider-neutral delivery boundary; production provider pending |
| Observability | `██████████ 100%` | 2026-09-03 | 2026-09-14 | Integrated — structured secret-safe boundary; production telemetry provider pending |
| Performance & Resilience | `██████████ 100%` | 2026-09-03 | 2026-09-14 | Integrated — deterministic acceptance contract; production SLO evidence pending |
| Platform / CI | `██████████ 100%` | 2026-09-03 | 2026-09-14 | Integrated — multi-agent ownership, verifier and CI control plane |
| Database Integration | `░░░░░░░░░░ 0%` | — | — | Ready — canonical lane available, work not yet started/integrated |
| Security Acceptance | `░░░░░░░░░░ 0%` | — | — | Blocked — production governance/provider/legal/runtime evidence required |
| pyPSX Live Integration | `░░░░░░░░░░ 0%` | — | — | External blocker — partner API/contract not yet available |

## Today’s close plan — 2026-09-14

| Work item | Start Date | End Date | Status |
| --- | --- | --- | --- |
| AI-agent prompt-injection hardening | 2026-09-14 | 2026-09-14 | Done |
| EF Core dependency alignment | 2026-09-14 | 2026-09-14 | Done |
| Broker trust canonicalization + staging merge | 2026-09-14 | 2026-09-14 | Done |
| Withdrawal/session/break-glass canonicalization | 2026-09-14 | 2026-09-14 | Done |
| README module/status reconciliation | 2026-09-14 | 2026-09-14 | Done |
| DB + pyPSX-free demo UI preview | 2026-09-14 | 2026-09-14 | Done — local/VPS/shared-host preview instructions included |
| Stale/experimental PR cleanup | 2026-09-14 | 2026-09-14 | Done |
| Database Integration canonical lane | — | — | Ready / next repository-owned module |
| Security Acceptance | — | — | Blocked by production evidence/governance gates |
| pyPSX live adapter | — | — | Blocked until partner API/contract arrives |

## Repository model

This repository uses an approved **split distribution model**:

- `vsn-klyvesta` remains public and GPL-3.0 for intentionally open/generic foundation code, public architecture/contracts/examples and public due-diligence material.
- Proprietary investment strategy, confidential broker material, customer/business logic intended to remain closed, production secrets and customer data must not be committed here.
- Proprietary production components require a separately approved private repository/security boundary and separate licence/dependency review.
- Existing GPL-3.0 history is not silently relicensed.

See `docs/REPOSITORY_LICENSING_MODEL.md` and `docs/REPOSITORY_GOVERNANCE.md`.

## Engineering governance

All AI/human engineering sessions must begin with `AGENTS.md` and `.ai/MASTER_ENGINEERING_PROMPT.md`, then read the machine-readable project state, guardrails, acceptance gates, and latest checkpoint before implementation.

Repository evidence, tests, documentation, and Git history are the source of truth; chat memory is not.

The normal workflow is protected-main + pull request + required CI/security checks + review. `main` must remain fail-closed for substantive production/security-sensitive promotion while hosted branch protection/ruleset enforcement is unresolved, unless a new explicit owner risk decision narrowly authorizes a named change.

## Core planning documents

- `AI-PLAN.md`
- `docs/PRODUCT_VISION_V2.md`
- `docs/PRODUCT_REQUIREMENTS.md`
- `docs/AI_AGENT_PACKAGES.md`
- `docs/INVESTMENT_EVENT_NOTIFICATIONS.md`
- `docs/COMPETITIVE_POSITIONING.md`
- `docs/ARCHITECTURE.md`
- `docs/DATA_FLOW.md`
- `docs/AI_AGENTS.md`
- `docs/RISK_COMPLIANCE.md`
- `docs/SECURITY.md`
- `docs/SECURITY_ARCHITECTURE_AUDIT_V1.md`
- `docs/THREAT_MODEL.md`
- `docs/PLATFORM_STACK_V2.md`
- `docs/DESIGN_SYSTEM.md`
- `docs/QA_PERFORMANCE.md`
- `docs/ROADMAP.md`
- `docs/REPOSITORY_GOVERNANCE.md`
- `docs/REPOSITORY_LICENSING_MODEL.md`

## Implementation foundation V1

- `docs/DOMAIN_DATABASE_MODEL_V1.md`
- `docs/AUTH_SESSION_ARCHITECTURE_V1.md`
- `docs/AUTHORIZATION_PRIVILEGE_MODEL.md`
- `docs/AUTHORIZATION_MATRIX_V1.md`
- `docs/ORDER_LEDGER_STATE_MACHINES_V1.md`
- `docs/API_CONTRACT_BASELINE_V1.md`
- `contracts/openapi/klyvesta.v1.yaml`
- `contracts/broker/BROKER_ADAPTER_V1.md`
- `docs/FOUNDATION_IMPLEMENTATION_PLAN_V1.md`

Accepted planning ADRs:
- `docs/adr/ADR-001-AI-CANNOT-EXECUTE-DIRECTLY.md`
- `docs/adr/ADR-002-PLATFORM-STACK-AND-NATIVE-CLIENTS.md`
- `docs/adr/ADR-003-AUTHENTICATION-AND-FINANCIAL-API-SECURITY.md`
- `docs/adr/ADR-004-FINANCIAL-DATA-INTEGRITY-AND-STATE-MACHINES.md`

## Canonical AI engineering state

- `AGENTS.md`
- `.ai/MASTER_ENGINEERING_PROMPT.md`
- `.ai/state.json`
- `.ai/guardrails.md`
- `.ai/acceptance-gates.yaml`
- latest checkpoint path is recorded in `.ai/state.json`
