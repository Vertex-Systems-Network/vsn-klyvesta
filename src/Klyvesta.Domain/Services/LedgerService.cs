using Klyvesta.Domain.Common;
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
    /// Gets a journal by its ID.
    /// </summary>
    Task<Journal?> GetJournalByIdAsync(JournalId journalId, CancellationToken ct = default);
}

/// <summary>
/// EF Core implementation of ILedgerService.
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
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            // Validate journal balance
            if (journal.TotalDebits.Amount != journal.TotalCredits.Amount)
            {
                throw new InvalidOperationException(
                    $"Journal imbalance: Debits={journal.TotalDebits}, Credits={journal.TotalCredits}");
            }

            // Create entity
            var entity = new JournalEntity
            {
                Id = Guid.NewGuid(),
                JournalId = journal.Id.Value.ToString("D"),
                ExternalReference = journal.ExternalReference,
                IdempotencyKey = journal.IdempotencyKey,
                Description = journal.Description,
                TotalDebitsMinorUnits = (long)journal.TotalDebits.Amount.MinorUnits,
                TotalCreditsMinorUnits = (long)journal.TotalCredits.Amount.MinorUnits,
                Currency = journal.Currency,
                State = JournalState.Committed,
                CommittedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                ExternalTimestampUtc = journal.CreatedAtUtc,
                ObservedAtUtc = DateTime.UtcNow
            };

            _dbContext.Journals.Add(entity);

            // Add postings
            foreach (var posting in journal.Postings)
            {
                var postingEntity = new PostingEntity
                {
                    Id = Guid.NewGuid(),
                    PostingId = posting.Id.Value.ToString("D"),
                    JournalId = entity.Id,
                    LedgerAccountId = posting.LedgerAccountId.Value,
                    EntryType = posting.EntryType == EntryType.Debit ? EntryType.Debit : EntryType.Credit,
                    AmountMinorUnits = (long)posting.Amount.MinorUnits,
                    Currency = posting.Currency,
                    Description = posting.Description,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    ExternalTimestampUtc = posting.CreatedAtUtc,
                    ObservedAtUtc = DateTime.UtcNow
                };
                _dbContext.Postings.Add(postingEntity);
            }

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Committed journal {JournalId} with {PostingCount} postings", 
                journal.Id, journal.Postings.Count);
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
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var originalEntity = await _dbContext.Journals
                .FirstOrDefaultAsync(j => j.JournalId == journalId.Value.ToString("D"), ct);

            if (originalEntity == null)
                throw new KeyNotFoundException($"Journal {journalId} not found");

            if (originalEntity.State != JournalState.Committed)
                throw new InvalidOperationException($"Cannot reverse journal in state {originalEntity.State}");

            // Create reversal journal
            var reversalEntity = new JournalEntity
            {
                Id = Guid.NewGuid(),
                JournalId = Guid.NewGuid().ToString("D"),
                ExternalReference = $"Reversal of {originalEntity.JournalId}: {reason}",
                IdempotencyKey = $"REV-{originalEntity.IdempotencyKey}",
                Description = $"Reversal: {originalEntity.Description}",
                TotalDebitsMinorUnits = originalEntity.TotalCreditsMinorUnits,
                TotalCreditsMinorUnits = originalEntity.TotalDebitsMinorUnits,
                Currency = originalEntity.Currency,
                State = JournalState.Committed,
                CommittedAtUtc = DateTime.UtcNow,
                ReversesJournalId = originalEntity.Id,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                ExternalTimestampUtc = DateTime.UtcNow,
                ObservedAtUtc = DateTime.UtcNow
            };

            _dbContext.Journals.Add(reversalEntity);

            // Create reversed postings
            foreach (var originalPosting in _dbContext.Postings.Where(p => p.JournalId == originalEntity.Id))
            {
                var reversedPosting = new PostingEntity
                {
                    Id = Guid.NewGuid(),
                    PostingId = Guid.NewGuid().ToString("D"),
                    JournalId = reversalEntity.Id,
                    LedgerAccountId = originalPosting.LedgerAccountId,
                    EntryType = originalPosting.EntryType == EntryType.Debit ? EntryType.Credit : EntryType.Debit,
                    AmountMinorUnits = originalPosting.AmountMinorUnits,
                    Currency = originalPosting.Currency,
                    Description = $"Reversal of {originalPosting.PostingId}",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    ExternalTimestampUtc = DateTime.UtcNow,
                    ObservedAtUtc = DateTime.UtcNow
                };
                _dbContext.Postings.Add(reversedPosting);
            }

            // Mark original as reversed
            originalEntity.State = JournalState.Reversed;
            originalEntity.ReversalJournalId = reversalEntity.Id;
            originalEntity.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Reversed journal {JournalId} with reason: {Reason}", journalId, reason);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to reverse journal {JournalId}", journalId);
            throw;
        }
    }

    public async Task<Money> GetAccountBalanceAsync(LedgerAccountId accountId, string currency, CancellationToken ct = default)
    {
        var accountEntity = await _dbContext.LedgerAccounts
            .FirstOrDefaultAsync(a => a.LedgerAccountId == accountId.Value.ToString("D") && a.Currency == currency, ct);

        if (accountEntity == null)
            return new Money(0, currency);

        // Calculate balance from postings
        var debitTotal = await _dbContext.Postings
            .Where(p => p.LedgerAccountId == accountEntity.Id && p.EntryType == EntryType.Debit)
            .SumAsync(p => p.AmountMinorUnits, ct);

        var creditTotal = await _dbContext.Postings
            .Where(p => p.LedgerAccountId == accountEntity.Id && p.EntryType == EntryType.Credit)
            .SumAsync(p => p.AmountMinorUnits, ct);

        var netMinorUnits = accountEntity.Type switch
        {
            AccountType.Asset or AccountType.Expense => debitTotal - creditTotal,
            AccountType.Liability or AccountType.Equity or AccountType.Revenue => creditTotal - debitTotal,
            _ => debitTotal - creditTotal
        };

        return new Money((decimal)netMinorUnits, currency);
    }

    public async Task<Journal?> GetJournalByIdAsync(JournalId journalId, CancellationToken ct = default)
    {
        var entity = await _dbContext.Journals
            .Include(j => j.Postings)
            .FirstOrDefaultAsync(j => j.JournalId == journalId.Value.ToString("D"), ct);

        if (entity == null)
            return null;

        // Map back to domain model
        return new Journal(
            new JournalId(Guid.Parse(entity.JournalId)),
            entity.Description,
            entity.Currency,
            entity.ExternalReference,
            entity.IdempotencyKey,
            entity.CreatedAtUtc
        );
    }
}
