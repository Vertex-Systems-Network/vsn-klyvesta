using Klyvesta.Application.Orders;

namespace Klyvesta.Application.Brokerage;

public sealed class ExecutionHoldBrokerAdapter : IBrokerAdapter
{
    public const string HoldReasonCode = "EXECUTION_BLOCKED_BY_PORTFOLIO_HOLD";

    private readonly IBrokerAdapter _inner;
    private readonly IOrderExecutionHoldProvider _holdProvider;
    private readonly Func<DateTimeOffset> _clock;
    private long _requestSequence;

    public ExecutionHoldBrokerAdapter(
        IBrokerAdapter inner,
        IOrderExecutionHoldProvider holdProvider,
        Func<DateTimeOffset>? clock = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _holdProvider = holdProvider ?? throw new ArgumentNullException(nameof(holdProvider));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public Task<BrokerOperationResult<BrokerCapabilities>> GetCapabilitiesAsync(
        CancellationToken cancellationToken = default) =>
        _inner.GetCapabilitiesAsync(cancellationToken);

    public Task<BrokerOperationResult<BrokerHealthSnapshot>> GetHealthAsync(
        CancellationToken cancellationToken = default) =>
        _inner.GetHealthAsync(cancellationToken);

    public Task<BrokerOperationResult<IReadOnlyList<BrokerCashBalance>>> GetBalancesAsync(
        string accountReference,
        CancellationToken cancellationToken = default) =>
        _inner.GetBalancesAsync(accountReference, cancellationToken);

    public Task<BrokerOperationResult<IReadOnlyList<BrokerPosition>>> GetPositionsAsync(
        string accountReference,
        CancellationToken cancellationToken = default) =>
        _inner.GetPositionsAsync(accountReference, cancellationToken);

    public Task<BrokerOperationResult<BrokerOrderSnapshot>> SubmitOrderAsync(
        SubmitBrokerOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var hold = _holdProvider.GetHold(command.AccountReference);
        if (!hold.IsHeld)
        {
            return _inner.SubmitOrderAsync(command, cancellationToken);
        }

        var requestSequence = Interlocked.Increment(ref _requestSequence);
        return Task.FromResult(new BrokerOperationResult<BrokerOrderSnapshot>(
            RequestId: $"execution-hold-{requestSequence:D8}",
            Environment: BrokerEnvironment.Paper,
            Operation: "SubmitOrder",
            ObservedAt: _clock(),
            State: BrokerResultState.RetryableFailure,
            Value: null,
            ReasonCode: hold.ReasonCode ?? HoldReasonCode,
            ExternalCorrelationId: null));
    }

    public Task<BrokerOperationResult<BrokerOrderSnapshot>> GetOrderAsync(
        Guid brokerOrderId,
        CancellationToken cancellationToken = default) =>
        _inner.GetOrderAsync(brokerOrderId, cancellationToken);

    public Task<BrokerOperationResult<BrokerOrderSnapshot>> CancelOrderAsync(
        Guid brokerOrderId,
        CancellationToken cancellationToken = default) =>
        _inner.CancelOrderAsync(brokerOrderId, cancellationToken);
}
