# M-AGENT-06 Database Integration Audit

- Branch: `parallel/database-integration`
- Accepted baseline: `54a968b51f0ccca59a8b5654209f6aefa8a546ad`
- Role: database-integration-agent
- Scope: migrations, model snapshots, database model documentation, work-item/checkpoint evidence only
- Module source modification: **not authorized and not performed**

## Inventory

Canonical persistence root at the accepted baseline:

- `src/Klyvesta.Infrastructure/Persistence/KlyvestaDbContext.cs`
- `src/Klyvesta.Infrastructure/Persistence/Migrations/20260825153544_F2InitialPersistence.cs`
- `src/Klyvesta.Infrastructure/Persistence/Migrations/20260825153544_F2InitialPersistence.Designer.cs`
- `src/Klyvesta.Infrastructure/Persistence/Migrations/KlyvestaDbContextModelSnapshot.cs`

There is one timestamped migration in the canonical migration directory and one current model snapshot.

## Migration order validation

**PASS for the current baseline.**

The migration sequence contains a single timestamped migration, `20260825153544_F2InitialPersistence`, so there is no sibling timestamp collision, ambiguous ordering, duplicate migration identifier or later migration that could be reordered ahead of it.

No new migration is generated in this checkpoint. Ledger, Identity/Authorization, Notifications and other parallel module branches have not yet been accepted into `parallel/integration-staging`; generating persistence artifacts against unintegrated module source would create a stale/competing snapshot and violate M-AGENT-06 single-writer ownership.

## Snapshot consistency

**PASS for the currently integrated persistence model.**

`KlyvestaDbContext`, the initial migration and `KlyvestaDbContextModelSnapshot` consistently describe these records:

1. `IdempotencyRecord` -> `ops.idempotency_record`
   - primary key `pk_idempotency_record`
   - unique `(scope, key)` index `ux_idempotency_record_scope_key`
   - expiry index `ix_idempotency_record_expires_at`
   - state, expiry and completion chronology checks
2. `InboxMessage` -> `ops.inbox_message`
   - primary key `pk_inbox_message`
   - unique `(provider, message_id)` index `ux_inbox_message_provider_message_id`
   - `(state, received_at)` index `ix_inbox_message_state_received_at`
   - state and processed chronology checks
3. `OutboxMessage` -> `notification.outbox`
   - primary key `pk_outbox`
   - filtered pending index `ix_outbox_pending` on `(next_attempt_at, occurred_at)` where `published_at IS NULL`
   - non-negative attempt-count check

The snapshot reports EF product version `10.0.11`, which matches the centrally pinned `Microsoft.EntityFrameworkCore` / `Microsoft.EntityFrameworkCore.Design` version at this baseline.

## Dependency-version coordination finding

Recent canonical CI output has emitted an `MSB3277` warning showing `Microsoft.EntityFrameworkCore.Relational` `10.0.4` and `10.0.11` in the same API graph. The database-integration lane does **not** own `Directory.Packages.props`, provider-package selection or shared dependency files, so it does not mutate them here.

This must be treated as a shared dependency/release coordination item rather than silently repaired from the migration lane. Before production database acceptance, the shared dependency owner should prove one coherent EF/Npgsql relational dependency graph on the exact integration head.

## Rollback analysis

The initial migration `Down()` removes all three persistence tables:

- `ops.idempotency_record`
- `ops.inbox_message`
- `notification.outbox`

That rollback is structurally valid for an empty/non-live development database but is **data-destructive** after real writes. Therefore:

- do not represent `database update 0` / migration rollback as a safe production rollback after data exists;
- production rollback requires a tested restore/forward-fix strategy and retained database backup/restore evidence;
- later financial-domain migrations must include explicit expand/contract and rollback analysis rather than assuming table-drop reversal is acceptable;
- migration promotion must remain serialized through this lane to prevent snapshot races.

## Parallel-lane handoff rule

When a module PR requiring persistence is accepted into `parallel/integration-staging`:

1. refresh this branch to the accepted integration head;
2. inspect the integrated model/source contract;
3. generate or hand-author exactly one ordered migration under this lane;
4. update the model snapshot in the same atomic change;
5. verify upgrade from the previous accepted migration state;
6. inspect generated SQL for destructive operations and lock risk;
7. document forward-fix / restore behavior;
8. only then submit database integration for the one-at-a-time merge train.

No parallel module agent is authorized to generate its own competing EF migration or model snapshot.

## Current result

- migration-order-validation: **PASS**
- snapshot-consistency: **PASS**
- rollback-analysis: **PASS WITH PRODUCTION DESTRUCTIVE-ROLLBACK WARNING**
- new migration required now: **NO — wait for accepted integrated model change**
- module-source writes: **0**
- production database mutation: **0**
