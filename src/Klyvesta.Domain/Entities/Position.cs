using Klyvesta.Domain.ValueObjects;

namespace Klyvesta.Domain.Entities;

/// <summary>
/// Projected position for a customer's holding in a specific symbol.
/// Updated based on executions, not intents.
/// </summary>
public sealed class Position
{
    public Guid Id { get; }
    public Guid CustomerId { get; }
    public string Symbol { get; }
    public Quantity Quantity { get; private set; }
    public Money AverageCostBasis { get; private set; }
    public DateTime LastUpdatedAtUtc { get; private set; }
    
    public Position(Guid id, Guid customerId, string symbol)
    {
        Id = id;
        CustomerId = customerId;
        Symbol = symbol.ToUpperInvariant();
        Quantity = Quantity.Zero;
        AverageCostBasis = Money.Zero;
        LastUpdatedAtUtc = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Update position based on a buy execution.
    /// </summary>
    public void AddBuyExecution(Quantity quantity, Money pricePerUnit)
    {
        if (quantity.Amount <= 0)
            throw new ArgumentException("Buy quantity must be positive", nameof(quantity));
        
        var totalCost = pricePerUnit * quantity.Amount;
        var currentTotalValue = AverageCostBasis * Quantity.Amount;
        var newTotalValue = currentTotalValue + totalCost;
        var newQuantity = Quantity + quantity;
        
        Quantity = newQuantity;
        AverageCostBasis = newTotalValue / newQuantity.Amount;
        LastUpdatedAtUtc = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Update position based on a sell execution.
    /// </summary>
    public void AddSellExecution(Quantity quantity, Money pricePerUnit)
    {
        if (quantity.Amount <= 0)
            throw new ArgumentException("Sell quantity must be positive", nameof(quantity));
        
        if (quantity > Quantity)
            throw new InvalidOperationException($"Cannot sell {quantity} when position is {Quantity}");
        
        Quantity = Quantity - quantity;
        
        // Average cost basis remains the same for remaining shares
        if (Quantity.Amount == 0)
        {
            AverageCostBasis = Money.Zero;
        }
        
        LastUpdatedAtUtc = DateTime.UtcNow;
    }
    
    public bool HasPosition => Quantity.Amount > 0;
}
