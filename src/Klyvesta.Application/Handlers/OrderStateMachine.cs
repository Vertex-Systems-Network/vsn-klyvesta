using Klyvesta.Domain.Entities;
using Klyvesta.Domain.Enums;

namespace Klyvesta.Application.Handlers;

/// <summary>
/// State machine for order lifecycle management.
/// Enforces valid state transitions and invariants.
/// </summary>
public sealed class OrderStateMachine
{
    private readonly OrderIntent _order;

    public OrderStateMachine(OrderIntent order)
    {
        _order = order ?? throw new ArgumentNullException(nameof(order));
    }

    /// <summary>
    /// Current order status.
    /// </summary>
    public OrderStatus CurrentStatus => _order.Status;

    /// <summary>
    /// Check if order is in a terminal state.
    /// </summary>
    public bool IsTerminal => _order.IsTerminalState;

    /// <summary>
    /// Transition order from Pending to Submitted.
    /// </summary>
    public void Submit()
    {
        if (_order.Status != OrderStatus.Pending)
            throw new InvalidOperationException($"Cannot submit order in {_order.Status} state");

        _order.Submit();
    }

    /// <summary>
    /// Apply an execution (fill) to the order.
    /// </summary>
    public void AddExecution(Execution execution)
    {
        if (_order.IsTerminalState)
            throw new InvalidOperationException($"Cannot add execution to terminal order: {_order.Status}");

        if (execution.Quantity > _order.RemainingQuantity)
            throw new InvalidOperationException($"Execution quantity {execution.Quantity} exceeds remaining {(_order.RemainingQuantity)}");

        _order.AddExecution(execution);
    }

    /// <summary>
    /// Cancel the order.
    /// </summary>
    public void Cancel()
    {
        if (_order.IsTerminalState)
            throw new InvalidOperationException($"Cannot cancel terminal order: {_order.Status}");

        _order.Cancel();
    }

    /// <summary>
    /// Reject the order with a reason.
    /// </summary>
    public void Reject(RejectionReason reason, string? details = null)
    {
        if (_order.IsTerminalState)
            throw new InvalidOperationException($"Cannot reject terminal order: {_order.Status}");

        _order.Reject(reason, details);
    }

    /// <summary>
    /// Mark order as unknown (ambiguous broker response).
    /// </summary>
    public void MarkUnknown()
    {
        if (_order.IsTerminalState)
            throw new InvalidOperationException($"Cannot mark terminal order as unknown: {_order.Status}");

        _order.MarkUnknown();
    }

    /// <summary>
    /// Get valid next states from current state.
    /// </summary>
    public IReadOnlyList<OrderStatus> GetValidNextStates()
    {
        return _order.Status switch
        {
            OrderStatus.Pending => new[] { OrderStatus.Submitted, OrderStatus.Rejected },
            OrderStatus.Submitted => new[] { OrderStatus.PartiallyFilled, OrderStatus.Filled, OrderStatus.Cancelled, OrderStatus.Unknown },
            OrderStatus.PartiallyFilled => new[] { OrderStatus.Filled, OrderStatus.Cancelled, OrderStatus.Unknown },
            OrderStatus.Filled => Array.Empty<OrderStatus>(),
            OrderStatus.Cancelled => Array.Empty<OrderStatus>(),
            OrderStatus.Rejected => Array.Empty<OrderStatus>(),
            OrderStatus.Unknown => new[] { OrderStatus.Filled, OrderStatus.Cancelled, OrderStatus.Rejected },
            _ => Array.Empty<OrderStatus>()
        };
    }

    /// <summary>
    /// Check if a transition to the target status is valid.
    /// </summary>
    public bool CanTransitionTo(OrderStatus targetStatus)
    {
        return GetValidNextStates().Contains(targetStatus);
    }
}
