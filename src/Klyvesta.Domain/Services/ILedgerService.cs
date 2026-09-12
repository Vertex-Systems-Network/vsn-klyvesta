using Klyvesta.Domain.ValueObjects;

namespace Klyvesta.Domain.Services;

/// <summary>
/// Double-entry ledger service interface.
/// Ensures debits always equal credits.
/// </summary>
public interface ILedgerService
{
    /// <summary>
    /// Post a double-entry transaction.
    /// Must have at least one debit and one credit that balance.
    /// </summary>
    Task<PostingResult> PostAsync(LedgerTransaction transaction, CancellationToken ct);
    
    /// <summary>
    /// Get current balance for an account.
    /// </summary>
    Task<Money> GetBalanceAsync(Guid accountId, string currency, CancellationToken ct);
    
    /// <summary>
    /// Get all postings for an account within a time range.
    /// </summary>
    Task<IReadOnlyList<LedgerPosting>> GetPostingsAsync(
        Guid accountId, 
        DateTime fromUtc, 
        DateTime toUtc, 
        CancellationToken ct);
}

/// <summary>
/// A complete double-entry transaction with balanced debits and credits.
/// </summary>
public sealed class LedgerTransaction
{
    public Guid Id { get; }
    public string Description { get; }
    public IReadOnlyList<LedgerPostingLine> Lines { get; }
    public string? CorrelationId { get; }
    public string? IdempotencyKey { get; }
    public DateTime CreatedAtUtc { get; }
    
    public LedgerTransaction(
        string description,
        IEnumerable<LedgerPostingLine> lines,
        string? correlationId = null,
        string? idempotencyKey = null)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required", nameof(description));
        
        var linesList = lines.ToList();
        if (linesList.Count < 2)
            throw new ArgumentException("Transaction must have at least two lines", nameof(lines));
        
        // Validate balance
        var totalDebits = linesList.Where(l => l.IsDebit).Sum(l => l.Amount.Amount);
        var totalCredits = linesList.Where(l => l.IsCredit).Sum(l => l.Amount.Amount);
        
        if (totalDebits != totalCredits)
            throw new ArgumentException(
                $"Transaction unbalanced: debits={totalDebits}, credits={totalCredits}", 
                nameof(lines));
        
        Id = Guid.CreateVersion7();
        Description = description;
        Lines = linesList;
        CorrelationId = correlationId;
        IdempotencyKey = idempotencyKey;
        CreatedAtUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// A single line in a ledger transaction (debit or credit).
/// </summary>
public sealed class LedgerPostingLine
{
    public Guid AccountId { get; }
    public Money Amount { get; }
    public bool IsDebit { get; }
    public bool IsCredit => !IsDebit;
    public string? Reference { get; }
    
    public LedgerPostingLine(Guid accountId, Money amount, bool isDebit, string? reference = null)
    {
        AccountId = accountId;
        Amount = amount;
        IsDebit = isDebit;
        Reference = reference;
    }
    
    public static LedgerPostingLine Debit(Guid accountId, Money amount, string? reference = null)
        => new(accountId, amount, true, reference);
    
    public static LedgerPostingLine Credit(Guid accountId, Money amount, string? reference = null)
        => new(accountId, amount, false, reference);
}

/// <summary>
/// Result of posting a transaction.
/// </summary>
public sealed class PostingResult
{
    public bool Success { get; }
    public Guid? TransactionId { get; }
    public string? Error { get; }
    
    private PostingResult(bool success, Guid? transactionId, string? error)
    {
        Success = success;
        TransactionId = transactionId;
        Error = error;
    }
    
    public static PostingResult Succeeded(Guid transactionId) 
        => new(true, transactionId, null);
    
    public static PostingResult Failed(string error) 
        => new(false, null, error);
}

/// <summary>
/// Immutable ledger posting record.
/// </summary>
public sealed class LedgerPosting
{
    public Guid Id { get; }
    public Guid TransactionId { get; }
    public Guid AccountId { get; }
    public Money Amount { get; }
    public bool IsDebit { get; }
    public string? Reference { get; }
    public DateTime PostedAtUtc { get; }
    
    public LedgerPosting(
        Guid id,
        Guid transactionId,
        Guid accountId,
        Money amount,
        bool isDebit,
        string? reference,
        DateTime postedAtUtc)
    {
        Id = id;
        TransactionId = transactionId;
        AccountId = accountId;
        Amount = amount;
        IsDebit = isDebit;
        Reference = reference;
        PostedAtUtc = postedAtUtc;
    }
}
