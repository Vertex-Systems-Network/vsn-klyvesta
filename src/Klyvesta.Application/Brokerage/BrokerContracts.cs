namespace Klyvesta.Application.Brokerage;

public enum BrokerResultState
{
    Success,
    Rejected,
    RetryableFailure,
    Unknown,
}

public enum BrokerEnvironment
{
    Paper,
    Sandbox,
    Production,
}

public enum BrokerOrderState
{
    PendingSubmit,
    Submitted,
    Open,
    PartiallyFilled,
    Filled,
    CancelPending,
    Cancelled,
    Rejected,
    Unknown,
}

public enum BrokerOrderSide
{
    Buy,
    Sell,
}

public enum BrokerOrderType
{
    Market,
    Limit,
}

public enum BrokerTimeInForce
{
    Day,
}

public sealed record BrokerCapabilities(
    string BrokerCode,
    string Version,
    BrokerEnvironment Environment,
    IReadOnlySet<BrokerOrderType> SupportedOrderTypes,
    IReadOnlySet<BrokerTimeInForce> SupportedTimeInForce,
    bool SupportsCancel,
    bool SupportsClientIdempotency,
    bool SupportsExecutionIdentifiers,
    bool SupportsBalances,
    bool SupportsPositions,
    bool SupportsMarketData,
    bool SupportsFunding,
    bool SupportsWithdrawals,
    bool SupportsStatements);

public sealed record BrokerHealthSnapshot(
    bool IsAvailable,
    bool IsRateLimited,
    string Status);

public sealed record BrokerOperationResult<T>(
    string RequestId,
    BrokerEnvironment Environment,
    string Operation,
    DateTimeOffset ObservedAt,
    BrokerResultState State,
    T? Value,
    string? ReasonCode = null,
    string? ExternalCorrelationId = null);

public sealed record SubmitBrokerOrderCommand(
    Guid BrokerOrderId,
    Guid OrderIntentId,
    string IdempotencyKey,
    string AccountReference,
    string InstrumentReference,
    BrokerOrderSide Side,
    BrokerOrderType OrderType,
    decimal Quantity,
    decimal? LimitPrice,
    BrokerTimeInForce TimeInForce);

public sealed record BrokerExecution(
    string ExecutionId,
    Guid BrokerOrderId,
    string InstrumentReference,
    decimal Quantity,
    decimal Price,
    DateTimeOffset TradeAt);

public sealed record BrokerOrderSnapshot(
    Guid BrokerOrderId,
    string ExternalOrderId,
    BrokerOrderState State,
    decimal RequestedQuantity,
    decimal FilledQuantity,
    IReadOnlyList<BrokerExecution> Executions,
    string? ReasonCode = null);

public sealed record BrokerCashBalance(
    string AccountReference,
    string Currency,
    decimal Cash,
    decimal AvailableCash,
    DateTimeOffset ObservedAt);

public sealed record BrokerPosition(
    string AccountReference,
    string InstrumentReference,
    decimal Quantity,
    DateTimeOffset ObservedAt);

public interface IBrokerAdapter
{
    Task<BrokerOperationResult<BrokerCapabilities>> GetCapabilitiesAsync(
        CancellationToken cancellationToken = default);

    Task<BrokerOperationResult<BrokerHealthSnapshot>> GetHealthAsync(
        CancellationToken cancellationToken = default);

    Task<BrokerOperationResult<IReadOnlyList<BrokerCashBalance>>> GetBalancesAsync(
        string accountReference,
        CancellationToken cancellationToken = default);

    Task<BrokerOperationResult<IReadOnlyList<BrokerPosition>>> GetPositionsAsync(
        string accountReference,
        CancellationToken cancellationToken = default);

    Task<BrokerOperationResult<BrokerOrderSnapshot>> SubmitOrderAsync(
        SubmitBrokerOrderCommand command,
        CancellationToken cancellationToken = default);

    Task<BrokerOperationResult<BrokerOrderSnapshot>> GetOrderAsync(
        Guid brokerOrderId,
        CancellationToken cancellationToken = default);

    Task<BrokerOperationResult<BrokerOrderSnapshot>> CancelOrderAsync(
        Guid brokerOrderId,
        CancellationToken cancellationToken = default);
}
