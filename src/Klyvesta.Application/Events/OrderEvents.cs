using Klyvesta.Domain.ValueObjects;

namespace Klyvesta.Application.Events;

/// <summary>
/// Domain event raised when an order intent is submitted.
/// </summary>
public sealed class OrderIntentSubmittedEvent
{
    public Guid OrderIntentId { get; }
    public Guid CustomerId { get; }
    public string Symbol { get; }
    public Domain.Enums.OrderSide Side { get; }
    public Domain.Enums.OrderType Type { get; }
    public Quantity Quantity { get; }
    public Money? LimitPrice { get; }
    public DateTime SubmittedAtUtc { get; }

    public OrderIntentSubmittedEvent(
        Guid orderIntentId,
        Guid customerId,
        string symbol,
        Domain.Enums.OrderSide side,
        Domain.Enums.OrderType type,
        Quantity quantity,
        Money? limitPrice,
        DateTime submittedAtUtc)
    {
        OrderIntentId = orderIntentId;
        CustomerId = customerId;
        Symbol = symbol;
        Side = side;
        Type = type;
        Quantity = quantity;
        LimitPrice = limitPrice;
        SubmittedAtUtc = submittedAtUtc;
    }
}

/// <summary>
/// Domain event raised when an order status changes.
/// </summary>
public sealed class OrderStatusChangedEvent
{
    public Guid OrderIntentId { get; }
    public Guid CustomerId { get; }
    public Domain.Enums.OrderStatus PreviousStatus { get; }
    public Domain.Enums.OrderStatus NewStatus { get; }
    public DateTime ChangedAtUtc { get; }

    public OrderStatusChangedEvent(
        Guid orderIntentId,
        Guid customerId,
        Domain.Enums.OrderStatus previousStatus,
        Domain.Enums.OrderStatus newStatus,
        DateTime changedAtUtc)
    {
        OrderIntentId = orderIntentId;
        CustomerId = customerId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        ChangedAtUtc = changedAtUtc;
    }
}

/// <summary>
/// Domain event raised when an execution (fill) occurs.
/// </summary>
public sealed class ExecutionReceivedEvent
{
    public Guid ExecutionId { get; }
    public Guid OrderIntentId { get; }
    public Guid CustomerId { get; }
    public Quantity Quantity { get; }
    public Money PricePerUnit { get; }
    public Money TotalValue { get; }
    public DateTime ExecutedAtUtc { get; }

    public ExecutionReceivedEvent(
        Guid executionId,
        Guid orderIntentId,
        Guid customerId,
        Quantity quantity,
        Money pricePerUnit,
        DateTime executedAtUtc)
    {
        ExecutionId = executionId;
        OrderIntentId = orderIntentId;
        CustomerId = customerId;
        Quantity = quantity;
        PricePerUnit = pricePerUnit;
        TotalValue = pricePerUnit * quantity.Amount;
        ExecutedAtUtc = executedAtUtc;
    }
}

/// <summary>
/// Domain event raised when a position is updated.
/// </summary>
public sealed class PositionUpdatedEvent
{
    public Guid CustomerId { get; }
    public string Symbol { get; }
    public Quantity NewQuantity { get; }
    public Money NewAverageCostBasis { get; }
    public DateTime UpdatedAtUtc { get; }

    public PositionUpdatedEvent(
        Guid customerId,
        string symbol,
        Quantity newQuantity,
        Money newAverageCostBasis,
        DateTime updatedAtUtc)
    {
        CustomerId = customerId;
        Symbol = symbol;
        NewQuantity = newQuantity;
        NewAverageCostBasis = newAverageCostBasis;
        UpdatedAtUtc = updatedAtUtc;
    }
}

/// <summary>
/// Domain event raised when a ledger transaction is posted.
/// </summary>
public sealed class LedgerTransactionPostedEvent
{
    public Guid TransactionId { get; }
    public string Description { get; }
    public IReadOnlyList<LedgerPostingSummary> Postings { get; }
    public DateTime PostedAtUtc { get; }

    public LedgerTransactionPostedEvent(
        Guid transactionId,
        string description,
        IEnumerable<LedgerPostingSummary> postings,
        DateTime postedAtUtc)
    {
        TransactionId = transactionId;
        Description = description;
        Postings = postings.ToList().AsReadOnly();
        PostedAtUtc = postedAtUtc;
    }
}

/// <summary>
/// Summary of a ledger posting for event publication.
/// </summary>
public sealed class LedgerPostingSummary
{
    public Guid AccountId { get; }
    public Money Amount { get; }
    public bool IsDebit { get; }
    public string? Reference { get; }

    public LedgerPostingSummary(Guid accountId, Money amount, bool isDebit, string? reference)
    {
        AccountId = accountId;
        Amount = amount;
        IsDebit = isDebit;
        Reference = reference;
    }
}
