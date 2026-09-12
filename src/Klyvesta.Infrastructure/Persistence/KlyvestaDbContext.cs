using Klyvesta.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Klyvesta.Infrastructure.Persistence;

public sealed class KlyvestaDbContext(DbContextOptions<KlyvestaDbContext> options) : DbContext(options)
{
    internal DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    internal DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    // P1 Domain Records
    internal DbSet<OrderIntentRecord> OrderIntents => Set<OrderIntentRecord>();
    internal DbSet<ExecutionRecord> Executions => Set<ExecutionRecord>();
    internal DbSet<PositionRecord> Positions => Set<PositionRecord>();
    internal DbSet<LedgerJournalRecord> LedgerJournals => Set<LedgerJournalRecord>();
    internal DbSet<LedgerPostingRecord> LedgerPostings => Set<LedgerPostingRecord>();
    internal DbSet<BrokerOrderRecord> BrokerOrders => Set<BrokerOrderRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureIdempotency(modelBuilder);
        ConfigureInbox(modelBuilder);
        ConfigureOutbox(modelBuilder);
        
        // P1 Domain Configurations
        ConfigureOrderIntents(modelBuilder);
        ConfigureExecutions(modelBuilder);
        ConfigurePositions(modelBuilder);
        ConfigureLedgerJournals(modelBuilder);
        ConfigureLedgerPostings(modelBuilder);
        ConfigureBrokerOrders(modelBuilder);
    }

    private static void ConfigureIdempotency(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("idempotency_record", "ops", table =>
            {
                table.HasCheckConstraint(
                    "ck_idempotency_record_state",
                    "state IN ('in_progress', 'completed', 'failed')");
                table.HasCheckConstraint(
                    "ck_idempotency_record_expiry",
                    "expires_at > created_at");
                table.HasCheckConstraint(
                    "ck_idempotency_record_completion_chronology",
                    "completed_at IS NULL OR completed_at >= created_at");
            });

            entity.HasKey(item => item.Id).HasName("pk_idempotency_record");

            entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(item => item.Scope).HasColumnName("scope").HasMaxLength(128).IsRequired();
            entity.Property(item => item.Key).HasColumnName("key").HasMaxLength(200).IsRequired();
            entity.Property(item => item.RequestHash).HasColumnName("request_hash").HasColumnType("character(64)").IsRequired();
            entity.Property(item => item.State).HasColumnName("state").HasMaxLength(32).IsRequired();
            entity.Property(item => item.OperationId).HasColumnName("operation_id");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(item => item.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamp with time zone");
            entity.Property(item => item.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone").IsRequired();

            entity.HasIndex(item => new { item.Scope, item.Key })
                .IsUnique()
                .HasDatabaseName("ux_idempotency_record_scope_key");

            entity.HasIndex(item => item.ExpiresAt)
                .HasDatabaseName("ix_idempotency_record_expires_at");
        });
    }

    private static void ConfigureInbox(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("inbox_message", "ops", table =>
            {
                table.HasCheckConstraint(
                    "ck_inbox_message_state",
                    "state IN ('received', 'processing', 'processed', 'failed')");
                table.HasCheckConstraint(
                    "ck_inbox_message_processing_chronology",
                    "processed_at IS NULL OR processed_at >= received_at");
            });

            entity.HasKey(item => item.Id).HasName("pk_inbox_message");

            entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(item => item.Provider).HasColumnName("provider").HasMaxLength(64).IsRequired();
            entity.Property(item => item.MessageId).HasColumnName("message_id").HasMaxLength(256).IsRequired();
            entity.Property(item => item.PayloadHash).HasColumnName("payload_hash").HasColumnType("character(64)").IsRequired();
            entity.Property(item => item.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.State).HasColumnName("state").HasMaxLength(32).IsRequired();
            entity.Property(item => item.ReceivedAt).HasColumnName("received_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(item => item.ProcessedAt).HasColumnName("processed_at").HasColumnType("timestamp with time zone");

            entity.HasIndex(item => new { item.Provider, item.MessageId })
                .IsUnique()
                .HasDatabaseName("ux_inbox_message_provider_message_id");

            entity.HasIndex(item => new { item.State, item.ReceivedAt })
                .HasDatabaseName("ix_inbox_message_state_received_at");
        });
    }

    private static void ConfigureOutbox(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox", "notification", table =>
            {
                table.HasCheckConstraint(
                    "ck_outbox_attempt_count",
                    "attempt_count >= 0");
            });

            entity.HasKey(item => item.Id).HasName("pk_outbox");

            entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(item => item.EventType).HasColumnName("event_type").HasMaxLength(256).IsRequired();
            entity.Property(item => item.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.HeadersJson).HasColumnName("headers_json").HasColumnType("jsonb");
            entity.Property(item => item.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(item => item.PublishedAt).HasColumnName("published_at").HasColumnType("timestamp with time zone");
            entity.Property(item => item.AttemptCount).HasColumnName("attempt_count").IsRequired();
            entity.Property(item => item.NextAttemptAt).HasColumnName("next_attempt_at").HasColumnType("timestamp with time zone");
            entity.Property(item => item.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(128);

            entity.HasIndex(item => new { item.NextAttemptAt, item.OccurredAt })
                .HasFilter("published_at IS NULL")
                .HasDatabaseName("ix_outbox_pending");
        });
    }

    private static void ConfigureOrderIntents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderIntentRecord>(entity =>
        {
            entity.ToTable("order_intent", "trading");

            entity.HasKey(e => e.Id).HasName("pk_order_intent");

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.CustomerId).HasColumnName("customer_id").IsRequired();
            entity.Property(e => e.Symbol).HasColumnName("symbol").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Side).HasColumnName("side").IsRequired();
            entity.Property(e => e.Type).HasColumnName("type").IsRequired();
            entity.Property(e => e.Quantity).HasColumnName("quantity").HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.LimitPrice).HasColumnName("limit_price").HasPrecision(18, 4);
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(3).HasDefaultValue("PKR");
            entity.Property(e => e.TimeInForce).HasColumnName("time_in_force").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128);
            entity.Property(e => e.BrokerOrderId).HasColumnName("broker_order_id");
            entity.Property(e => e.RejectionReason).HasColumnName("rejection_reason");
            entity.Property(e => e.RejectionDetails).HasColumnName("rejection_details").HasMaxLength(500);
            entity.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
            entity.Property(e => e.SubmittedAtUtc).HasColumnName("submitted_at_utc").HasColumnType("timestamp with time zone");

            entity.HasIndex(e => e.CustomerId).HasDatabaseName("ix_order_intent_customer_id");
            entity.HasIndex(e => e.Status).HasDatabaseName("ix_order_intent_status");
            entity.HasIndex(e => e.IdempotencyKey).IsUnique().HasDatabaseName("ux_order_intent_idempotency_key").HasFilter("idempotency_key IS NOT NULL");
        });
    }

    private static void ConfigureExecutions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExecutionRecord>(entity =>
        {
            entity.ToTable("execution", "trading");

            entity.HasKey(e => e.Id).HasName("pk_execution");

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.OrderIntentId).HasColumnName("order_intent_id").IsRequired();
            entity.Property(e => e.CustomerId).HasColumnName("customer_id").IsRequired();
            entity.Property(e => e.ExecutionId).HasColumnName("execution_id").HasMaxLength(128).IsRequired();
            entity.Property(e => e.Quantity).HasColumnName("quantity").HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.PricePerUnit).HasColumnName("price_per_unit").HasPrecision(18, 4).IsRequired();
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
            entity.Property(e => e.ExecutedAtUtc).HasColumnName("executed_at_utc").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(e => e.BrokerReference).HasColumnName("broker_reference").HasMaxLength(256);
            entity.Property(e => e.RecordedAtUtc).HasColumnName("recorded_at_utc").HasColumnType("timestamp with time zone").IsRequired();

            entity.HasIndex(e => e.OrderIntentId).HasDatabaseName("ix_execution_order_intent_id");
            entity.HasIndex(e => new { e.CustomerId, e.ExecutionId }).IsUnique().HasDatabaseName("ux_execution_customer_execution_id");
        });
    }

    private static void ConfigurePositions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PositionRecord>(entity =>
        {
            entity.ToTable("position", "trading");

            entity.HasKey(e => e.Id).HasName("pk_position");

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.CustomerId).HasColumnName("customer_id").IsRequired();
            entity.Property(e => e.Symbol).HasColumnName("symbol").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Quantity).HasColumnName("quantity").HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.AverageCostBasis).HasColumnName("average_cost_basis").HasPrecision(18, 4).IsRequired();
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
            entity.Property(e => e.LastUpdatedAtUtc).HasColumnName("last_updated_at_utc").HasColumnType("timestamp with time zone").IsRequired();

            entity.HasIndex(e => new { e.CustomerId, e.Symbol }).IsUnique().HasDatabaseName("ux_position_customer_symbol");
        });
    }

    private static void ConfigureLedgerJournals(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LedgerJournalRecord>(entity =>
        {
            entity.ToTable("ledger_journal", "accounting");

            entity.HasKey(e => e.Id).HasName("pk_ledger_journal");

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
            entity.Property(e => e.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128);
            entity.Property(e => e.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128);
            entity.Property(e => e.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();

            entity.HasIndex(e => e.CorrelationId).HasDatabaseName("ix_ledger_journal_correlation_id");
            entity.HasIndex(e => e.IdempotencyKey).IsUnique().HasDatabaseName("ux_ledger_journal_idempotency_key").HasFilter("idempotency_key IS NOT NULL");
        });
    }

    private static void ConfigureLedgerPostings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LedgerPostingRecord>(entity =>
        {
            entity.ToTable("ledger_posting", "accounting");

            entity.HasKey(e => e.Id).HasName("pk_ledger_posting");

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.JournalId).HasColumnName("journal_id").IsRequired();
            entity.Property(e => e.AccountId).HasColumnName("account_id").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("amount").HasPrecision(18, 4).IsRequired();
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
            entity.Property(e => e.IsDebit).HasColumnName("is_debit").IsRequired();
            entity.Property(e => e.Reference).HasColumnName("reference").HasMaxLength(256);
            entity.Property(e => e.PostedAtUtc).HasColumnName("posted_at_utc").HasColumnType("timestamp with time zone").IsRequired();

            entity.HasIndex(e => e.JournalId).HasDatabaseName("ix_ledger_posting_journal_id");
            entity.HasIndex(e => e.AccountId).HasDatabaseName("ix_ledger_posting_account_id");
        });
    }

    private static void ConfigureBrokerOrders(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BrokerOrderRecord>(entity =>
        {
            entity.ToTable("broker_order", "trading");

            entity.HasKey(e => e.Id).HasName("pk_broker_order");

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.OrderIntentId).HasColumnName("order_intent_id").IsRequired();
            entity.Property(e => e.CustomerId).HasColumnName("customer_id").IsRequired();
            entity.Property(e => e.Symbol).HasColumnName("symbol").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Side).HasColumnName("side").IsRequired();
            entity.Property(e => e.Type).HasColumnName("type").IsRequired();
            entity.Property(e => e.Quantity).HasColumnName("quantity").HasPrecision(18, 8).IsRequired();
            entity.Property(e => e.LimitPrice).HasColumnName("limit_price").HasPrecision(18, 4);
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(3);
            entity.Property(e => e.TimeInForce).HasColumnName("time_in_force").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.BrokerOrderId).HasColumnName("broker_order_id").HasMaxLength(128);
            entity.Property(e => e.RejectionReason).HasColumnName("rejection_reason");
            entity.Property(e => e.RejectionDetails).HasColumnName("rejection_details").HasMaxLength(500);
            entity.Property(e => e.SubmittedAtUtc).HasColumnName("submitted_at_utc").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");

            entity.HasIndex(e => e.OrderIntentId).HasDatabaseName("ix_broker_order_order_intent_id");
            entity.HasIndex(e => e.BrokerOrderId).HasDatabaseName("ix_broker_order_broker_order_id").HasFilter("broker_order_id IS NOT NULL");
        });
    }
}
