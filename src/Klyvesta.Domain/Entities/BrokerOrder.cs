using Klyvesta.Domain.Enums;
using Klyvesta.Domain.ValueObjects;

namespace Klyvesta.Domain.Entities;

/// <summary>
/// Represents an order submitted to the broker.
/// Tracks broker-specific state separate from customer intent.
/// </summary>
public sealed class BrokerOrder
{
    public Guid Id { get; }
    public Guid OrderIntentId { get; }
    public Guid CustomerId { get; }
    public string Symbol { get; }
    public OrderSide Side { get; }
    public OrderType Type { get; }
    public Quantity Quantity { get; }
    public Money? LimitPrice { get; }
    public TimeInForce TimeInForce { get; }
    public OrderStatus Status { get; private set; }
    public string? BrokerOrderId { get; private set; } // Assigned by broker on submit
    public RejectionReason? RejectionReason { get; private set; }
    public string? RejectionDetails { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private readonly List<Execution> _executions = new();
    public IReadOnlyList<Execution> Executions => _executions.AsReadOnly();

    public Quantity FilledQuantity => _executions.Sum(e => e.Quantity);
    public Quantity RemainingQuantity => Quantity - FilledQuantity;

    public bool IsTerminalState =>
        Status is OrderStatus.Filled or OrderStatus.Cancelled or OrderStatus.Rejected;

    public BrokerOrder(
        Guid id,
        Guid orderIntentId,
        Guid customerId,
        string symbol,
        OrderSide side,
        OrderType type,
        Quantity quantity,
        Money? limitPrice,
        TimeInForce timeInForce,
        string? idempotencyKey = null)
    {
        Id = id;
        OrderIntentId = orderIntentId;
        CustomerId = customerId;
        Symbol = symbol.ToUpperInvariant();
        Side = side;
        Type = type;
        Quantity = quantity;
        LimitPrice = limitPrice;
        TimeInForce = timeInForce;
        Status = OrderStatus.Pending;
        IdempotencyKey = idempotencyKey;
    }

    public string? IdempotencyKey { get; private set; }

    public void SubmitToBroker()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException($"Cannot submit order in {Status} state");

        // Generate broker-assigned order ID
        BrokerOrderId = Guid.CreateVersion7().ToString("N");
        Status = OrderStatus.Submitted;
        SubmittedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = SubmittedAtUtc;
    }

    public void MarkSubmitted(string brokerOrderId)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException($"Cannot submit order in {Status} state");

        BrokerOrderId = brokerOrderId ?? throw new ArgumentNullException(nameof(brokerOrderId));
        Status = OrderStatus.Submitted;
        SubmittedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = SubmittedAtUtc;
    }

    public void AddExecution(Execution execution)
    {
        if (IsTerminalState)
            throw new InvalidOperationException($"Cannot add execution to terminal order: {Status}");

        _executions.Add(execution);
        UpdateStatusFromExecutions();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (IsTerminalState)
            throw new InvalidOperationException($"Cannot cancel terminal order: {Status}");

        Status = OrderStatus.Cancelled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reject(RejectionReason reason, string? details = null)
    {
        if (IsTerminalState)
            throw new InvalidOperationException($"Cannot reject terminal order: {Status}");

        Status = OrderStatus.Rejected;
        RejectionReason = reason;
        RejectionDetails = details;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkUnknown()
    {
        if (IsTerminalState)
            throw new InvalidOperationException($"Cannot mark terminal order as unknown: {Status}");

        Status = OrderStatus.Unknown;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private void UpdateStatusFromExecutions()
    {
        if (FilledQuantity == Quantity)
        {
            Status = OrderStatus.Filled;
        }
        else if (FilledQuantity > Quantity.Zero)
        {
            Status = OrderStatus.PartiallyFilled;
        }
    }
}
