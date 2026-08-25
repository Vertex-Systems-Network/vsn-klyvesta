# F2 PostgreSQL Persistence Technical Acceptance

Date: 2026-08-25

Status: **TECHNICALLY ACCEPTED AS A STACKED DRAFT / NOT MERGE-AUTHORIZED**

Branch: `foundation/f2-postgres-persistence`

Pull request: #9, stacked on `foundation/f1-dotnet-skeleton` / PR #6.

Validated implementation HEAD: `0ccecf4dc5b2ffeb0873605dc0b178fc36748283`

## Scope accepted

F2 establishes generic PostgreSQL persistence safety primitives only:

- EF Core 10.0.11 / repository-local `dotnet-ef` 10.0.11;
- Npgsql EF provider 10.0.3;
- PostgreSQL 18.6 acceptance environment;
- `KlyvestaDbContext` persistence boundary;
- `ops.idempotency_record`;
- `ops.inbox_message`;
- `notification.outbox`;
- reviewed initial EF migration and model snapshot;
- database-level duplicate, lifecycle, chronology, and retry-attempt constraints;
- migration drift, apply, rollback-to-zero, and re-apply verification;
- CI revalidation of workflow and canonical-state changes.

This checkpoint does **not** accept or authorize customer PII persistence, brokerage funding, ledger/order aggregates, live broker connectivity, personalized AI advice, Guarded Auto, or real-money execution.

## Acceptance evidence

All required technical gates passed on the same validated implementation HEAD `0ccecf4dc5b2ffeb0873605dc0b178fc36748283`:

1. `dotnet-foundation` run `32874409925`
   - formatting: PASS
   - Release API graph build: PASS
   - architecture verifier build: PASS
   - architecture-boundary verification: PASS

2. `f2-postgres` run `32874409783`
   - PostgreSQL container initialization: PASS
   - infrastructure restore/format/build: PASS
   - exact PostgreSQL 18.6 numeric version verification: PASS
   - pending EF model-change check: PASS / no drift
   - migration apply: PASS
   - database constraint regression suite: PASS
   - idempotent migration script generation: PASS
   - rollback to zero: PASS
   - F2 table-removal verification: PASS
   - migration re-apply after rollback: PASS
   - post-reapply constraint regression suite: PASS

3. `codeql` run `32874409870`
   - C# build for CodeQL: PASS
   - `security-extended` analysis: PASS

## Adversarial findings resolved during F2

- EF-generated migration analyzer failures (`CA1861` / `IDE0161`) were fixed by marking the generated migration correctly rather than disabling analyzers globally.
- PostgreSQL version verification was changed from distro-sensitive display text to exact `server_version_num = 180006` enforcement.
- the pending-outbox index was corrected to index `(next_attempt_at, occurred_at)` under `published_at IS NULL`.
- no implicit/global Npgsql retry policy is enabled; future retries must be operation-specific and idempotency-aware.
- CI PostgreSQL credentials are ephemeral per workflow run rather than a static committed password.
- completion/processing timestamp chronology is enforced at the database layer.

## Preserved invariants

- no authoritative `float`/`double` money model introduced;
- no customer balance table introduced;
- no broker/order/fill state fabricated;
- no AI/LLM execution path introduced;
- no real-money side effects exist in this branch;
- database uniqueness remains the final duplicate-creation barrier;
- outbox publication failure cannot become financial truth;
- production startup does not automatically apply migrations.

## Governance result

F2 is technically green but remains a **stacked Draft PR**.

The canonical active task remains `P0-T1 — Confirm regulatory and pyPSX Broker API operating model`.

`accepted_phases` remains unchanged. No Phase 0/1/2/3/4 gate is claimed complete by this checkpoint.

Known merge/production blockers remain:

- Issue #1 — main branch protection/ruleset is still open;
- Issue #2 — repository visibility / GPL-3.0 / proprietary licensing decision is still open;
- pyPSX technical/commercial onboarding and regulated operating-model evidence are still pending;
- production real-money launch remains blocked by the canonical security/regulatory gates.

## Exact continuation

Do not merge PR #9 to `main` from this checkpoint alone.

Continue canonical task `P0-T1`: obtain the pyPSX Broker API technical/commercial onboarding response, map it against `contracts/broker/BROKER_ADAPTER_V1.md`, and update broker/data-flow/threat-model/acceptance evidence before any real-money integration.
