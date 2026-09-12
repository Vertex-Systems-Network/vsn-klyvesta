namespace Klyvesta.Domain.Orders;

using Klyvesta.Domain.Common;

/// <summary>
/// Order intent represents a validated intention to place an order.
/// It is created AFTER passing Risk Governor and Compliance Gate checks.
/// This is the last domain object before broker adapter invocation.
/// </summary>
public sealed class OrderIntent : IEntity
{
    public OrderIntentId Id { get; init; } = OrderIntentId.New();
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    
    public required AccountId AccountId { get; init; }
    public required InstrumentId InstrumentId { get; init; }
    public required Side Side { get; init; }
    public required string OrderType { get; init; } // "MARKET", "LIMIT", etc.
    public required decimal Quantity { get; init; }
    public decimal? LimitPrice { get; init; }
    public TimeInForce TimeInForce { get; init; } = TimeInForce.Day;
    
    /// <summary>
    /// Reference to AI proposal if this was AI-generated (Guarded Auto or Assisted mode).
    /// Null for manual orders.
    /// </summary>
    public ProposalId? AiProposalId { get; init; }
    
    /// <summary>
    /// Reference to mandate if this is a Guarded Auto order.
    /// </summary>
    public MandateId? MandateId { get; init; }
    
    /// <summary>
    /// Correlation ID for tracing across services.
    /// </summary>
    public required CorrelationId CorrelationId { get; init; }
    
    /// <summary>
    /// Idempotency key to prevent duplicate processing.
    /// </summary>
    public required string IdempotencyKey { get; init; }
    
    /// <summary>
    /// Risk Governor decision reference.
    /// </summary>
    public required Guid RiskCheckId { get; init; }
    
    /// <summary>
    /// Compliance Gate decision reference.
    /// </summary>
    public required Guid ComplianceCheckId { get; init; }
    
    private OrderState _state = OrderState.PendingSubmit;
    public OrderState State
    {
        get => _state;
        private set => _state = value;
    }
    
    public OrderId? BrokerOrderId { get; private set; }
    public string? ExternalOrderId { get; private set; }
    
    public decimal FilledQuantity { get; private set; }
    public decimal? AverageFillPrice { get; private set; }
    
    public DateTime? SubmittedAtUtc { get; private set; }
    public DateTime? FilledAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public DateTime? RejectedAtUtc { get; private set; }
    
    public void MarkSubmitted(OrderId brokerOrderId, string? externalOrderId, DateTime submittedAt)
    {
        if (_state != OrderState.PendingSubmit)
            throw new InvalidOperationException($"Cannot submit order in state {_state}");
        
        BrokerOrderId = brokerOrderId;
        ExternalOrderId = externalOrderId;
        SubmittedAtUtc = submittedAt;
        _state = OrderState.Submitted;
    }
    
    public void MarkOpen()
    {
        if (_state is not OrderState.Submitted and not OrderState.PartiallyFilled)
            throw new InvalidOperationException($"Cannot mark order as open in state {_state}");
        _state = OrderState.Open;
    }
    
    public void ApplyFill(decimal quantity, decimal price, DateTime fillTime)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Fill quantity must be positive");
        if (price <= 0)
            throw new ArgumentOutOfRangeException(nameof(price), "Fill price must be positive");
        
        var previousFilled = FilledQuantity;
        FilledQuantity += quantity;
        
        // Calculate new average price
        var previousTotalValue = previousFilled * (AverageFillPrice ?? 0);
        var newFillValue = quantity * price;
        var newTotalQuantity = FilledQuantity;
        AverageFillPrice = newTotalQuantity > 0 ? (previousTotalValue + newFillValue) / newTotalQuantity : null;
        
        if (FilledQuantity >= Quantity)
        {
            _state = OrderState.Filled;
            FilledAtUtc = fillTime;
        }
        else if (_state == OrderState.Submitted || _state == OrderState.Open)
        {
            _state = OrderState.PartiallyFilled;
        }
    }
    
    public void MarkCancelled(DateTime cancelledAt)
    {
        if (_state is OrderState.Filled or OrderState.Cancelled)
            throw new InvalidOperationException($"Cannot cancel order in state {_state}");
        
        CancelledAtUtc = cancelledAt;
        _state = OrderState.Cancelled;
    }
    
    public void MarkRejected(DateTime rejectedAt, string reason)
    {
        if (_state is OrderState.Filled or OrderState.Cancelled)
            throw new InvalidOperationException($"Cannot reject order in terminal state {_state}");
        
        RejectedAtUtc = rejectedAt;
        _state = OrderState.Rejected;
    }
    
    public void MarkUnknown()
    {
        // UNKNOWN state means we need to reconcile with broker
        _state = OrderState.Unknown;
    }
    
    public bool IsTerminal => _state is OrderState.Filled or OrderState.Cancelled or OrderState.Rejected;
    public bool CanBeCancelled => _state is OrderState.Submitted or OrderState.Open or OrderState.PartiallyFilled;
    public bool HasRemainingQuantity => FilledQuantity < Quantity;
    public decimal RemainingQuantity => Quantity - FilledQuantity;
}

/// <summary>
/// Result of order intent validation.
/// </summary>
public readonly record struct OrderIntentValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings
);

/// <summary>
/// Service interface for order intent management.
/// </summary>
public interface IOrderIntentService
{
    /// <summary>
    /// Creates a new order intent after validation.
    /// </summary>
    Task<OrderIntent> CreateOrderIntentAsync(OrderIntent intent, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets order intent by ID.
    /// </summary>
    Task<OrderIntent?> GetOrderIntentAsync(OrderIntentId intentId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates an order intent without creating it.
    /// </summary>
    Task<OrderIntentValidationResult> ValidateOrderIntentAsync(OrderIntent intent, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if an idempotency key has already been processed.
    /// </summary>
    Task<bool> IsIdempotencyKeyProcessedAsync(string idempotencyKey, CancellationToken cancellationToken = default);
}
