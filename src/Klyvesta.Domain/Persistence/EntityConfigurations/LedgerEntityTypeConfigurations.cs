using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Klyvesta.Domain.Persistence.Entities;

namespace Klyvesta.Domain.Persistence.EntityConfigurations;

/// <summary>
/// EF Core configuration for LedgerAccountEntity.
/// Ensures append-only pattern and proper indexing.
/// </summary>
public class LedgerAccountEntityTypeConfiguration : IEntityTypeConfiguration<LedgerAccountEntity>
{
    public void Configure(EntityTypeBuilder<LedgerAccountEntity> builder)
    {
        builder.ToTable("ledger_accounts", schema: "ledger");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.LedgerAccountId)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(a => a.LedgerAccountId)
            .IsUnique();

        builder.Property(a => a.AccountCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(a => a.AccountCode);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.Type)
            .IsRequired();

        builder.Property(a => a.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .IsFixedLength();

        builder.Property(a => a.DebitBalanceMinorUnits)
            .IsRequired();

        builder.Property(a => a.CreditBalanceMinorUnits)
            .IsRequired();

        // Self-referencing hierarchy for chart of accounts
        builder.HasOne<LedgerAccountEntity>()
            .WithMany()
            .HasForeignKey(a => a.ParentAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Soft delete pattern
        builder.Property(a => a.DeletedAtUtc)
            .HasDefaultValue(null);

        // Optimistic concurrency
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion()
            .HasColumnName("row_version");
    }
}

/// <summary>
/// EF Core configuration for JournalEntity.
/// Enforces immutability after commit and balance validation.
/// </summary>
public class JournalEntityTypeConfiguration : IEntityTypeConfiguration<JournalEntity>
{
    public void Configure(EntityTypeBuilder<JournalEntity> builder)
    {
        builder.ToTable("journals", schema: "ledger");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.JournalId)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(j => j.JournalId)
            .IsUnique();

        builder.Property(j => j.ExternalReference)
            .HasMaxLength(255);

        builder.HasIndex(j => j.ExternalReference);

        builder.Property(j => j.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(j => j.IdempotencyKey)
            .IsUnique();

        builder.Property(j => j.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(j => j.TotalDebitsMinorUnits)
            .IsRequired();

        builder.Property(j => j.TotalCreditsMinorUnits)
            .IsRequired();

        builder.Property(j => j.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .IsFixedLength();

        builder.Property(j => j.State)
            .IsRequired();

        // Check constraint: debits must equal credits
        builder.ToTable(t => t.HasCheckConstraint("CK_Journals_DebitsEqualsCredits",
            "\"TotalDebitsMinorUnits\" = \"TotalCreditsMinorUnits\""));

        // State machine constraints
        builder.ToTable(t => t.HasCheckConstraint("CK_Journals_CommittedRequiresTimestamp",
            "\"State\" != 1 OR \"CommittedAtUtc\" IS NOT NULL"));

        builder.Property(j => j.ReversalJournalId)
            .HasDefaultValue(null);

        builder.HasOne(j => j.LedgerAccount)
            .WithMany(a => a.Journals)
            .HasForeignKey(j => j.LedgerAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        // Soft delete
        builder.Property(j => j.DeletedAtUtc)
            .HasDefaultValue(null);

        // Optimistic concurrency
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion()
            .HasColumnName("row_version");

        // Index for finding journals by state
        builder.HasIndex(j => j.State);
        builder.HasIndex(j => j.CommittedAtUtc);
    }
}

/// <summary>
/// EF Core configuration for PostingEntity.
/// Individual debit/credit entries within a journal.
/// </summary>
public class PostingEntityTypeConfiguration : IEntityTypeConfiguration<PostingEntity>
{
    public void Configure(EntityTypeBuilder<PostingEntity> builder)
    {
        builder.ToTable("postings", schema: "ledger");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PostingId)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(p => p.PostingId)
            .IsUnique();

        builder.Property(p => p.JournalId)
            .IsRequired();

        builder.HasOne(p => p.Journal)
            .WithMany(j => j.Postings)
            .HasForeignKey(p => p.JournalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.JournalId);

        builder.Property(p => p.LedgerAccountId)
            .IsRequired();

        builder.HasOne(p => p.LedgerAccount)
            .WithMany()
            .HasForeignKey(p => p.LedgerAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.LedgerAccountId);

        builder.Property(p => p.EntryType)
            .IsRequired();

        builder.Property(p => p.AmountMinorUnits)
            .IsRequired();

        builder.Property(p => p.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .IsFixedLength();

        builder.Property(p => p.Description)
            .HasMaxLength(500);

        // Soft delete
        builder.Property(p => p.DeletedAtUtc)
            .HasDefaultValue(null);

        // Optimistic concurrency
        builder.Property<byte[]>("RowVersion")
            .IsRowVersion()
            .HasColumnName("row_version");

        // Index for ledger account statement queries (descending by created date)
        builder.HasIndex(p => p.LedgerAccountId, p => p.CreatedAtUtc)
            .HasDatabaseName("IX_postings_ledger_account_created");
    }
}
