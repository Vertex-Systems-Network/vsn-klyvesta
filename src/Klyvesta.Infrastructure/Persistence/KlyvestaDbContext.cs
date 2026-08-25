using Klyvesta.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Klyvesta.Infrastructure.Persistence;

public sealed class KlyvestaDbContext(DbContextOptions<KlyvestaDbContext> options) : DbContext(options)
{
    internal DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    internal DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureIdempotency(modelBuilder);
        ConfigureInbox(modelBuilder);
        ConfigureOutbox(modelBuilder);
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
}
