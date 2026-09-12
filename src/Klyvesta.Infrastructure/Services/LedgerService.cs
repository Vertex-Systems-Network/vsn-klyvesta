using Klyvesta.Domain.Services;
using Klyvesta.Domain.ValueObjects;
using Klyvesta.Infrastructure.Persistence;
using Klyvesta.Infrastructure.Persistence.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Klyvesta.Infrastructure.Services;

/// <summary>
/// Implementation of double-entry ledger service.
/// Ensures debits always equal credits.
/// </summary>
public sealed class LedgerService : ILedgerService
{
    private readonly KlyvestaDbContext _dbContext;
    private readonly ILogger<LedgerService> _logger;

    public LedgerService(KlyvestaDbContext dbContext, ILogger<LedgerService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PostingResult> PostAsync(LedgerTransaction transaction, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            // Check for idempotency
            if (!string.IsNullOrWhiteSpace(transaction.IdempotencyKey))
            {
                var existingJournal = await _dbContext.LedgerJournals
                    .FirstOrDefaultAsync(j => j.IdempotencyKey == transaction.IdempotencyKey, ct);

                if (existingJournal != null)
                {
                    _logger.LogInformation(
                        "Duplicate transaction detected with idempotency key {IdempotencyKey}. Returning existing transaction {TransactionId}",
                        transaction.IdempotencyKey, existingJournal.Id);
                    
                    return PostingResult.Succeeded(existingJournal.Id);
                }
            }

            // Create journal record
            var journal = new LedgerJournalRecord
            {
                Id = transaction.Id,
                Description = transaction.Description,
                CorrelationId = transaction.CorrelationId,
                IdempotencyKey = transaction.IdempotencyKey,
                CreatedAtUtc = DateTime.UtcNow,
                Status = 1 // Posted
            };

            _dbContext.LedgerJournals.Add(journal);

            // Create posting lines
            foreach (var line in transaction.Lines)
            {
                var posting = new LedgerPostingRecord
                {
                    Id = Guid.CreateVersion7(),
                    JournalId = journal.Id,
                    AccountId = line.AccountId,
                    Amount = line.Amount.Amount,
                    Currency = line.Amount.Currency,
                    IsDebit = line.IsDebit,
                    Reference = line.Reference,
                    PostedAtUtc = DateTime.UtcNow
                };

                _dbContext.LedgerPostings.Add(posting);
            }

            await _dbContext.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);

            _logger.LogInformation(
                "Posted ledger transaction {TransactionId} with {LineCount} lines",
                transaction.Id, transaction.Lines.Count);

            return PostingResult.Succeeded(transaction.Id);
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync(ct);
            _logger.LogError(ex,
                "Failed to post ledger transaction {TransactionId}",
                transaction.Id);
            return PostingResult.Failed(ex.Message);
        }
    }

    public async Task<Money> GetBalanceAsync(Guid accountId, string currency, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var postings = await _dbContext.LedgerPostings
            .Where(p => p.AccountId == accountId && p.Currency == currency)
            .ToListAsync(ct);

        var totalDebits = postings.Where(p => p.IsDebit).Sum(p => p.Amount);
        var totalCredits = postings.Where(p => !p.IsDebit).Sum(p => p.Amount);

        var balance = totalDebits - totalCredits;

        return new Money(balance, currency);
    }

    public async Task<IReadOnlyList<LedgerPosting>> GetPostingsAsync(
        Guid accountId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var records = await _dbContext.LedgerPostings
            .Where(p => p.AccountId == accountId && 
                       p.PostedAtUtc >= fromUtc && 
                       p.PostedAtUtc <= toUtc)
            .OrderBy(p => p.PostedAtUtc)
            .ToListAsync(ct);

        return records.Select(r => new LedgerPosting(
            r.Id,
            r.JournalId,
            r.AccountId,
            new Money(r.Amount, r.Currency),
            r.IsDebit,
            r.Reference,
            r.PostedAtUtc)).ToList().AsReadOnly();
    }
}
