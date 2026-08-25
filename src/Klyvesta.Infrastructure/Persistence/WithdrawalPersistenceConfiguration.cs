using Klyvesta.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;

namespace Klyvesta.Infrastructure.Persistence;

internal static class WithdrawalPersistenceConfiguration
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureBeneficiaryVersion(modelBuilder);
        ConfigureWithdrawalRequest(modelBuilder);
        ConfigureWithdrawalAuthorization(modelBuilder);
        ConfigureRelationships(modelBuilder);
    }

    private static void ConfigureBeneficiaryVersion(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WithdrawalBeneficiaryVersionRecord>(entity =>
        {
            entity.ToTable("withdrawal_beneficiary_version", "funding", table =>
            {
                table.HasCheckConstraint(
                    "ck_withdrawal_beneficiary_identifiers",
                    "version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                    "beneficiary_id <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                    "customer_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                table.HasCheckConstraint(
                    "ck_withdrawal_beneficiary_version_number",
                    "version_number > 0");
                table.HasCheckConstraint(
                    "ck_withdrawal_beneficiary_destination_hash",
                    "destination_hash ~ '^[0-9A-F]{64}$'");
                table.HasCheckConstraint(
                    "ck_withdrawal_beneficiary_state",
                    "state IN ('pending_verification', 'verified_cooling_off', 'active', 'blocked', 'revoked')");
                table.HasCheckConstraint(
                    "ck_withdrawal_beneficiary_verification_evidence",
                    "(verification_evidence_reference IS NULL AND verified_at IS NULL AND available_after IS NULL) OR " +
                    "(verification_evidence_reference IS NOT NULL AND btrim(verification_evidence_reference) <> '' AND " +
                    "verified_at IS NOT NULL AND available_after IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_withdrawal_beneficiary_verification_chronology",
                    "verified_at IS NULL OR (verified_at >= created_at AND available_after >= verified_at)");
                table.HasCheckConstraint(
                    "ck_withdrawal_beneficiary_block_pair",
                    "(blocked_at IS NULL AND block_reason IS NULL) OR " +
                    "(blocked_at IS NOT NULL AND block_reason IS NOT NULL AND btrim(block_reason) <> '')");
                table.HasCheckConstraint(
                    "ck_withdrawal_beneficiary_revocation_pair",
                    "(revoked_at IS NULL AND revocation_reason IS NULL) OR " +
                    "(revoked_at IS NOT NULL AND revocation_reason IS NOT NULL AND btrim(revocation_reason) <> '')");
                table.HasCheckConstraint(
                    "ck_withdrawal_beneficiary_transition_chronology",
                    "(blocked_at IS NULL OR blocked_at >= created_at) AND " +
                    "(revoked_at IS NULL OR revoked_at >= created_at) AND " +
                    "(blocked_at IS NULL OR revoked_at IS NULL OR revoked_at >= blocked_at)");
                table.HasCheckConstraint(
                    "ck_withdrawal_beneficiary_state_evidence",
                    "(state = 'pending_verification' AND verification_evidence_reference IS NULL AND blocked_at IS NULL AND revoked_at IS NULL) OR " +
                    "(state IN ('verified_cooling_off', 'active') AND verification_evidence_reference IS NOT NULL AND blocked_at IS NULL AND revoked_at IS NULL) OR " +
                    "(state = 'blocked' AND blocked_at IS NOT NULL AND revoked_at IS NULL) OR " +
                    "(state = 'revoked' AND revoked_at IS NOT NULL)");
            });

            entity.HasKey(item => item.VersionId).HasName("pk_withdrawal_beneficiary_version");
            entity.HasAlternateKey(item => new { item.VersionId, item.CustomerId, item.DestinationHash })
                .HasName("ak_withdrawal_beneficiary_binding");

            entity.Property(item => item.VersionId).HasColumnName("version_id").ValueGeneratedNever();
            entity.Property(item => item.BeneficiaryId).HasColumnName("beneficiary_id").IsRequired();
            entity.Property(item => item.VersionNumber).HasColumnName("version_number").IsRequired();
            entity.Property(item => item.CustomerId).HasColumnName("customer_id").IsRequired();
            entity.Property(item => item.DestinationHash).HasColumnName("destination_hash").HasColumnType("character(64)").IsRequired();
            entity.Property(item => item.State).HasColumnName("state").HasMaxLength(32).IsRequired();
            entity.Property(item => item.VerificationEvidenceReference).HasColumnName("verification_evidence_reference").HasMaxLength(256);
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(item => item.VerifiedAt).HasColumnName("verified_at").HasColumnType("timestamp with time zone");
            entity.Property(item => item.AvailableAfter).HasColumnName("available_after").HasColumnType("timestamp with time zone");
            entity.Property(item => item.BlockedAt).HasColumnName("blocked_at").HasColumnType("timestamp with time zone");
            entity.Property(item => item.BlockReason).HasColumnName("block_reason").HasMaxLength(128);
            entity.Property(item => item.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamp with time zone");
            entity.Property(item => item.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(128);

            entity.HasIndex(item => new { item.BeneficiaryId, item.VersionNumber })
                .IsUnique()
                .HasDatabaseName("ux_withdrawal_beneficiary_version");
            entity.HasIndex(item => new { item.CustomerId, item.State })
                .HasDatabaseName("ix_withdrawal_beneficiary_customer_state");
        });
    }

    private static void ConfigureWithdrawalRequest(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WithdrawalRequestRecord>(entity =>
        {
            entity.ToTable("withdrawal", "funding", table =>
            {
                table.HasCheckConstraint(
                    "ck_withdrawal_identifiers",
                    "id <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                    "customer_id <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                    "beneficiary_version_id <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                    "requested_by_principal_id <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                    "(approved_by_principal_id IS NULL OR approved_by_principal_id <> '00000000-0000-0000-0000-000000000000'::uuid) AND " +
                    "(authorization_id IS NULL OR authorization_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
                table.HasCheckConstraint("ck_withdrawal_amount", "amount > 0");
                table.HasCheckConstraint("ck_withdrawal_currency", "currency ~ '^[A-Z]{3}$'");
                table.HasCheckConstraint(
                    "ck_withdrawal_hashes",
                    "destination_hash ~ '^[0-9A-F]{64}$' AND transaction_data_hash ~ '^[0-9A-F]{64}$'");
                table.HasCheckConstraint(
                    "ck_withdrawal_state",
                    "state IN ('requested', 'security_check', 'security_hold', 'policy_check', 'rejected', " +
                    "'approval_pending', 'approved', 'submission_pending', 'submitted', 'processing', 'completed', 'failed', 'unknown')");
                table.HasCheckConstraint("ck_withdrawal_chronology", "updated_at >= created_at");
                table.HasCheckConstraint(
                    "ck_withdrawal_approval_pair",
                    "(approved_by_principal_id IS NULL AND approved_at IS NULL) OR " +
                    "(approved_by_principal_id IS NOT NULL AND approved_at IS NOT NULL AND approved_at >= created_at AND " +
                    "approved_by_principal_id <> requested_by_principal_id)");
                table.HasCheckConstraint(
                    "ck_withdrawal_reason_content",
                    "reason_code IS NULL OR btrim(reason_code) <> ''");
                table.HasCheckConstraint(
                    "ck_withdrawal_reason_required",
                    "state NOT IN ('security_hold', 'rejected', 'failed', 'unknown') OR reason_code IS NOT NULL");
                table.HasCheckConstraint(
                    "ck_withdrawal_authorization_state",
                    "(state IN ('requested', 'security_check', 'security_hold', 'policy_check', 'rejected', 'approval_pending', 'approved') AND authorization_id IS NULL) OR " +
                    "(state IN ('submission_pending', 'submitted', 'processing', 'completed', 'failed', 'unknown') AND authorization_id IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_withdrawal_external_reference",
                    "(external_reference IS NULL OR btrim(external_reference) <> '') AND " +
                    "(state NOT IN ('submitted', 'processing') OR external_reference IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_withdrawal_outcome_evidence",
                    "(state IN ('completed', 'failed') AND outcome_evidence_reference IS NOT NULL AND btrim(outcome_evidence_reference) <> '') OR " +
                    "(state NOT IN ('completed', 'failed') AND outcome_evidence_reference IS NULL)");
            });

            entity.HasKey(item => item.Id).HasName("pk_withdrawal");
            entity.HasAlternateKey(item => new { item.Id, item.RequestedByPrincipalId, item.TransactionDataHash })
                .HasName("ak_withdrawal_request_authorization_binding");

            entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(item => item.CustomerId).HasColumnName("customer_id").IsRequired();
            entity.Property(item => item.BeneficiaryVersionId).HasColumnName("beneficiary_version_id").IsRequired();
            entity.Property(item => item.Amount).HasColumnName("amount").HasColumnType("numeric(24,8)").IsRequired();
            entity.Property(item => item.Currency).HasColumnName("currency").HasColumnType("character(3)").IsRequired();
            entity.Property(item => item.DestinationHash).HasColumnName("destination_hash").HasColumnType("character(64)").IsRequired();
            entity.Property(item => item.TransactionDataHash).HasColumnName("transaction_data_hash").HasColumnType("character(64)").IsRequired();
            entity.Property(item => item.State).HasColumnName("state").HasMaxLength(32).IsRequired();
            entity.Property(item => item.RequestedByPrincipalId).HasColumnName("requested_by_principal_id").IsRequired();
            entity.Property(item => item.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(item => item.ReasonCode).HasColumnName("reason_code").HasMaxLength(128);
            entity.Property(item => item.ApprovedByPrincipalId).HasColumnName("approved_by_principal_id");
            entity.Property(item => item.ApprovedAt).HasColumnName("approved_at").HasColumnType("timestamp with time zone");
            entity.Property(item => item.AuthorizationId).HasColumnName("authorization_id");
            entity.Property(item => item.ExternalReference).HasColumnName("external_reference").HasMaxLength(256);
            entity.Property(item => item.OutcomeEvidenceReference).HasColumnName("outcome_evidence_reference").HasMaxLength(256);

            entity.HasIndex(item => new { item.CustomerId, item.State, item.CreatedAt })
                .HasDatabaseName("ix_withdrawal_customer_state_created_at");
            entity.HasIndex(item => new { item.BeneficiaryVersionId, item.CustomerId, item.DestinationHash })
                .HasDatabaseName("ix_withdrawal_beneficiary_binding");
            entity.HasIndex(item => new { item.AuthorizationId, item.Id })
                .HasFilter("authorization_id IS NOT NULL")
                .HasDatabaseName("ix_withdrawal_selected_authorization");
        });
    }

    private static void ConfigureWithdrawalAuthorization(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WithdrawalAuthorizationRecord>(entity =>
        {
            entity.ToTable("withdrawal_authorization", "funding", table =>
            {
                table.HasCheckConstraint(
                    "ck_withdrawal_authorization_identifiers",
                    "id <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                    "withdrawal_id <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                    "principal_id <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                    "session_id <> '00000000-0000-0000-0000-000000000000'::uuid");
                table.HasCheckConstraint(
                    "ck_withdrawal_authorization_hash",
                    "transaction_data_hash ~ '^[0-9A-F]{64}$'");
                table.HasCheckConstraint(
                    "ck_withdrawal_authorization_expiry",
                    "expires_at > authorized_at");
            });

            entity.HasKey(item => item.Id).HasName("pk_withdrawal_authorization");
            entity.HasAlternateKey(item => new { item.Id, item.WithdrawalId })
                .HasName("ak_withdrawal_authorization_withdrawal");

            entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(item => item.WithdrawalId).HasColumnName("withdrawal_id").IsRequired();
            entity.Property(item => item.PrincipalId).HasColumnName("principal_id").IsRequired();
            entity.Property(item => item.SessionId).HasColumnName("session_id").IsRequired();
            entity.Property(item => item.TransactionDataHash).HasColumnName("transaction_data_hash").HasColumnType("character(64)").IsRequired();
            entity.Property(item => item.AuthorizedAt).HasColumnName("authorized_at").HasColumnType("timestamp with time zone").IsRequired();
            entity.Property(item => item.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone").IsRequired();

            entity.HasIndex(item => new { item.WithdrawalId, item.ExpiresAt })
                .HasDatabaseName("ix_withdrawal_authorization_withdrawal_expiry");
            entity.HasIndex(item => new { item.WithdrawalId, item.PrincipalId, item.TransactionDataHash })
                .HasDatabaseName("ix_withdrawal_authorization_binding");
            entity.HasIndex(item => new { item.SessionId, item.PrincipalId })
                .HasDatabaseName("ix_withdrawal_authorization_session_principal");
        });
    }

    private static void ConfigureRelationships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SecuritySessionRecord>()
            .HasAlternateKey(item => new { item.Id, item.PrincipalId })
            .HasName("ak_security_session_principal");

        modelBuilder.Entity<WithdrawalRequestRecord>()
            .HasOne<WithdrawalBeneficiaryVersionRecord>()
            .WithMany()
            .HasForeignKey(item => new { item.BeneficiaryVersionId, item.CustomerId, item.DestinationHash })
            .HasPrincipalKey(item => new { item.VersionId, item.CustomerId, item.DestinationHash })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_withdrawal_beneficiary_binding");

        modelBuilder.Entity<WithdrawalAuthorizationRecord>()
            .HasOne<WithdrawalRequestRecord>()
            .WithMany()
            .HasForeignKey(item => new { item.WithdrawalId, item.PrincipalId, item.TransactionDataHash })
            .HasPrincipalKey(item => new { item.Id, item.RequestedByPrincipalId, item.TransactionDataHash })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_withdrawal_authorization_request_binding");

        modelBuilder.Entity<WithdrawalAuthorizationRecord>()
            .HasOne<SecuritySessionRecord>()
            .WithMany()
            .HasForeignKey(item => new { item.SessionId, item.PrincipalId })
            .HasPrincipalKey(item => new { item.Id, item.PrincipalId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_withdrawal_authorization_session_principal");

        modelBuilder.Entity<WithdrawalRequestRecord>()
            .HasOne<WithdrawalAuthorizationRecord>()
            .WithMany()
            .HasForeignKey(item => new { item.AuthorizationId, item.Id })
            .HasPrincipalKey(item => new { item.Id, item.WithdrawalId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_withdrawal_selected_authorization");
    }
}
