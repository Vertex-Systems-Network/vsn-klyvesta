using Klyvesta.Domain.Enums;
using Klyvesta.Domain.ValueObjects;

namespace Klyvesta.Domain.Entities;

/// <summary>
/// Customer order intent before broker submission.
/// Captures what the customer wants to do, not yet executed.
/// </summary>
public sealed class OrderIntent
{
    public Guid Id { get; }
    public Guid CustomerId { get; }
    public string Symbol { get; }
    public OrderSide Side { get; }
    public OrderType Type { get; }
    public Quantity Quantity { get; }
    public Money? LimitPrice { get; } // Null for market orders
    public TimeInForce TimeInForce { get; }
    public string? IdempotencyKey { get; }
    public OrderStatus Status { get; private set; }
    public RejectionReason? RejectionReason { get; private set; }
    public string? RejectionDetails { get; private set; }
    public DateTime CreatedAtUtc { get; }
    public DateTime? UpdatedAtUtc { get; private set; }
    
    // Navigation (for domain tracking, not persistence)
    private readonly List<Execution> _executions = new();
    public IReadOnlyList<Execution> Executions => _executions.AsReadOnly();
    
    public Quantity FilledQuantity => _executions.Sum(e => e.Quantity);
    public Quantity RemainingQuantity => Quantity - FilledQuantity;
    
    public bool IsTerminalState => 
        Status is OrderStatus.Filled or OrderStatus.Cancelled or OrderStatus.Rejected;
    
    public OrderIntent(
        Guid id,
        Guid customerId,
        string symbol,
        OrderSide side,
        OrderType type,
        Quantity quantity,
        Money? limitPrice,
        TimeInForce timeInForce,
        string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required", nameof(symbol));
        
        Id = id;
        CustomerId = customerId;
        Symbol = symbol.ToUpperInvariant();
        Side = side;
        Type = type;
        Quantity = quantity;
        LimitPrice = limitPrice;
        TimeInForce = timeInForce;
        IdempotencyKey = idempotencyKey;
        Status = OrderStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }
    
    public void Submit()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException($"Cannot submit order in {Status} state");
        
        Status = OrderStatus.Submitted;
        UpdatedAtUtc = DateTime.UtcNow;
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
