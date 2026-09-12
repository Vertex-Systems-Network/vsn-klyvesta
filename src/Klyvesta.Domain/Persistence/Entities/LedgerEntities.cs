using System;
using Klyvesta.Domain.Common;
using Klyvesta.Domain.Ledger;

namespace Klyvesta.Domain.Persistence.Entities;

/// <summary>
/// PostgreSQL entity for LedgerAccount with EF Core mappings.
/// Append-only: accounts are never deleted, only referenced by journals.
/// </summary>
public class LedgerAccountEntity : IObserved
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Strongly-typed identifier wrapper
    /// </summary>
    public string LedgerAccountId { get; set; } = string.Empty;
    
    /// <summary>
    /// Human-readable account code (e.g., "1000-CASH-USD")
    /// </summary>
    public string AccountCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Descriptive name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Account type determines normal balance direction
    /// </summary>
    public AccountType Type { get; set; }
    
    /// <summary>
    /// Currency code (ISO 4217)
    /// </summary>
    public string Currency { get; set; } = "USD";
    
    /// <summary>
    /// Parent account for hierarchical chart of accounts
    /// </summary>
    public Guid? ParentAccountId { get; set; }
    
    /// <summary>
    /// Account is active and can accept postings
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Current debit balance in minor units (cents)
    /// </summary>
    public long DebitBalanceMinorUnits { get; set; }
    
    /// <summary>
    /// Current credit balance in minor units (cents)
    /// </summary>
    public long CreditBalanceMinorUnits { get; set; }
    
    /// <summary>
    /// Last posting timestamp
    /// </summary>
    public DateTime LastPostedAt { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAtUtc { get; set; }
    
    /// <summary>
    /// Navigation: historical journal entries
    /// </summary>
    public virtual ICollection<JournalEntity> Journals { get; set; } = new List<JournalEntity>();
}

/// <summary>
/// PostgreSQL entity for Journal (immutable after commit).
/// Represents a single accounting transaction with balanced debits/credits.
/// </summary>
public class JournalEntity : IObserved
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Strongly-typed identifier wrapper
    /// </summary>
    public string JournalId { get; set; } = string.Empty;
    
    /// <summary>
    /// Reference to originating business event (order execution, fee, etc.)
    /// </summary>
    public string? ExternalReference { get; set; }
    
    /// <summary>
    /// Idempotency key to prevent duplicate processing
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Journal description/narrative
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Total debits in minor units (must equal credits)
    /// </summary>
    public long TotalDebitsMinorUnits { get; set; }
    
    /// <summary>
    /// Total credits in minor units (must equal debits)
    /// </summary>
    public long TotalCreditsMinorUnits { get; set; }
    
    /// <summary>
    /// Currency code (all postings must match)
    /// </summary>
    public string Currency { get; set; } = "USD";
    
    /// <summary>
    /// Journal state: Draft -> Committed -> (optionally) Reversed
    /// </summary>
    public JournalState State { get; set; } = JournalState.Draft;
    
    /// <summary>
    /// Timestamp when journal was committed (became immutable)
    /// </summary>
    public DateTime? CommittedAtUtc { get; set; }
    
    /// <summary>
    /// If reversed, reference to reversal journal
    /// </summary>
    public Guid? ReversalJournalId { get; set; }
    
    /// <summary>
    /// Original journal being reversed (if this is a reversal)
    /// </summary>
    public Guid? ReversesJournalId { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAtUtc { get; set; }
    
    /// <summary>
    /// Foreign key to ledger account (optional, for categorization)
    /// </summary>
    public Guid? LedgerAccountId { get; set; }
    public virtual LedgerAccountEntity? LedgerAccount { get; set; }
    
    /// <summary>
    /// Postings (individual debit/credit entries)
    /// </summary>
    public virtual ICollection<PostingEntity> Postings { get; set; } = new List<PostingEntity>();
}

/// <summary>
/// PostgreSQL entity for Posting (individual debit or credit entry).
/// Each posting belongs to exactly one journal.
/// </summary>
public class PostingEntity : IObserved
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Strongly-typed identifier wrapper
    /// </summary>
    public string PostingId { get; set; } = string.Empty;
    
    /// <summary>
    /// Foreign key to parent journal
    /// </summary>
    public Guid JournalId { get; set; }
    public virtual JournalEntity Journal { get; set; } = null!;
    
    /// <summary>
    /// Foreign key to ledger account
    /// </summary>
    public Guid LedgerAccountId { get; set; }
    public virtual LedgerAccountEntity LedgerAccount { get; set; } = null!;
    
    /// <summary>
    /// Entry type: Debit or Credit
    /// </summary>
    public EntryType EntryType { get; set; }
    
    /// <summary>
    /// Amount in minor units (cents)
    /// </summary>
    public long AmountMinorUnits { get; set; }
    
    /// <summary>
    /// Currency code (must match journal currency)
    /// </summary>
    public string Currency { get; set; } = "USD";
    
    /// <summary>
    /// Optional line description
    /// </summary>
    public string? Description { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAtUtc { get; set; }
}

/// <summary>
/// Account type enum matching double-entry accounting standards
/// </summary>
public enum AccountType
{
    Asset = 0,
    Liability = 1,
    Equity = 2,
    Revenue = 3,
    Expense = 4
}

/// <summary>
/// Journal lifecycle states
/// </summary>
public enum JournalState
{
    Draft = 0,          // Can be modified
    Committed = 1,      // Immutable, posted to ledger
    Reversed = 2        // Has been reversed by compensating entry
}

/// <summary>
/// Debit or Credit entry type
/// </summary>
public enum EntryType
{
    Debit = 0,
    Credit = 1
}
