namespace Klyvesta.Domain.Broker;

using Klyvesta.Domain.Common;

/// <summary>
/// Capabilities exposed by a broker adapter.
/// All capabilities must be explicitly declared - unsupported features are UNSUPPORTED, never simulated.
/// </summary>
public sealed record BrokerCapabilities(
    string BrokerCode,
    string BrokerVersion,
    bool SupportsAccountOpening,
    IReadOnlyList<string> SupportedOrderTypes,
    IReadOnlyList<string> SupportedTimeInForce,
    bool SupportsModifyOrder,
    bool SupportsCancelOrder,
    bool SupportsIdempotencyKey,
    bool SupportsExecutionIdentifiers,
    string EventDeliveryMechanism, // "Webhook" | "Streaming" | "Polling" | "None"
    bool SupportsBalanceQuery,
    bool SupportsPositionQuery,
    bool SupportsFundingDeposit,
    bool SupportsFundingWithdrawal,
    bool SupportsStatements,
    bool SupportsMarketData,
    bool HasMarketDataFreshnessMetadata,
    string Environment, // "Sandbox" | "Production"
    int? RateLimitPerSecond,
    IReadOnlyDictionary<string, string> AdditionalCapabilities
);

/// <summary>
/// Normalized result envelope from broker operations.
/// </summary>
public sealed record BrokerResultEnvelope(
    Guid RequestId,
    string BrokerCode,
    string Environment,
    string Operation,
    DateTime ObservedAtUtc,
    DateTime? BrokerTimestampUtc,
    string? ExternalCorrelationId,
    BrokerResultState ResultState,
    string? ReasonCode,
    string? RawEvidenceRef
)
{
    public bool IsSuccess => ResultState == BrokerResultState.Success;
    public bool IsUnknown => ResultState == BrokerResultState.Unknown;
    public bool IsRetryable => ResultState == BrokerResultState.RetryableFailure;
}

/// <summary>
/// Normalized cash balance from broker.
/// </summary>
public sealed record CashBalance(
    string Currency,
    decimal Total,
    decimal? Available,
    decimal? Reserved,
    decimal? Unsettled,
    DateTime ObservedAtUtc,
    string SourceReference
);

/// <summary>
/// Normalized position from broker.
/// </summary>
public sealed record BrokerPosition(
    InstrumentId InstrumentId,
    string ExternalInstrumentId,
    decimal Quantity,
    decimal? SettledQuantity,
    decimal? UnsettledQuantity,
    decimal? CostBasis,
    string? CostBasisCurrency,
    DateTime ObservedAtUtc,
    string SourceReference
);

/// <summary>
/// Order submission request normalized for any broker.
/// </summary>
public sealed record BrokerOrderRequest(
    OrderId InternalOrderId,
    OrderIntentId OrderIntentId,
    string IdempotencyKey,
    AccountId AccountId,
    InstrumentId InstrumentId,
    string ExternalInstrumentId,
    Side Side,
    string OrderType, // "MARKET" | "LIMIT" | etc.
    decimal Quantity,
    decimal? LimitPrice,
    TimeInForce TimeInForce,
    CorrelationId CorrelationId
);

/// <summary>
/// Normalized execution/fill from broker.
/// </summary>
public sealed record BrokerExecution(
    string ExternalExecutionId,
    OrderId InternalOrderId,
    string ExternalOrderId,
    InstrumentId InstrumentId,
    Side Side,
    decimal Quantity,
    decimal Price,
    decimal? FeeAmount,
    string? FeeCurrency,
    decimal? TaxAmount,
    string? TaxCurrency,
    DateTime TradeTimestampUtc,
    DateTime? SettlementDateUtc,
    DateTime ObservedAtUtc
);

/// <summary>
/// Interface for broker adapters - the boundary between Klyvesta and external brokers.
/// </summary>
public interface IBrokerAdapter
{
    /// <summary>
    /// Returns the capabilities of this broker adapter.
    /// </summary>
    Task<BrokerCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets normalized account status.
    /// </summary>
    Task<AccountStatus> GetAccountStatusAsync(AccountId accountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets cash balances for an account.
    /// </summary>
    Task<IReadOnlyList<CashBalance>> GetBalancesAsync(AccountId accountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets positions for an account.
    /// </summary>
    Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync(AccountId accountId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Submits an order to the broker.
    /// Returns UNKNOWN if timeout occurs after possible side effect - caller must reconcile.
    /// </summary>
    Task<BrokerResultEnvelope> SubmitOrderAsync(
        BrokerOrderRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Queries order status by strongest available reference.
    /// </summary>
    Task<(OrderState State, BrokerResultEnvelope Envelope)> GetOrderStatusAsync(
        OrderId orderId,
        string? externalOrderId = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cancels an order. May return UNKNOWN if acknowledgment is ambiguous.
    /// </summary>
    Task<BrokerResultEnvelope> CancelOrderAsync(
        OrderId orderId,
        string? externalOrderId = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets executions/fills for an order or account.
    /// </summary>
    Task<IReadOnlyList<BrokerExecution>> GetExecutionsAsync(
        AccountId accountId,
        OrderId? orderId = null,
        DateTime? startTimeUtc = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Exception thrown when broker operation fails with known non-retryable error.
/// </summary>
public sealed class BrokerRejectedException : Exception
{
    public BrokerRejectedException(string reasonCode, string message, BrokerResultEnvelope? Envelope = null)
        : base(message)
    {
        ReasonCode = reasonCode;
        this.Envelope = Envelope;
    }
    
    public string ReasonCode { get; }
    public BrokerResultEnvelope? Envelope { get; }
}

/// <summary>
/// Exception thrown when broker operation fails but may be safely retried.
/// </summary>
public sealed class BrokerRetryableException : Exception
{
    public BrokerRetryableException(string reasonCode, string message, BrokerResultEnvelope? Envelope = null)
        : base(message)
    {
        ReasonCode = reasonCode;
        this.Envelope = Envelope;
    }
    
    public string ReasonCode { get; }
    public BrokerResultEnvelope? Envelope { get; }
}

/// <summary>
/// Exception thrown when broker response is ambiguous - financial side effect may have occurred.
/// Triggers reconciliation workflow rather than blind retry.
/// </summary>
public sealed class BrokerAmbiguousException : Exception
{
    public BrokerAmbiguousException(string message, BrokerResultEnvelope? Envelope = null)
        : base(message)
    {
        this.Envelope = Envelope;
    }
    
    public BrokerResultEnvelope? Envelope { get; }
}
