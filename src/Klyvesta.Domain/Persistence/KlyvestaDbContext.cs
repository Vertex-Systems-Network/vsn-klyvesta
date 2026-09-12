using Microsoft.EntityFrameworkCore;
using Klyvesta.Domain.Persistence.Entities;
using Klyvesta.Domain.Common;

namespace Klyvesta.Domain.Persistence;

/// <summary>
/// EF Core DbContext for Klyvesta domain persistence.
/// Configures PostgreSQL mappings with financial precision requirements:
/// - UUID primary keys (UUIDv7 compatible)
/// - Exact numeric/decimal fields for money (no floating point)
/// - UTC timestamps only
/// - Optimistic concurrency via RowVersion
/// - Append-only patterns for ledger entities
/// </summary>
public class KlyvestaDbContext : DbContext
{
    public KlyvestaDbContext(DbContextOptions<KlyvestaDbContext> options)
        : base(options)
    {
    }

    // Ledger aggregates
    public DbSet<LedgerAccountEntity> LedgerAccounts => Set<LedgerAccountEntity>();
    public DbSet<JournalEntity> Journals => Set<JournalEntity>();
    public DbSet<PostingEntity> Postings => Set<PostingEntity>();

    // Order management
    public DbSet<OrderIntentEntity> OrderIntents => Set<OrderIntentEntity>();
    public DbSet<OrderExecutionEntity> OrderExecutions => Set<OrderExecutionEntity>();

    // Risk & Compliance
    public DbSet<RiskPolicyEntity> RiskPolicies => Set<RiskPolicyEntity>();
    public DbSet<RiskDecisionEntity> RiskDecisions => Set<RiskDecisionEntity>();
    public DbSet<CompliancePolicyEntity> CompliancePolicies => Set<CompliancePolicyEntity>();
    public DbSet<MandateEntity> Mandates => Set<MandateEntity>();
    public DbSet<ComplianceDecisionEntity> ComplianceDecisions => Set<ComplianceDecisionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from separate classes
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KlyvestaDbContext).Assembly);

        // Enforce UTC timestamp convention for all entities with IObserved
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IObserved).IsAssignableFrom(entityType.ClrType))
            {
                var createdAtProp = entityType.FindProperty("CreatedAtUtc");
                var updatedAtProp = entityType.FindProperty("UpdatedAtUtc");
                
                if (createdAtProp != null)
                {
                    createdAtProp.SetDefaultValueSql("CURRENT_TIMESTAMP AT TIME ZONE 'UTC'");
                }
                
                if (updatedAtProp != null)
                {
                    updatedAtProp.SetDefaultValueSql("CURRENT_TIMESTAMP AT TIME ZONE 'UTC'");
                }
            }
        }
    }

    /// <summary>
    /// Override SaveChanges to automatically update UpdatedAtUtc timestamps
    /// and enforce optimistic concurrency.
    /// </summary>
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        UpdateTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is IObserved && 
                       (e.State == EntityState.Modified || e.State == EntityState.Added));

        foreach (var entry in entries)
        {
            if (entry.Entity is IObserved observed)
            {
                observed.UpdatedAtUtc = DateTime.UtcNow;
                
                if (entry.State == EntityState.Added && observed.CreatedAtUtc == default)
                {
                    observed.CreatedAtUtc = DateTime.UtcNow;
                }
            }
        }
    }
}
