using Klyvesta.Domain.Ledger;
using Klyvesta.Domain.Orders;

namespace Klyvesta.Domain.Activity;

public enum CustomerActivitySource
{
    Order = 1,
    Ledger = 2,
}

public enum CustomerActivityCode
{
    OrderIntentCreated = 1,
    OrderValidationPending = 2,
    OrderRejected = 3,
    OrderApproved = 4,
    OrderExecutionPending = 5,
    OrderExecutionCreated = 6,
    OrderSubmitPending = 7,
    OrderSubmitting = 8,
    OrderSubmitted = 9,
    OrderOpen = 10,
    OrderPartiallyFilled = 11,
    OrderCancelPending = 12,
    OrderFilled = 13,
    OrderCancelled = 14,
    OrderExpired = 15,
    OrderStatusUnknown = 16,
    LedgerPosted = 17,
    LedgerReversal = 18,
}

public enum CustomerActivityFlow
{
    None = 0,
    Debit = 1,
    Credit = 2,
    Mixed = 3,
}

public enum CustomerActivityAuthority
{
    ReadOnlyPaper = 1,
}

public sealed record CustomerOrderActivityEvidence(
    Guid SourceEventId,
    Guid CustomerId,
    DateTimeOffset OccurredAt,
    OrderIntent Intent,
    ManagedBrokerOrder? BrokerOrder);

public sealed record CustomerLedgerActivityEvidence(
    Guid CustomerId,
    LedgerJournalEntry Entry,
    IReadOnlyList<LedgerAccount> Accounts);

public sealed record CustomerActivityItem(
    Guid SourceEventId,
    CustomerActivitySource Source,
    CustomerActivityCode Code,
    DateTimeOffset OccurredAt,
    string? InstrumentReference,
    decimal? Quantity,
    decimal? FilledQuantity,
    decimal? Amount,
    string? Currency,
    CustomerActivityFlow Flow);

public sealed record CustomerActivityTimeline(
    Guid CustomerId,
    string AccountReference,
    DateTimeOffset AsOf,
    IReadOnlyList<CustomerActivityItem> Items,
    CustomerActivityAuthority Authority);
