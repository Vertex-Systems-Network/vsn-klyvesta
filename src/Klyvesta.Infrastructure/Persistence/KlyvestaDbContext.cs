using Klyvesta.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Klyvesta.Infrastructure.Persistence;

public sealed class KlyvestaDbContext(DbContextOptions<KlyvestaDbContext> options) : DbContext(options)
{
    internal DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    internal DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    internal DbSet<SecurityDeviceRecord> SecurityDevices => Set<SecurityDeviceRecord>();

    internal DbSet<SecuritySessionRecord> SecuritySessions => Set<SecuritySessionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureIdempotency(modelBuilder);
        ConfigureInbox(modelBuilder);
        ConfigureOutbox(modelBuilder);
        ConfigureSecurityDevice(modelBuilder);
        ConfigureSecuritySession(modelBuilder);
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

    private static void ConfigureSecurityDevice(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SecurityDeviceRecord>(entity =>
        {
            entity.ToTable("security_device", "identity", table =>
            {
                table.HasCheckConstraint(
                    "ck_security_device_principal_type",
                    "principal_type IN ('customer', 'staff', 'service')");
                table.HasCheckConstraint(
                    "ck_security_device_trust_state",
                    "trust_state IN ('untrusted', 'trusted', 'restricted', 'revoked')");
                table.HasCheckConstraint(
                    "ck_security_device_integrity_state",
                    "integrity_state IN ('unknown', 'meets_baseline', 'degraded', 'failed')");
                table.HasCheckConstraint(
                    "ck_security_device_last_seen_chronology",
                    "last_seen_at >= registered_at");
                table.HasCheckConstraint(
                    "ck_security_device_restriction_pair",
                    "(restricted_at IS NULL AND restriction_reason IS NULL) OR " +
                    "(restricted_at IS NOT NULL AND restriction_reason IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_security_device_revocation_pair",
                    "(revoked_at IS NULL AND revocation_reason IS NULL) OR " +
                    "(revoked_at IS NOT NULL AND revocation_reason IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_security_device_state_evidence",
                    "(trust_state IN ('untrusted', 'trusted') AND restricted_at IS NULL AND revoked_at IS NULL) OR " +
                    "(trust_state = 'restricted' AND restricted_at IS NOT NULL AND revoked_at IS NULL) OR " +
                    "(trust_state = 'revoked' AND revoked_at IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_security_device_restriction_chronology",
                    "restricted_at IS NULL OR restricted_at >= registered_at");
                table.HasCheckConstraint(
                    "ck_security_device_revocation_chronology",
                    "revoked_at IS NULL OR revoked_at >= registered_at");
                table.HasCheckConstraint(
                    "ck_security_device_transition_chronology",
                    "restricted_at IS NULL OR revoked_at IS NULL OR revoked_at >= restricted_at");
            });

            entity.HasKey(item => item.Id).HasName("pk_security_device");
            entity.HasAlternateKey(item => new { item.Id, item.PrincipalId, item.PrincipalType })
                .HasName("ak_security_device_identity");

            entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(item => item.PrincipalId).HasColumnName("principal_id").IsRequired();
            entity.Property(item => item.PrincipalType).HasColumnName("principal_type").HasMaxLength(32).IsRequired();
            entity.Property(item => item.TrustState).HasColumnName("trust_state").HasMaxLength(32).IsRequired();
            entity.Property(item => item.IntegrityState).HasColumnName("integrity_state").HasMaxLength(32).IsRequired();
            entity.Property(item => item.RegisteredAt).HasColumnName("registered_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(item => item.LastSeenAt).HasColumnName("last_seen_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(item => item.RestrictedAt).HasColumnName("restricted_at").HasColumnType("timestamp with time zone");
            entity.Property(item => item.RestrictionReason).HasColumnName("restriction_reason").HasMaxLength(128);
            entity.Property(item => item.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamp with time zone");
            entity.Property(item => item.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(128);

            entity.HasIndex(item => new { item.PrincipalId, item.TrustState })
                .HasDatabaseName("ix_security_device_principal_state");
            entity.HasIndex(item => item.LastSeenAt)
                .HasDatabaseName("ix_security_device_last_seen_at");
        });
    }

    private static void ConfigureSecuritySession(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SecuritySessionRecord>(entity =>
        {
            entity.ToTable("security_session", "identity", table =>
            {
                table.HasCheckConstraint(
                    "ck_security_session_principal_type",
                    "principal_type IN ('customer', 'staff', 'service')");
                table.HasCheckConstraint(
                    "ck_security_session_auth_strength",
                    "authentication_strength IN ('password', 'strong_mfa', 'phishing_resistant')");
                table.HasCheckConstraint(
                    "ck_security_session_idle_timeout",
                    "idle_timeout_seconds > 0");
                table.HasCheckConstraint(
                    "ck_security_session_auth_chronology",
                    "authenticated_at <= created_at");
                table.HasCheckConstraint(
                    "ck_security_session_last_seen_chronology",
                    "last_seen_at >= created_at AND last_seen_at < absolute_expires_at");
                table.HasCheckConstraint(
                    "ck_security_session_absolute_expiry",
                    "absolute_expires_at > created_at");
                table.HasCheckConstraint(
                    "ck_security_session_restriction_pair",
                    "(restricted_at IS NULL AND restriction_reason IS NULL) OR " +
                    "(restricted_at IS NOT NULL AND restriction_reason IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_security_session_revocation_pair",
                    "(revoked_at IS NULL AND revocation_reason IS NULL) OR " +
                    "(revoked_at IS NOT NULL AND revocation_reason IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_security_session_state_evidence",
                    "(revoked_at IS NULL AND restricted = FALSE AND restricted_at IS NULL) OR " +
                    "(revoked_at IS NULL AND restricted = TRUE AND restricted_at IS NOT NULL) OR " +
                    "(revoked_at IS NOT NULL AND restricted = FALSE)");
                table.HasCheckConstraint(
                    "ck_security_session_restriction_chronology",
                    "restricted_at IS NULL OR restricted_at >= created_at");
                table.HasCheckConstraint(
                    "ck_security_session_revocation_chronology",
                    "revoked_at IS NULL OR revoked_at >= created_at");
                table.HasCheckConstraint(
                    "ck_security_session_transition_chronology",
                    "restricted_at IS NULL OR revoked_at IS NULL OR revoked_at >= restricted_at");
            });

            entity.HasKey(item => item.Id).HasName("pk_security_session");

            entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(item => item.PrincipalId).HasColumnName("principal_id").IsRequired();
            entity.Property(item => item.PrincipalType).HasColumnName("principal_type").HasMaxLength(32).IsRequired();
            entity.Property(item => item.DeviceId).HasColumnName("device_id").IsRequired();
            entity.Property(item => item.AuthenticatedAt).HasColumnName("authenticated_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(item => item.LastSeenAt).HasColumnName("last_seen_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(item => item.IdleTimeoutSeconds).HasColumnName("idle_timeout_seconds").IsRequired();
            entity.Property(item => item.AbsoluteExpiresAt).HasColumnName("absolute_expires_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(item => item.AuthenticationStrength).HasColumnName("authentication_strength").HasMaxLength(32).IsRequired();
            entity.Property(item => item.Restricted).HasColumnName("restricted").IsRequired();
            entity.Property(item => item.RestrictedAt).HasColumnName("restricted_at").HasColumnType("timestamp with time zone");
            entity.Property(item => item.RestrictionReason).HasColumnName("restriction_reason").HasMaxLength(128);
            entity.Property(item => item.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamp with time zone");
            entity.Property(item => item.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(128);

            entity.HasIndex(item => new { item.PrincipalId, item.RevokedAt, item.AbsoluteExpiresAt })
                .HasDatabaseName("ix_security_session_principal_lifecycle");
            entity.HasIndex(item => new { item.DeviceId, item.RevokedAt })
                .HasDatabaseName("ix_security_session_device_lifecycle");
            entity.HasIndex(item => item.AbsoluteExpiresAt)
                .HasDatabaseName("ix_security_session_absolute_expires_at");

            entity.HasOne<SecurityDeviceRecord>()
                .WithMany()
                .HasForeignKey(item => new { item.DeviceId, item.PrincipalId, item.PrincipalType })
                .HasPrincipalKey(item => new { item.Id, item.PrincipalId, item.PrincipalType })
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_security_session_device_identity");
        });
    }
}
