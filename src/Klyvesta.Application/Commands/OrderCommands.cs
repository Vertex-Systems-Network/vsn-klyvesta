using Klyvesta.Domain.ValueObjects;

namespace Klyvesta.Application.Commands;

/// <summary>
/// Command to submit a new order intent from a customer.
/// </summary>
public sealed record SubmitOrderIntentCommand(
    Guid CustomerId,
    AccountId AccountId,
    Symbol Symbol,
    OrderSide Side,
    OrderType Type,
    Quantity Quantity,
    Money? LimitPrice,
    TimeInForce TimeInForce,
    string? IdempotencyKey = null,
    bool IsAutoOrder = false
);

/// <summary>
/// Command to cancel an existing order.
/// </summary>
public sealed record CancelOrderCommand(
    Guid CustomerId,
    OrderId OrderId,
    string? IdempotencyKey = null
);

/// <summary>
/// Command to query the status of an order.
/// </summary>
public sealed record QueryOrderStatusCommand(
    Guid CustomerId,
    OrderId OrderId
);

/// <summary>
/// Command to apply an execution (fill) to positions and ledger.
/// Internal command triggered by broker events.
/// </summary>
public sealed record ApplyExecutionCommand(
    Execution Execution,
    OrderSide Side,
    Guid CustomerId
);
