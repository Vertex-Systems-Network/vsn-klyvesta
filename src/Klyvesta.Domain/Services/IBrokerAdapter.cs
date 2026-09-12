using Klyvesta.Domain.Entities;
using Klyvesta.Domain.ValueObjects;
using Klyvesta.Domain.Enums;

namespace Klyvesta.Domain.Services;

/// <summary>
/// Normalized broker adapter contract.
/// Implemented by PaperBrokerAdapter for testing and future live adapters.
/// </summary>
public interface IBrokerAdapter
{
    /// <summary>
    /// Submit an order to the broker.
    /// Returns result with deterministic outcomes based on scenario configuration.
    /// </summary>
    Task<BrokerSubmitResult> SubmitOrderAsync(OrderIntent order, CancellationToken ct);
    
    /// <summary>
    /// Cancel an existing order.
    /// </summary>
    Task<BrokerCancelResult> CancelOrderAsync(Guid orderIntentId, CancellationToken ct);
    
    /// <summary>
    /// Query current order status.
    /// </summary>
    Task<BrokerOrderStatus> QueryOrderStatusAsync(Guid orderIntentId, CancellationToken ct);
    
    /// <summary>
    /// Get current cash balance for a customer (paper simulation).
    /// </summary>
    Task<Money> GetCashBalanceAsync(Guid customerId, CancellationToken ct);
    
    /// <summary>
    /// Get current position quantity for a symbol (paper simulation).
    /// </summary>
    Task<Quantity> GetPositionAsync(Guid customerId, string symbol, CancellationToken ct);
    
    /// <summary>
    /// Check if market is currently open for trading.
    /// </summary>
    Task<bool> IsMarketOpenAsync(CancellationToken ct);
    
    /// <summary>
    /// Get health status of the broker connection.
    /// </summary>
    Task<BrokerHealth> GetHealthAsync(CancellationToken ct);
}

/// <summary>
/// Result of submitting an order to the broker.
/// </summary>
public sealed class BrokerSubmitResult
{
    public bool Success { get; }
    public Guid? OrderId { get; }
    public RejectionReason? RejectionReason { get; }
    public string? Details { get; }
    public bool IsUnknown { get; } // True when outcome is ambiguous
    
    private BrokerSubmitResult(bool success, Guid? orderId, RejectionReason? reason, string? details, bool isUnknown)
    {
        Success = success;
        OrderId = orderId;
        RejectionReason = reason;
        Details = details;
        IsUnknown = isUnknown;
    }
    
    public static BrokerSubmitResult Succeeded(Guid orderId) 
        => new(true, orderId, null, null, false);
    
    public static BrokerSubmitResult Rejected(RejectionReason reason, string? details = null) 
        => new(false, null, reason, details, false);
    
    public static BrokerSubmitResult Unknown(string details) 
        => new(false, null, null, details, true);
    
    public static BrokerSubmitResult Failed(string details) 
        => new(false, null, null, details, false);
}

/// <summary>
/// Result of cancelling an order.
/// </summary>
public sealed class BrokerCancelResult
{
    public bool Success { get; }
    public string? Details { get; }
    
    public BrokerCancelResult(bool success, string? details)
    {
        Success = success;
        Details = details;
    }
}

/// <summary>
/// Current order status from broker perspective.
/// </summary>
public sealed class BrokerOrderStatus
{
    public OrderStatus Status { get; }
    public Quantity FilledQuantity { get; }
    public Quantity RemainingQuantity { get; }
    public IReadOnlyList<Execution> Executions { get; }
    
    public BrokerOrderStatus(
        OrderStatus status,
        Quantity filledQuantity,
        Quantity remainingQuantity,
        IReadOnlyList<Execution> executions)
    {
        Status = status;
        FilledQuantity = filledQuantity;
        RemainingQuantity = remainingQuantity;
        Executions = executions;
    }
}

/// <summary>
/// Broker health status.
/// </summary>
public enum BrokerHealth
{
    Healthy = 0,
    Degraded = 1,
    Unavailable = 2
}
