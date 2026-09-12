using Klyvesta.Domain.Ledger;
using Klyvesta.Domain.Persistence;
using Klyvesta.Domain.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Klyvesta.Domain.Services;

/// <summary>
/// Service interface for managing the double-entry ledger.
/// Ensures atomic commits and enforces accounting invariants.
/// </summary>
public interface ILedgerService
{
    /// <summary>
    /// Commits a new journal entry atomically.
    /// Validates that Debits == Credits before persisting.
    /// </summary>
    Task CommitJournalAsync(Journal journal, CancellationToken ct = default);

    /// <summary>
    /// Reverses an existing committed journal.
    /// Creates a new reversing journal with swapped debits/credits.
    /// </summary>
    Task ReverseJournalAsync(JournalId journalId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the current balance for a specific account.
    /// </summary>
    Task<Money> GetAccountBalanceAsync(LedgerAccountId accountId, string currency, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a journal by ID including its postings.
    /// </summary>
    Task<Journal?> GetJournalByIdAsync(JournalId journalId, CancellationToken ct = default);
}

/// <summary>
/// Concrete implementation of ILedgerService using Entity Framework Core.
/// </summary>
public class LedgerService : ILedgerService
{
    private readonly KlyvestaDbContext _dbContext;
    private readonly ILogger<LedgerService> _logger;

    public LedgerService(KlyvestaDbContext dbContext, ILogger<LedgerService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task CommitJournalAsync(Journal journal, CancellationToken ct = default)
    {
        // Invariant Check: Debits must equal Credits
        if (!journal.IsCommitted)
        {
            journal.Commit(); // This validates balance internally
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            // Calculate totals in minor units (cents)
            var totalDebits = journal.Postings.Where(p => p.IsDebit).Sum(p => p.Amount.Amount);
            var totalCredits = journal.Postings.Where(p => !p.IsDebit).Sum(p => p.Amount.Amount);
            var currency = journal.Postings.First().Amount.Currency;

            // Map Domain Journal to EF Entity
            var journalEntity = new JournalEntity
            {
                Id = Guid.NewGuid(),
                JournalId = journal.Id.ToString(),
                Description = journal.Description,
                IdempotencyKey = journal.IdempotencyKey,
                ExternalReference = journal.SourceType,
                TotalDebitsMinorUnits = (long)(totalDebits * 100), // Convert to cents
                TotalCreditsMinorUnits = (long)(totalCredits * 100),
                Currency = currency,
                State = JournalState.Committed,
                CommittedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                Postings = journal.Postings.Select(p => new PostingEntity
                {
                    Id = Guid.NewGuid(),
                    PostingId = Guid.NewGuid().ToString(),
                    JournalId = Guid.Empty, // Will be set by EF relationship
                    LedgerAccountId = Guid.Empty, // TODO: Map from AccountId
                    EntryType = p.IsDebit ? EntryType.Debit : EntryType.Credit,
                    AmountMinorUnits = (long)(p.Amount.Amount * 100), // Convert to cents
                    Currency = p.Amount.Currency,
                    Description = p.Description,
                    CreatedAtUtc = DateTime.UtcNow
                }).ToList()
            };

            _dbContext.Journals.Add(journalEntity);
            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Committed journal {JournalId} with {PostingCount} postings", journal.Id, journal.Postings.Count);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to commit journal {JournalId}", journal.Id);
            throw;
        }
    }

    public async Task ReverseJournalAsync(JournalId journalId, string reason, CancellationToken ct = default)
    {
        var originalJournal = await GetJournalByIdAsync(journalId, ct)
            ?? throw new NotFoundException($"Journal {journalId} not found");

        if (!originalJournal.IsCommitted)
        {
            throw new InvalidOperationException($"Cannot reverse journal {journalId} that is not committed.");
        }

        var reversalPostings = originalJournal.Postings.Select(p => 
            new Posting
            {
                AccountId = p.AccountId,
                Amount = p.Amount, // Same magnitude
                IsDebit = !p.IsDebit, // Swap side
                Description = $"Reversal: {p.Description}",
                SubLedgerReferenceId = p.SubLedgerReferenceId,
                SubLedgerType = p.SubLedgerType
            }
        ).ToList();

        var reversalJournal = new Journal
        {
            Id = JournalId.New(),
            Description = $"Reversal of {journalId}: {reason}",
            EffectiveDateUtc = DateTime.UtcNow,
            SourceType = "Reversal",
            SourceId = Guid.NewGuid(),
            IdempotencyKey = $"REV-{journalId}-{Guid.NewGuid()}",
            Postings = reversalPostings
        };

        reversalJournal.Commit();
        await CommitJournalAsync(reversalJournal, ct);
    }

    public async Task<Money> GetAccountBalanceAsync(LedgerAccountId accountId, string currency, CancellationToken ct = default)
    {
        var accountEntity = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(a => a.LedgerAccountId == accountId.ToString() && a.Currency == currency, ct)
            ?? throw new NotFoundException($"Account {accountId} not found");

        // Calculate balance from postings
        var balance = await _dbContext.Postings
            .Where(p => p.LedgerAccountId == accountEntity.Id && p.Currency == currency)
            .SumAsync(p => p.EntryType == EntryType.Debit ? p.AmountMinorUnits : -p.AmountMinorUnits, ct);

        return new Money(balance / 100m, currency); // Convert from cents
    }

    public async Task<Journal?> GetJournalByIdAsync(JournalId journalId, CancellationToken ct = default)
    {
        var entity = await _dbContext.Journals
            .Include(j => j.Postings)
            .ThenInclude(p => p.LedgerAccount)
            .FirstOrDefaultAsync(j => j.JournalId == journalId.ToString(), ct);

        if (entity == null) return null;

        // Note: This is a simplified reconstruction. Full implementation would need proper AccountId mapping.
        var postings = entity.Postings.Select(p => new Posting
        {
            AccountId = new LedgerAccountId(Guid.Parse(p.LedgerAccountId)), // Simplified mapping
            Amount = new Money(p.AmountMinorUnits / 100m, p.Currency),
            IsDebit = p.EntryType == EntryType.Debit,
            Description = p.Description
        }).ToList();

        var journal = new Journal
        {
            Id = journalId,
            Description = entity.Description,
            EffectiveDateUtc = entity.CommittedAtUtc ?? entity.CreatedAtUtc,
            SourceType = entity.ExternalReference ?? "Unknown",
            SourceId = entity.Id,
            IdempotencyKey = entity.IdempotencyKey,
            Postings = postings
        };

        // Manually mark as committed since domain object doesn't have setter
        if (entity.State == JournalState.Committed)
        {
            journal.Commit();
        }

        return journal;
    }
}
