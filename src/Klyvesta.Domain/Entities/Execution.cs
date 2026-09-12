using Klyvesta.Domain.ValueObjects;

namespace Klyvesta.Domain.Entities;

/// <summary>
/// Represents a single fill/execution of an order.
/// Each execution has a stable unique ID for deduplication.
/// </summary>
public sealed class Execution
{
    public Guid Id { get; }
    public Guid OrderIntentId { get; }
    public string ExecutionId { get; } // Stable ID from broker for deduplication
    public Quantity Quantity { get; }
    public Money PricePerUnit { get; }
    public Money TotalValue => PricePerUnit * Quantity.Amount;
    public DateTime ExecutedAtUtc { get; }
    public string? BrokerReference { get; }
    
    public Execution(
        Guid id,
        Guid orderIntentId,
        string executionId,
        Quantity quantity,
        Money pricePerUnit,
        DateTime executedAtUtc,
        string? brokerReference = null)
    {
        if (string.IsNullOrWhiteSpace(executionId))
            throw new ArgumentException("Execution ID is required", nameof(executionId));
        
        if (quantity.Amount <= 0)
            throw new ArgumentException("Execution quantity must be positive", nameof(quantity));
        
        Id = id;
        OrderIntentId = orderIntentId;
        ExecutionId = executionId;
        Quantity = quantity;
        PricePerUnit = pricePerUnit;
        ExecutedAtUtc = executedAtUtc;
        BrokerReference = brokerReference;
    }
}
