using Klyvesta.Domain.Entities;
using Klyvesta.Domain.ValueObjects;

namespace Klyvesta.Domain.Services;

/// <summary>
/// Position tracking service interface.
/// Maintains projected holdings based on executions.
/// </summary>
public interface IPositionService
{
    /// <summary>
    /// Get current position for a customer's symbol holding.
    /// </summary>
    Task<Position> GetPositionAsync(Guid customerId, string symbol, CancellationToken ct);
    
    /// <summary>
    /// Get all positions for a customer.
    /// </summary>
    Task<IReadOnlyList<Position>> GetAllPositionsAsync(Guid customerId, CancellationToken ct);
    
    /// <summary>
    /// Update position based on an execution.
    /// Internal method called by execution handler.
    /// </summary>
    Task ApplyExecutionAsync(Execution execution, OrderSide side, CancellationToken ct);
}
