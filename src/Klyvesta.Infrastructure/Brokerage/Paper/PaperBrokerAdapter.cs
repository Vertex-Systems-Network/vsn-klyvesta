using Klyvesta.Application.Brokerage;

namespace Klyvesta.Infrastructure.Brokerage.Paper;

public sealed class PaperBrokerAdapter : IBrokerAdapter
{
    private const string BrokerCode = "KLYVESTA_PAPER";
    private const string BrokerVersion = "1";

    private readonly object _sync = new();
    private readonly PaperBrokerProfile _profile;
    private readonly Dictionary<Guid, PaperOrder> _orders = [];
    private readonly Dictionary<string, IdempotentSubmission> _idempotency = new(StringComparer.Ordinal);
    private readonly HashSet<string> _eventIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _executionIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, decimal> _cashByAccount = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Account, string Instrument), decimal> _positions = [];
    private long _requestSequence;
    private long _executionSequence;

    public PaperBrokerAdapter(PaperBrokerProfile? profile = null)
    {
        _profile = profile ?? new PaperBrokerProfile();
        ValidateProfile(_profile);
    }

    public int AcceptedOrderCount
    {
        get
        {
            lock (_sync)
            {
                return _orders.Count;
            }
        }
    }

    public Task<BrokerOperationResult<BrokerCapabilities>> GetCapabilitiesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        BrokerCapabilities capabilities = new(
            BrokerCode,
            BrokerVersion,
            BrokerEnvironment.Paper,
            new HashSet<BrokerOrderType> { BrokerOrderType.Market, BrokerOrderType.Limit },
            new HashSet<BrokerTimeInForce> { BrokerTimeInForce.Day },
            SupportsCancel: true,
            SupportsClientIdempotency: true,
            SupportsExecutionIdentifiers: true,
            SupportsBalances: true,
            SupportsPositions: true,
            SupportsMarketData: false,
            SupportsFunding: false,
            SupportsWithdrawals: false,
            SupportsStatements: false);

        return Task.FromResult(NewResult("GetCapabilities", BrokerResultState.Success, capabilities));
    }

    public Task<BrokerOperationResult<BrokerHealthSnapshot>> GetHealthAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var status = !_profile.IsAvailable
            ? "UNAVAILABLE"
            : _profile.IsRateLimited
                ? "RATE_LIMITED"
                : "AVAILABLE";

        BrokerHealthSnapshot health = new(_profile.IsAvailable, _profile.IsRateLimited, status);
        return Task.FromResult(NewResult("GetHealth", BrokerResultState.Success, health));
    }

    public Task<BrokerOperationResult<IReadOnlyList<BrokerCashBalance>>> GetBalancesAsync(
        string accountReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(accountReference))
        {
            return Task.FromResult(NewResult<IReadOnlyList<BrokerCashBalance>>(
                "GetBalances",
                BrokerResultState.Rejected,
                null,
                "INVALID_ACCOUNT_REFERENCE"));
        }

        lock (_sync)
        {
            EnsureAccount(accountReference);
            var observedAt = NextObservedAt();
            IReadOnlyList<BrokerCashBalance> balances =
            [
                new BrokerCashBalance(
                    accountReference,
                    "PKR",
                    _cashByAccount[accountReference],
                    _cashByAccount[accountReference],
                    observedAt),
            ];

            return Task.FromResult(NewResult(
                "GetBalances",
                BrokerResultState.Success,
                balances,
                observedAt: observedAt));
        }
    }

    public Task<BrokerOperationResult<IReadOnlyList<BrokerPosition>>> GetPositionsAsync(
        string accountReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(accountReference))
        {
            return Task.FromResult(NewResult<IReadOnlyList<BrokerPosition>>(
                "GetPositions",
                BrokerResultState.Rejected,
                null,
                "INVALID_ACCOUNT_REFERENCE"));
        }

        lock (_sync)
        {
            EnsureAccount(accountReference);
            var observedAt = NextObservedAt();
            IReadOnlyList<BrokerPosition> positions = _positions
                .Where(pair => pair.Key.Account == accountReference && pair.Value != 0m)
                .OrderBy(pair => pair.Key.Instrument, StringComparer.Ordinal)
                .Select(pair => new BrokerPosition(
                    accountReference,
                    pair.Key.Instrument,
                    pair.Value,
                    observedAt))
                .ToArray();

            return Task.FromResult(NewResult(
                "GetPositions",
                BrokerResultState.Success,
                positions,
                observedAt: observedAt));
        }
    }

    public Task<BrokerOperationResult<BrokerOrderSnapshot>> SubmitOrderAsync(
        SubmitBrokerOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validationError = ValidateCommand(command);
        if (validationError is not null)
        {
            return Task.FromResult(NewResult<BrokerOrderSnapshot>(
                "SubmitOrder",
                BrokerResultState.Rejected,
                null,
                validationError));
        }

        lock (_sync)
        {
            var fingerprint = CommandFingerprint.From(command);
            if (_idempotency.TryGetValue(command.IdempotencyKey, out var existing))
            {
                if (existing.Fingerprint != fingerprint)
                {
                    return Task.FromResult(NewResult<BrokerOrderSnapshot>(
                        "SubmitOrder",
                        BrokerResultState.Rejected,
                        null,
                        "IDEMPOTENCY_CONFLICT"));
                }

                return Task.FromResult(existing.Result);
            }

            if (!_profile.MarketOpen)
            {
                return Task.FromResult(NewResult<BrokerOrderSnapshot>(
                    "SubmitOrder",
                    BrokerResultState.Rejected,
                    null,
                    "MARKET_CLOSED"));
            }

            if (!_profile.IsAvailable)
            {
                return Task.FromResult(NewResult<BrokerOrderSnapshot>(
                    "SubmitOrder",
                    BrokerResultState.RetryableFailure,
                    null,
                    "BROKER_UNAVAILABLE"));
            }

            if (_profile.IsRateLimited)
            {
                return Task.FromResult(NewResult<BrokerOrderSnapshot>(
                    "SubmitOrder",
                    BrokerResultState.RetryableFailure,
                    null,
                    "RATE_LIMITED"));
            }

            if (_profile.TimeoutBeforeSideEffect)
            {
                return Task.FromResult(NewResult<BrokerOrderSnapshot>(
                    "SubmitOrder",
                    BrokerResultState.RetryableFailure,
                    null,
                    "TIMEOUT_BEFORE_SIDE_EFFECT"));
            }

            EnsureAccount(command.AccountReference);

            var externalOrderId = $"paper-order-{command.BrokerOrderId:N}";
            var order = new PaperOrder(command, externalOrderId);
            _orders.Add(command.BrokerOrderId, order);

            BrokerOperationResult<BrokerOrderSnapshot> result;
            if (_profile.SubmissionOutcome == PaperSubmissionOutcome.Rejected)
            {
                order.State = BrokerOrderState.Rejected;
                order.ReasonCode = "PAPER_REJECTION";
                result = NewResult(
                    "SubmitOrder",
                    BrokerResultState.Rejected,
                    order.ToSnapshot(),
                    order.ReasonCode,
                    externalOrderId);
            }
            else
            {
                var plannedFillQuantity = GetPlannedFillQuantity(command.Quantity);
                var plannedFillPrice = GetFillPrice(command);
                var capacityError = ValidatePaperCapacity(command, plannedFillQuantity, plannedFillPrice);

                if (capacityError is not null)
                {
                    order.State = BrokerOrderState.Rejected;
                    order.ReasonCode = capacityError;
                    result = NewResult(
                        "SubmitOrder",
                        BrokerResultState.Rejected,
                        order.ToSnapshot(),
                        capacityError,
                        externalOrderId);
                }
                else
                {
                    ApplyConfiguredSubmissionOutcome(order, plannedFillPrice);

                    if (_profile.AmbiguousTimeoutAfterAcceptance)
                    {
                        result = NewResult<BrokerOrderSnapshot>(
                            "SubmitOrder",
                            BrokerResultState.Unknown,
                            null,
                            "RESPONSE_LOST_AFTER_ACCEPT",
                            externalOrderId);
                    }
                    else
                    {
                        result = NewResult(
                            "SubmitOrder",
                            BrokerResultState.Success,
                            order.ToSnapshot(),
                            externalCorrelationId: externalOrderId);
                    }
                }
            }

            _idempotency.Add(command.IdempotencyKey, new IdempotentSubmission(fingerprint, result));
            return Task.FromResult(result);
        }
    }

    public Task<BrokerOperationResult<BrokerOrderSnapshot>> GetOrderAsync(
        Guid brokerOrderId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (!_orders.TryGetValue(brokerOrderId, out var order))
            {
                return Task.FromResult(NewResult<BrokerOrderSnapshot>(
                    "GetOrder",
                    BrokerResultState.Rejected,
                    null,
                    "ORDER_NOT_FOUND"));
            }

            return Task.FromResult(NewResult(
                "GetOrder",
                BrokerResultState.Success,
                order.ToSnapshot(),
                externalCorrelationId: order.ExternalOrderId));
        }
    }

    public Task<BrokerOperationResult<BrokerOrderSnapshot>> CancelOrderAsync(
        Guid brokerOrderId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (!_orders.TryGetValue(brokerOrderId, out var order))
            {
                return Task.FromResult(NewResult<BrokerOrderSnapshot>(
                    "CancelOrder",
                    BrokerResultState.Rejected,
                    null,
                    "ORDER_NOT_FOUND"));
            }

            if (!_profile.IsAvailable || _profile.IsRateLimited)
            {
                return Task.FromResult(NewResult<BrokerOrderSnapshot>(
                    "CancelOrder",
                    BrokerResultState.RetryableFailure,
                    order.ToSnapshot(),
                    !_profile.IsAvailable ? "BROKER_UNAVAILABLE" : "RATE_LIMITED",
                    order.ExternalOrderId));
            }

            if (IsTerminal(order.State))
            {
                return Task.FromResult(NewResult(
                    "CancelOrder",
                    BrokerResultState.Rejected,
                    order.ToSnapshot(),
                    "ORDER_NOT_CANCELLABLE",
                    order.ExternalOrderId));
            }

            order.State = BrokerOrderState.Cancelled;
            return Task.FromResult(NewResult(
                "CancelOrder",
                BrokerResultState.Success,
                order.ToSnapshot(),
                externalCorrelationId: order.ExternalOrderId));
        }
    }

    public PaperBrokerEventResult ApplyPaperEvent(PaperBrokerEvent paperEvent)
    {
        ArgumentNullException.ThrowIfNull(paperEvent);

        if (string.IsNullOrWhiteSpace(paperEvent.EventId))
        {
            throw new ArgumentException("Paper event id is required.", nameof(paperEvent));
        }

        lock (_sync)
        {
            if (!_orders.TryGetValue(paperEvent.BrokerOrderId, out var order))
            {
                throw new InvalidOperationException($"Paper order {paperEvent.BrokerOrderId} does not exist.");
            }

            if (!_eventIds.Add(paperEvent.EventId))
            {
                return new PaperBrokerEventResult(
                    DuplicateEvent: true,
                    DuplicateExecution: false,
                    order.ToSnapshot());
            }

            var duplicateExecution = false;
            if (paperEvent.Execution is not null)
            {
                duplicateExecution = !ApplyExecution(order, paperEvent.Execution);
            }

            TryAdvanceState(order, paperEvent.State);
            return new PaperBrokerEventResult(
                DuplicateEvent: false,
                DuplicateExecution: duplicateExecution,
                order.ToSnapshot());
        }
    }

    private void ApplyConfiguredSubmissionOutcome(PaperOrder order, decimal fillPrice)
    {
        switch (_profile.SubmissionOutcome)
        {
            case PaperSubmissionOutcome.Open:
                order.State = BrokerOrderState.Open;
                break;

            case PaperSubmissionOutcome.FullFill:
                ApplyGeneratedFills(order, order.Command.Quantity, fillPrice, _profile.FillCount);
                order.State = BrokerOrderState.Filled;
                break;

            case PaperSubmissionOutcome.PartialFill:
                var partialQuantity = order.Command.Quantity * _profile.PartialFillRatio;
                ApplyGeneratedFills(order, partialQuantity, fillPrice, _profile.FillCount);
                order.State = BrokerOrderState.PartiallyFilled;
                break;

            case PaperSubmissionOutcome.Rejected:
                throw new InvalidOperationException("Rejected submissions are handled before configured outcome application.");

            default:
                throw new InvalidOperationException($"Unsupported paper submission outcome {_profile.SubmissionOutcome}.");
        }
    }

    private void ApplyGeneratedFills(PaperOrder order, decimal totalQuantity, decimal price, int fillCount)
    {
        var remaining = totalQuantity;

        for (var index = 1; index <= fillCount; index++)
        {
            var quantity = index == fillCount ? remaining : totalQuantity / fillCount;
            remaining -= quantity;

            var executionSequence = Interlocked.Increment(ref _executionSequence);
            BrokerExecution execution = new(
                $"paper-exec-{order.Command.BrokerOrderId:N}-{index:D2}",
                order.Command.BrokerOrderId,
                order.Command.InstrumentReference,
                quantity,
                price,
                _profile.StartTime.AddSeconds(executionSequence));

            if (!ApplyExecution(order, execution))
            {
                throw new InvalidOperationException($"Generated execution id {execution.ExecutionId} was unexpectedly duplicated.");
            }
        }
    }

    private bool ApplyExecution(PaperOrder order, BrokerExecution execution)
    {
        if (execution.BrokerOrderId != order.Command.BrokerOrderId)
        {
            throw new InvalidOperationException("Execution order id does not match the target paper order.");
        }

        if (!StringComparer.Ordinal.Equals(execution.InstrumentReference, order.Command.InstrumentReference))
        {
            throw new InvalidOperationException("Execution instrument does not match the target paper order.");
        }

        if (execution.Quantity <= 0m || execution.Price <= 0m)
        {
            throw new InvalidOperationException("Paper execution quantity and price must be positive exact decimals.");
        }

        if (!_executionIds.Add(execution.ExecutionId))
        {
            return false;
        }

        if (order.FilledQuantity + execution.Quantity > order.Command.Quantity)
        {
            _executionIds.Remove(execution.ExecutionId);
            throw new InvalidOperationException("Paper execution would exceed requested order quantity.");
        }

        EnsureAccount(order.Command.AccountReference);
        var notional = execution.Quantity * execution.Price;
        var positionKey = (order.Command.AccountReference, order.Command.InstrumentReference);
        _positions.TryGetValue(positionKey, out var currentPosition);

        if (order.Command.Side == BrokerOrderSide.Buy)
        {
            if (_cashByAccount[order.Command.AccountReference] < notional)
            {
                _executionIds.Remove(execution.ExecutionId);
                throw new InvalidOperationException("Paper execution exceeds available paper cash.");
            }

            _cashByAccount[order.Command.AccountReference] -= notional;
            _positions[positionKey] = currentPosition + execution.Quantity;
        }
        else
        {
            if (currentPosition < execution.Quantity)
            {
                _executionIds.Remove(execution.ExecutionId);
                throw new InvalidOperationException("Paper execution exceeds available paper position.");
            }

            _positions[positionKey] = currentPosition - execution.Quantity;
            _cashByAccount[order.Command.AccountReference] += notional;
        }

        order.Executions.Add(execution);
        return true;
    }

    private void TryAdvanceState(PaperOrder order, BrokerOrderState targetState)
    {
        if (IsTerminal(order.State))
        {
            return;
        }

        if (targetState == BrokerOrderState.Filled && order.FilledQuantity != order.Command.Quantity)
        {
            return;
        }

        if (targetState == BrokerOrderState.PartiallyFilled &&
            (order.FilledQuantity <= 0m || order.FilledQuantity >= order.Command.Quantity))
        {
            return;
        }

        if (targetState == BrokerOrderState.Rejected && order.FilledQuantity > 0m)
        {
            return;
        }

        if (StateRank(targetState) >= StateRank(order.State))
        {
            order.State = targetState;
        }
    }

    private string? ValidatePaperCapacity(
        SubmitBrokerOrderCommand command,
        decimal plannedFillQuantity,
        decimal fillPrice)
    {
        if (plannedFillQuantity <= 0m)
        {
            return null;
        }

        if (command.Side == BrokerOrderSide.Buy)
        {
            var requiredCash = plannedFillQuantity * fillPrice;
            return _cashByAccount[command.AccountReference] >= requiredCash
                ? null
                : "INSUFFICIENT_PAPER_CASH";
        }

        _positions.TryGetValue((command.AccountReference, command.InstrumentReference), out var currentPosition);
        return currentPosition >= plannedFillQuantity
            ? null
            : "INSUFFICIENT_PAPER_POSITION";
    }

    private decimal GetPlannedFillQuantity(decimal requestedQuantity) => _profile.SubmissionOutcome switch
    {
        PaperSubmissionOutcome.FullFill => requestedQuantity,
        PaperSubmissionOutcome.PartialFill => requestedQuantity * _profile.PartialFillRatio,
        _ => 0m,
    };

    private decimal GetFillPrice(SubmitBrokerOrderCommand command) =>
        command.OrderType == BrokerOrderType.Limit
            ? command.LimitPrice!.Value
            : _profile.FillPrice;

    private void EnsureAccount(string accountReference)
    {
        if (!_cashByAccount.ContainsKey(accountReference))
        {
            _cashByAccount.Add(accountReference, _profile.StartingCash);
        }
    }

    private BrokerOperationResult<T> NewResult<T>(
        string operation,
        BrokerResultState state,
        T? value,
        string? reasonCode = null,
        string? externalCorrelationId = null,
        DateTimeOffset? observedAt = null)
    {
        var sequence = Interlocked.Increment(ref _requestSequence);
        var timestamp = observedAt ?? _profile.StartTime.AddTicks(sequence);

        return new BrokerOperationResult<T>(
            $"paper-request-{sequence:D6}",
            BrokerEnvironment.Paper,
            operation,
            timestamp,
            state,
            value,
            reasonCode,
            externalCorrelationId);
    }

    private DateTimeOffset NextObservedAt()
    {
        var sequence = Interlocked.Increment(ref _requestSequence);
        return _profile.StartTime.AddTicks(sequence);
    }

    private static string? ValidateCommand(SubmitBrokerOrderCommand command)
    {
        if (command.BrokerOrderId == Guid.Empty || command.OrderIntentId == Guid.Empty)
        {
            return "INVALID_ORDER_REFERENCE";
        }

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) ||
            string.IsNullOrWhiteSpace(command.AccountReference) ||
            string.IsNullOrWhiteSpace(command.InstrumentReference))
        {
            return "INVALID_COMMAND_REFERENCE";
        }

        if (command.Quantity <= 0m)
        {
            return "INVALID_QUANTITY";
        }

        if (command.OrderType == BrokerOrderType.Limit &&
            (command.LimitPrice is null || command.LimitPrice <= 0m))
        {
            return "INVALID_LIMIT_PRICE";
        }

        if (command.OrderType == BrokerOrderType.Market && command.LimitPrice is not null)
        {
            return "MARKET_ORDER_MUST_NOT_HAVE_LIMIT_PRICE";
        }

        return null;
    }

    private static void ValidateProfile(PaperBrokerProfile profile)
    {
        if (profile.FillPrice <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(profile), "Paper fill price must be positive.");
        }

        if (profile.PartialFillRatio <= 0m || profile.PartialFillRatio >= 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(profile), "Partial fill ratio must be greater than zero and less than one.");
        }

        if (profile.FillCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(profile), "Paper fill count must be positive.");
        }

        if (profile.StartingCash < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(profile), "Starting paper cash cannot be negative.");
        }
    }

    private static bool IsTerminal(BrokerOrderState state) => state is
        BrokerOrderState.Filled or
        BrokerOrderState.Cancelled or
        BrokerOrderState.Rejected;

    private static int StateRank(BrokerOrderState state) => state switch
    {
        BrokerOrderState.PendingSubmit => 0,
        BrokerOrderState.Submitted => 1,
        BrokerOrderState.Open => 2,
        BrokerOrderState.PartiallyFilled => 3,
        BrokerOrderState.CancelPending => 4,
        BrokerOrderState.Filled or BrokerOrderState.Cancelled or BrokerOrderState.Rejected => 5,
        BrokerOrderState.Unknown => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown broker order state."),
    };

    private sealed class PaperOrder(SubmitBrokerOrderCommand command, string externalOrderId)
    {
        public SubmitBrokerOrderCommand Command { get; } = command;

        public string ExternalOrderId { get; } = externalOrderId;

        public BrokerOrderState State { get; set; } = BrokerOrderState.Submitted;

        public string? ReasonCode { get; set; }

        public List<BrokerExecution> Executions { get; } = [];

        public decimal FilledQuantity => Executions.Sum(execution => execution.Quantity);

        public BrokerOrderSnapshot ToSnapshot() => new(
            Command.BrokerOrderId,
            ExternalOrderId,
            State,
            Command.Quantity,
            FilledQuantity,
            Executions.ToArray(),
            ReasonCode);
    }

    private sealed record IdempotentSubmission(
        CommandFingerprint Fingerprint,
        BrokerOperationResult<BrokerOrderSnapshot> Result);

    private sealed record CommandFingerprint(
        Guid BrokerOrderId,
        Guid OrderIntentId,
        string AccountReference,
        string InstrumentReference,
        BrokerOrderSide Side,
        BrokerOrderType OrderType,
        decimal Quantity,
        decimal? LimitPrice,
        BrokerTimeInForce TimeInForce)
    {
        public static CommandFingerprint From(SubmitBrokerOrderCommand command) => new(
            command.BrokerOrderId,
            command.OrderIntentId,
            command.AccountReference,
            command.InstrumentReference,
            command.Side,
            command.OrderType,
            command.Quantity,
            command.LimitPrice,
            command.TimeInForce);
    }
}
