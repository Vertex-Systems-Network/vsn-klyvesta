namespace Klyvesta.Domain.Ledger;

using Klyvesta.Domain.Common;

/// <summary>
/// Type of ledger account.
/// </summary>
public enum LedgerAccountType
{
    Asset = 0,
    Liability = 1,
    Equity = 2,
    Revenue = 3,
    Expense = 4
}

/// <summary>
/// A ledger account in the double-entry system.
/// Immutable once posted to - corrections require reversing entries.
/// </summary>
public sealed class LedgerAccount : IEntity
{
    public LedgerAccountId Id { get; init; } = LedgerAccountId.New();
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    
    public required string AccountCode { get; init; }
    public required string Name { get; init; }
    public required LedgerAccountType Type { get; init; }
    public required string Currency { get; init; }
    
    /// <summary>
    /// Optional reference to external entity (customer, broker, etc.)
    /// </summary>
    public Guid? ExternalReferenceId { get; init; }
    public string? ExternalReferenceType { get; init; }
    
    public bool IsActive { get; private set; } = true;
    
    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidOperationException($"Account {Id} is already deactivated");
        IsActive = false;
    }
}

/// <summary>
/// A journal entry containing balanced debit/credit postings.
/// Once committed, a journal is immutable.
/// </summary>
public sealed class Journal : IEntity
{
    public JournalId Id { get; init; } = JournalId.New();
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    
    public required string Description { get; init; }
    public DateTime EffectiveDateUtc { get; init; }
    
    /// <summary>
    /// Reference to source event/command for audit trail.
    /// </summary>
    public required string SourceType { get; init; }
    public required Guid SourceId { get; init; }
    
    /// <summary>
    /// Idempotency key to prevent duplicate journal creation.
    /// </summary>
    public required string IdempotencyKey { get; init; }
    
    /// <summary>
    /// Optional external reference for cross-system correlation.
    /// </summary>
    public string? ExternalReference { get; init; }
    
    /// <summary>
    /// Currency of this journal (must match all postings).
    /// </summary>
    public required string Currency { get; init; }
    
    private readonly List<Posting> _postings = new();
    public IReadOnlyList<Posting> Postings => _postings.AsReadOnly();
    
    public bool IsCommitted { get; private set; }
    
    /// <summary>
    /// Cached totals computed on commit.
    /// </summary>
    public decimal TotalDebits { get; private set; }
    public decimal TotalCredits { get; private set; }
    
    public void AddPosting(Posting posting)
    {
        if (IsCommitted)
            throw new InvalidOperationException("Cannot add postings to committed journal");
        _postings.Add(posting);
    }
    
    public void Commit()
    {
        if (IsCommitted)
            throw new InvalidOperationException($"Journal {Id} is already committed");
        
        // Validate balance: sum of debits must equal sum of credits
        TotalDebits = _postings.Where(p => p.IsDebit).Sum(p => p.Amount.Amount);
        TotalCredits = _postings.Where(p => p.IsCredit).Sum(p => p.Amount.Amount);
        
        if (TotalDebits != TotalCredits)
            throw new InvalidOperationException(
                $"Journal {Id} is not balanced. Debits: {TotalDebits}, Credits: {TotalCredits}");
        
        if (_postings.Count == 0)
            throw new InvalidOperationException($"Journal {Id} has no postings");
        
        IsCommitted = true;
    }
}

/// <summary>
/// A single debit or credit posting within a journal.
/// </summary>
public sealed class Posting : IEntity<Guid>
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required LedgerAccountId AccountId { get; init; }
    public required Money Amount { get; init; }
    public required bool IsDebit { get; init; }
    
    public bool IsCredit => !IsDebit;
    
    public string? Description { get; init; }
    
    /// <summary>
    /// Optional reference to sub-ledger entity (position, order, etc.)
    /// </summary>
    public Guid? SubLedgerReferenceId { get; init; }
    public string? SubLedgerType { get; init; }
    
    /// <summary>
    /// Entry type (debit/credit) for clarity.
    /// </summary>
    public string EntryType => IsDebit ? "Debit" : "Credit";
    
    /// <summary>
    /// Currency of the posting amount.
    /// </summary>
    public string Currency => Amount.Currency;
    
    /// <summary>
    /// Timestamp when this posting was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Result of validating journal balance.
/// </summary>
public readonly record struct BalanceValidationResult(
    bool IsBalanced,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal Difference,
    string Currency
);

/// <summary>
/// Service interface for ledger operations.
/// </summary>
public interface ILedgerService
{
    /// <summary>
    /// Creates a new ledger account.
    /// </summary>
    Task<LedgerAccount> CreateAccountAsync(LedgerAccount account, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets account by ID.
    /// </summary>
    Task<LedgerAccount?> GetAccountAsync(LedgerAccountId accountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates and commits a journal entry atomically.
    /// Returns the committed journal or throws on validation failure.
    /// </summary>
    Task<Journal> CreateJournalAsync(Journal journal, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates that a journal is balanced without persisting.
    /// </summary>
    BalanceValidationResult ValidateJournalBalance(Journal journal);
    
    /// <summary>
    /// Checks if an idempotency key has already been processed.
    /// </summary>
    Task<bool> IsIdempotencyKeyProcessedAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a reversing journal entry for a previous journal.
    /// </summary>
    Task<Journal> CreateReversalAsync(
        JournalId originalJournalId,
        string reversalReason,
        string newIdempotencyKey,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets current balance for an account.
    /// </summary>
    Task<Money> GetAccountBalanceAsync(LedgerAccountId accountId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Invariant violation in the ledger system.
/// </summary>
public sealed class LedgerInvariantException : Exception
{
    public LedgerInvariantException(string message) : base(message) { }
    
    public LedgerInvariantException(string message, Exception inner) : base(message, inner) { }
}
