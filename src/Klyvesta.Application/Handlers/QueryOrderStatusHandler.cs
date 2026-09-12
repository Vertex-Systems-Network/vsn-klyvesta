using Klyvesta.Application.Commands;
using Klyvesta.Domain.Services;

namespace Klyvesta.Application.Handlers;

/// <summary>
/// Handles order status query commands.
/// </summary>
public sealed class QueryOrderStatusHandler
{
    private readonly IBrokerAdapter _brokerAdapter;

    public QueryOrderStatusHandler(IBrokerAdapter brokerAdapter)
    {
        _brokerAdapter = brokerAdapter;
    }

    /// <summary>
    /// Handle order status query.
    /// </summary>
    public async Task<OrderStatusResult> HandleAsync(
        QueryOrderStatusCommand command,
        CancellationToken ct)
    {
        var status = await _brokerAdapter.QueryOrderStatusAsync(command.OrderId, ct);

        return OrderStatusResult.Succeeded(
            command.OrderId,
            status.Status,
            status.FilledQuantity,
            status.RemainingQuantity,
            status.Executions);
    }
}

/// <summary>
/// Result of handling an order status query.
/// </summary>
public sealed class OrderStatusResult
{
    public bool Success { get; }
    public Guid? OrderId { get; }
    public Domain.Enums.OrderStatus Status { get; }
    public Domain.ValueObjects.Quantity FilledQuantity { get; }
    public Domain.ValueObjects.Quantity RemainingQuantity { get; }
    public IReadOnlyList<Domain.Entities.Execution> Executions { get; }
    public string? Error { get; }

    private OrderStatusResult(
        bool success,
        Guid? orderId,
        Domain.Enums.OrderStatus status,
        Domain.ValueObjects.Quantity filledQuantity,
        Domain.ValueObjects.Quantity remainingQuantity,
        IReadOnlyList<Domain.Entities.Execution> executions,
        string? error)
    {
        Success = success;
        OrderId = orderId;
        Status = status;
        FilledQuantity = filledQuantity;
        RemainingQuantity = remainingQuantity;
        Executions = executions;
        Error = error;
    }

    public static OrderStatusResult Succeeded(
        Guid orderId,
        Domain.Enums.OrderStatus status,
        Domain.ValueObjects.Quantity filledQuantity,
        Domain.ValueObjects.Quantity remainingQuantity,
        IReadOnlyList<Domain.Entities.Execution> executions)
        => new(true, orderId, status, filledQuantity, remainingQuantity, executions, null);

    public static OrderStatusResult Failed(string error)
        => new(false, null, Domain.Enums.OrderStatus.Unknown, Domain.ValueObjects.Quantity.Zero, Domain.ValueObjects.Quantity.Zero, Array.Empty<Domain.Entities.Execution>(), error);
}
