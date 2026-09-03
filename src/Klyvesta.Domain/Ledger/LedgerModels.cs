using System.Collections.ObjectModel;

namespace Klyvesta.Domain.Ledger;

public enum LedgerSide
{
    Unknown = 0,
    Debit = 1,
    Credit = 2,
}

public enum LedgerAccountType
{
    Unknown = 0,
    CustomerCash = 1,
    ReservedCash = 2,
    SettlementReceivable = 3,
    SettlementPayable = 4,
    Fee = 5,
    Tax = 6,
    Clearing = 7,
    Suspense = 8,
}

public sealed class LedgerAccount
{
    public LedgerAccount(
        Guid accountId,
        string accountReference,
        string currency,
        LedgerAccountType accountType,
        string? ownerCustomerId = null)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Ledger account ID is required.", nameof(accountId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(accountReference);
        if (accountType == LedgerAccountType.Unknown)
        {
            throw new ArgumentException("Ledger account type is required.", nameof(accountType));
        }

        AccountId = accountId;
        AccountReference = accountReference;
        Currency = LedgerCurrency.Normalize(currency);
        AccountType = accountType;
        OwnerCustomerId = ownerCustomerId;
    }

    public Guid AccountId { get; }

    public string AccountReference { get; }

    public string Currency { get; }

    public LedgerAccountType AccountType { get; }

    public string? OwnerCustomerId { get; }
}

public sealed class LedgerPosting
{
    public const int MaximumAmountScale = 8;

    public LedgerPosting(Guid accountId, LedgerSide side, decimal amount, string currency)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Posting account ID is required.", nameof(accountId));
        }

        if (side is not (LedgerSide.Debit or LedgerSide.Credit))
        {
            throw new ArgumentException("Posting side must be Debit or Credit.", nameof(side));
        }

        LedgerDecimal.RequirePositive(amount, MaximumAmountScale, nameof(amount));

        AccountId = accountId;
        Side = side;
        Amount = amount;
        Currency = LedgerCurrency.Normalize(currency);
    }

    public Guid AccountId { get; }

    public LedgerSide Side { get; }

    public decimal Amount { get; }

    public string Currency { get; }
}

public sealed class LedgerJournalEntry
{
    private readonly ReadOnlyCollection<LedgerPosting> _lines;

    public LedgerJournalEntry(
        Guid entryId,
        string entryType,
        string correlationId,
        string actorReference,
        string operation,
        string idempotencyKey,
        string normalizedRequestHash,
        DateTimeOffset effectiveAt,
        DateTimeOffset postedAt,
        Guid? reversesEntryId,
        IEnumerable<LedgerPosting> lines)
    {
        if (entryId == Guid.Empty)
        {
            throw new ArgumentException("Journal entry ID is required.", nameof(entryId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(entryType);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedRequestHash);
        ArgumentNullException.ThrowIfNull(lines);

        var materializedLines = lines.ToArray();
        if (materializedLines.Length < 2)
        {
            throw new ArgumentException("A posted journal entry requires at least two lines.", nameof(lines));
        }

        ValidateBalanced(materializedLines);

        EntryId = entryId;
        EntryType = entryType;
        CorrelationId = correlationId;
        ActorReference = actorReference;
        Operation = operation;
        IdempotencyKey = idempotencyKey;
        NormalizedRequestHash = normalizedRequestHash;
        EffectiveAt = effectiveAt;
        PostedAt = postedAt;
        ReversesEntryId = reversesEntryId;
        _lines = Array.AsReadOnly(materializedLines);
    }

    public Guid EntryId { get; }

    public string EntryType { get; }

    public string CorrelationId { get; }

    public string ActorReference { get; }

    public string Operation { get; }

    public string IdempotencyKey { get; }

    public string NormalizedRequestHash { get; }

    public DateTimeOffset EffectiveAt { get; }

    public DateTimeOffset PostedAt { get; }

    public Guid? ReversesEntryId { get; }

    public IReadOnlyList<LedgerPosting> Lines => _lines;

    private static void ValidateBalanced(IReadOnlyCollection<LedgerPosting> lines)
    {
        foreach (var currencyGroup in lines.GroupBy(line => line.Currency, StringComparer.Ordinal))
        {
            var debitTotal = currencyGroup
                .Where(line => line.Side == LedgerSide.Debit)
                .Sum(line => line.Amount);
            var creditTotal = currencyGroup
                .Where(line => line.Side == LedgerSide.Credit)
                .Sum(line => line.Amount);

            if (debitTotal != creditTotal)
            {
                throw new ArgumentException(
                    $"Journal entry is not balanced for currency {currencyGroup.Key}.",
                    nameof(lines));
            }
        }
    }
}

public static class LedgerDecimal
{
    public static void RequirePositive(decimal amount, int maximumScale, string parameterName)
    {
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Ledger amounts must be greater than zero.");
        }

        var scale = (decimal.GetBits(amount)[3] >> 16) & 0x7F;
        if (scale > maximumScale)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Ledger amount scale {scale} exceeds the supported scale {maximumScale}.");
        }
    }
}

public static class LedgerCurrency
{
    public static string Normalize(string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || normalized.Any(character => character is < 'A' or > 'Z'))
        {
            throw new ArgumentException("Currency must be a three-letter alphabetic code.", nameof(currency));
        }

        return normalized;
    }
}
