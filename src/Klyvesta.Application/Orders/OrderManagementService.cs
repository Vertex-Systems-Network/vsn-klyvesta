using Klyvesta.Application.Brokerage;
using Klyvesta.Domain.Orders;

namespace Klyvesta.Application.Orders;

public sealed record OmsOrderRequest(
    Guid OrderIntentId,
    Guid BrokerOrderId,
    string IdempotencyKey,
    string AccountReference,
    string InstrumentReference,
    BrokerOrderSide Side,
    BrokerOrderType OrderType,
    decimal Quantity,
    decimal? LimitPrice,
    BrokerTimeInForce TimeInForce);

public sealed record OmsReservationSpec(
    Guid ReservationId,
    ReservationKind Kind,
    decimal Amount);

public sealed record OmsSnapshot(
    Guid OrderIntentId,
    OrderIntentState IntentState,
    Guid? BrokerOrderId,
    ManagedBrokerOrderState? BrokerOrderState,
    decimal FilledQuantity,
    ReservationState? ReservationState,
    decimal ReservedAmount,
    decimal ConsumedReservationAmount,
    decimal ReleasedReservationAmount,
    decimal RemainingReservationAmount,
    bool ReconciliationRequired,
    string? ReasonCode);

public sealed record OmsReconciliationItem(
    Guid Id,
    Guid BrokerOrderId,
    string ReasonCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);

public sealed class OrderManagementService
{
    private readonly IBrokerAdapter _brokerAdapter;
    private readonly Func<DateTimeOffset> _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<Guid, OmsEntry> _entries = [];
    private readonly Dictionary<string, IntentFingerprint> _idempotency = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, MutableReconciliationItem> _reconciliationByBrokerOrder = [];

    public OrderManagementService(IBrokerAdapter brokerAdapter, Func<DateTimeOffset>? clock = null)
    {
        _brokerAdapter = brokerAdapter ?? throw new ArgumentNullException(nameof(brokerAdapter));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<OmsSnapshot> CreateIntentAsync(
        OmsOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var fingerprint = IntentFingerprint.From(request);
            if (_idempotency.TryGetValue(request.IdempotencyKey, out var existing))
            {
                if (existing != fingerprint)
                {
                    throw new InvalidOperationException("Order intent idempotency key conflicts with a different payload.");
                }

                return Snapshot(_entries[existing.OrderIntentId]);
            }

            if (_entries.ContainsKey(request.OrderIntentId))
            {
                throw new InvalidOperationException($"Order intent {request.OrderIntentId} already exists.");
            }

            OrderIntent intent = new(
                request.OrderIntentId,
                request.BrokerOrderId,
                request.IdempotencyKey,
                request.AccountReference,
                request.InstrumentReference,
                request.Quantity);

            var entry = new OmsEntry(request, intent);
            _entries.Add(request.OrderIntentId, entry);
            _idempotency.Add(request.IdempotencyKey, fingerprint);
            return Snapshot(entry);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OmsSnapshot> BeginValidationAsync(
        Guid orderIntentId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var entry = GetEntry(orderIntentId);
            entry.Intent.StartValidation();
            return Snapshot(entry);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OmsSnapshot> RecordValidationAsync(
        Guid orderIntentId,
        OrderValidationEvidence evidence,
        OmsReservationSpec? reservationSpec,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var entry = GetEntry(orderIntentId);
            OrderReservation? reservation = null;

            if (evidence.Approved)
            {
                if (reservationSpec is null)
                {
                    throw new InvalidOperationException("Approved validation requires a reservation specification.");
                }

                reservation = new OrderReservation(
                    reservationSpec.ReservationId,
                    reservationSpec.Kind,
                    reservationSpec.Amount);
            }
            else if (reservationSpec is not null)
            {
                throw new InvalidOperationException("Rejected validation cannot reserve financial capacity.");
            }

            entry.Intent.RecordValidation(evidence, reservation);
            return Snapshot(entry);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OmsSnapshot> QueueForExecutionAsync(
        Guid orderIntentId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var entry = GetEntry(orderIntentId);
            entry.Intent.QueueExecution();
            return Snapshot(entry);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OmsSnapshot> ExecuteAsync(
        Guid orderIntentId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var entry = GetEntry(orderIntentId);

            if (entry.Intent.State == OrderIntentState.Rejected)
            {
                return Snapshot(entry);
            }

            if (entry.BrokerOrder is null)
            {
                if (entry.Intent.State != OrderIntentState.ExecutionPending)
                {
                    throw new InvalidOperationException($"Order intent {orderIntentId} is not execution-pending.");
                }

                entry.BrokerOrder = new ManagedBrokerOrder(
                    entry.Request.BrokerOrderId,
                    entry.Request.OrderIntentId,
                    entry.Request.Quantity);
                entry.Intent.MarkExecutionCreated();
            }

            if (entry.BrokerOrder.State != ManagedBrokerOrderState.PendingSubmit)
            {
                return Snapshot(entry);
            }

            entry.BrokerOrder.BeginSubmission();
            BrokerOperationResult<BrokerOrderSnapshot> result;

            try
            {
                result = await _brokerAdapter.SubmitOrderAsync(
                    ToBrokerCommand(entry.Request),
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                entry.BrokerOrder.MarkUnknown("BROKER_CALL_EXCEPTION_AFTER_SUBMISSION_STARTED", externalOrderId: null);
                QueueReconciliation(entry.BrokerOrder.Id, "BROKER_CALL_EXCEPTION_AFTER_SUBMISSION_STARTED");
                throw;
            }

            ApplyBrokerResult(entry, result, operation: "SUBMIT");
            return Snapshot(entry);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OmsSnapshot> CancelAsync(
        Guid orderIntentId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var entry = GetEntry(orderIntentId);

            if (entry.BrokerOrder is null)
            {
                entry.Intent.CancelBeforeBrokerSubmission();
                return Snapshot(entry);
            }

            if (entry.BrokerOrder.State == ManagedBrokerOrderState.Unknown)
            {
                QueueReconciliation(entry.BrokerOrder.Id, "CANCEL_BLOCKED_BY_UNKNOWN_ORDER_STATE");
                return Snapshot(entry);
            }

            if (entry.BrokerOrder.State is ManagedBrokerOrderState.Filled or ManagedBrokerOrderState.Cancelled or ManagedBrokerOrderState.Rejected)
            {
                return Snapshot(entry);
            }

            if (entry.BrokerOrder.State != ManagedBrokerOrderState.CancelPending)
            {
                entry.BrokerOrder.BeginCancel();
            }

            BrokerOperationResult<BrokerOrderSnapshot> result;

            try
            {
                result = await _brokerAdapter.CancelOrderAsync(
                    entry.BrokerOrder.Id,
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                entry.BrokerOrder.MarkUnknown("BROKER_CANCEL_EXCEPTION", entry.BrokerOrder.ExternalOrderId);
                QueueReconciliation(entry.BrokerOrder.Id, "BROKER_CANCEL_EXCEPTION");
                throw;
            }

            ApplyBrokerResult(entry, result, operation: "CANCEL");
            return Snapshot(entry);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OmsSnapshot> RecoverUnknownAsync(
        Guid orderIntentId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var entry = GetEntry(orderIntentId);
            var brokerOrder = entry.BrokerOrder ?? throw new InvalidOperationException("Order intent has no broker order to reconcile.");

            if (brokerOrder.State != ManagedBrokerOrderState.Unknown)
            {
                return Snapshot(entry);
            }

            var result = await _brokerAdapter.GetOrderAsync(brokerOrder.Id, cancellationToken).ConfigureAwait(false);

            if (result.State == BrokerResultState.Success && result.Value is not null)
            {
                ApplyBrokerSnapshot(entry, result.Value, resolvingUnknown: true);
                ResolveReconciliationIfSafe(entry);
            }
            else
            {
                QueueReconciliation(brokerOrder.Id, result.ReasonCode ?? "UNKNOWN_RECOVERY_NOT_CONCLUSIVE");
            }

            return Snapshot(entry);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OmsSnapshot> ApplyBrokerSnapshotAsync(
        Guid orderIntentId,
        BrokerOrderSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var entry = GetEntry(orderIntentId);
            if (entry.BrokerOrder is null)
            {
                throw new InvalidOperationException("Order intent has no broker order for snapshot application.");
            }

            ApplyBrokerSnapshot(
                entry,
                snapshot,
                resolvingUnknown: entry.BrokerOrder.State == ManagedBrokerOrderState.Unknown);
            ResolveReconciliationIfSafe(entry);
            return Snapshot(entry);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OmsSnapshot> GetSnapshotAsync(
        Guid orderIntentId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return Snapshot(GetEntry(orderIntentId));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<OmsReconciliationItem>> GetOpenReconciliationAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return _reconciliationByBrokerOrder.Values
                .Where(item => item.ResolvedAt is null)
                .OrderBy(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .Select(item => item.ToSnapshot())
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    private void ApplyBrokerResult(
        OmsEntry entry,
        BrokerOperationResult<BrokerOrderSnapshot> result,
        string operation)
    {
        var brokerOrder = entry.BrokerOrder ?? throw new InvalidOperationException("Broker order is required.");

        switch (result.State)
        {
            case BrokerResultState.RetryableFailure:
                if (operation == "SUBMIT")
                {
                    brokerOrder.MarkRetryableBeforeSideEffect(result.ReasonCode ?? "SAFE_RETRYABLE_FAILURE");
                }
                else
                {
                    QueueReconciliation(brokerOrder.Id, result.ReasonCode ?? "CANCEL_RETRYABLE_FAILURE");
                }

                return;

            case BrokerResultState.Unknown:
                brokerOrder.MarkUnknown(
                    result.ReasonCode ?? $"{operation}_UNKNOWN",
                    result.ExternalCorrelationId);
                QueueReconciliation(brokerOrder.Id, result.ReasonCode ?? $"{operation}_UNKNOWN");
                return;

            case BrokerResultState.Rejected:
                if (result.Value is not null)
                {
                    ApplyBrokerSnapshot(entry, result.Value, resolvingUnknown: false);
                }
                else if (operation == "SUBMIT")
                {
                    brokerOrder.ObserveState(
                        ManagedBrokerOrderState.Rejected,
                        result.ExternalCorrelationId,
                        result.ReasonCode);
                }
                else
                {
                    QueueReconciliation(brokerOrder.Id, result.ReasonCode ?? "CANCEL_REJECTED");
                }

                ReleaseUnusedReservationIfConclusive(entry);
                return;

            case BrokerResultState.Success:
                if (result.Value is null)
                {
                    brokerOrder.MarkUnknown("SUCCESS_WITHOUT_BROKER_ORDER_EVIDENCE", result.ExternalCorrelationId);
                    QueueReconciliation(brokerOrder.Id, "SUCCESS_WITHOUT_BROKER_ORDER_EVIDENCE");
                    return;
                }

                ApplyBrokerSnapshot(entry, result.Value, resolvingUnknown: false);
                ResolveReconciliationIfSafe(entry);
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.State, "Unknown broker result state.");
        }
    }

    private void ApplyBrokerSnapshot(OmsEntry entry, BrokerOrderSnapshot snapshot, bool resolvingUnknown)
    {
        var brokerOrder = entry.BrokerOrder ?? throw new InvalidOperationException("Broker order is required.");

        if (snapshot.BrokerOrderId != brokerOrder.Id)
        {
            QueueReconciliation(brokerOrder.Id, "BROKER_ORDER_ID_MISMATCH");
            if (brokerOrder.State != ManagedBrokerOrderState.Unknown && !IsTerminal(brokerOrder.State))
            {
                brokerOrder.MarkUnknown("BROKER_ORDER_ID_MISMATCH", brokerOrder.ExternalOrderId);
            }

            return;
        }

        var executionQuantity = snapshot.Executions.Sum(execution => execution.Quantity);
        if (snapshot.RequestedQuantity != brokerOrder.RequestedQuantity || snapshot.FilledQuantity != executionQuantity)
        {
            QueueReconciliation(brokerOrder.Id, "BROKER_SNAPSHOT_QUANTITY_MISMATCH");
            if (brokerOrder.State != ManagedBrokerOrderState.Unknown && !IsTerminal(brokerOrder.State))
            {
                brokerOrder.MarkUnknown("BROKER_SNAPSHOT_QUANTITY_MISMATCH", snapshot.ExternalOrderId);
            }

            return;
        }

        foreach (var execution in snapshot.Executions.OrderBy(execution => execution.TradeAt).ThenBy(execution => execution.ExecutionId, StringComparer.Ordinal))
        {
            ManagedExecution managedExecution = new(
                execution.ExecutionId,
                execution.Quantity,
                execution.Price,
                execution.TradeAt);

            if (brokerOrder.ApplyExecution(managedExecution))
            {
                ConsumeReservation(entry, managedExecution);
            }
        }

        var mappedState = MapState(snapshot.State);
        if (resolvingUnknown)
        {
            if (brokerOrder.State == ManagedBrokerOrderState.Filled && mappedState == ManagedBrokerOrderState.Filled)
            {
                // Complete execution evidence can itself establish FILLED while processing the recovery snapshot.
            }
            else if (brokerOrder.State == ManagedBrokerOrderState.Unknown)
            {
                brokerOrder.ResolveUnknown(mappedState, snapshot.ExternalOrderId, snapshot.ReasonCode);
            }
        }
        else if (!IsTerminal(brokerOrder.State))
        {
            brokerOrder.ObserveState(mappedState, snapshot.ExternalOrderId, snapshot.ReasonCode);
        }

        ReleaseUnusedReservationIfConclusive(entry);
    }

    private static void ConsumeReservation(OmsEntry entry, ManagedExecution execution)
    {
        var reservation = entry.Intent.Reservation ?? throw new InvalidOperationException("Execution requires an approved reservation.");
        var amount = reservation.Kind switch
        {
            ReservationKind.Cash => execution.Quantity * execution.Price,
            ReservationKind.PositionQuantity => execution.Quantity,
            _ => throw new ArgumentOutOfRangeException(nameof(reservation.Kind), reservation.Kind, "Unknown reservation kind."),
        };

        reservation.Consume(amount);
    }

    private static void ReleaseUnusedReservationIfConclusive(OmsEntry entry)
    {
        if (entry.BrokerOrder is null || entry.Intent.Reservation is null)
        {
            return;
        }

        if (entry.BrokerOrder.State is ManagedBrokerOrderState.Filled or ManagedBrokerOrderState.Cancelled or ManagedBrokerOrderState.Rejected)
        {
            entry.Intent.Reservation.ReleaseRemaining();
        }
    }

    private void QueueReconciliation(Guid brokerOrderId, string reasonCode)
    {
        if (_reconciliationByBrokerOrder.TryGetValue(brokerOrderId, out var existing) && existing.ResolvedAt is null)
        {
            existing.ReasonCode = reasonCode;
            return;
        }

        _reconciliationByBrokerOrder[brokerOrderId] = new MutableReconciliationItem(
            Guid.NewGuid(),
            brokerOrderId,
            reasonCode,
            _clock());
    }

    private void ResolveReconciliationIfSafe(OmsEntry entry)
    {
        if (entry.BrokerOrder is null || entry.BrokerOrder.State == ManagedBrokerOrderState.Unknown)
        {
            return;
        }

        if (_reconciliationByBrokerOrder.TryGetValue(entry.BrokerOrder.Id, out var item) && item.ResolvedAt is null)
        {
            item.ResolvedAt = _clock();
        }
    }

    private static SubmitBrokerOrderCommand ToBrokerCommand(OmsOrderRequest request) => new(
        request.BrokerOrderId,
        request.OrderIntentId,
        request.IdempotencyKey,
        request.AccountReference,
        request.InstrumentReference,
        request.Side,
        request.OrderType,
        request.Quantity,
        request.LimitPrice,
        request.TimeInForce);

    private static ManagedBrokerOrderState MapState(BrokerOrderState state) => state switch
    {
        BrokerOrderState.PendingSubmit => ManagedBrokerOrderState.PendingSubmit,
        BrokerOrderState.Submitted => ManagedBrokerOrderState.Submitted,
        BrokerOrderState.Open => ManagedBrokerOrderState.Open,
        BrokerOrderState.PartiallyFilled => ManagedBrokerOrderState.PartiallyFilled,
        BrokerOrderState.Filled => ManagedBrokerOrderState.Filled,
        BrokerOrderState.CancelPending => ManagedBrokerOrderState.CancelPending,
        BrokerOrderState.Cancelled => ManagedBrokerOrderState.Cancelled,
        BrokerOrderState.Rejected => ManagedBrokerOrderState.Rejected,
        BrokerOrderState.Unknown => ManagedBrokerOrderState.Unknown,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown normalized broker state."),
    };

    private static bool IsTerminal(ManagedBrokerOrderState state) =>
        state is ManagedBrokerOrderState.Filled or ManagedBrokerOrderState.Cancelled or ManagedBrokerOrderState.Rejected;

    private static OmsSnapshot Snapshot(OmsEntry entry)
    {
        var reservation = entry.Intent.Reservation;
        var brokerOrder = entry.BrokerOrder;
        var reconciliationRequired = brokerOrder is not null &&
            entry.Owner._reconciliationByBrokerOrder.TryGetValue(brokerOrder.Id, out var item) &&
            item.ResolvedAt is null;

        return new OmsSnapshot(
            entry.Intent.Id,
            entry.Intent.State,
            brokerOrder?.Id,
            brokerOrder?.State,
            brokerOrder?.FilledQuantity ?? 0m,
            reservation?.State,
            reservation?.InitialAmount ?? 0m,
            reservation?.ConsumedAmount ?? 0m,
            reservation?.ReleasedAmount ?? 0m,
            reservation?.RemainingAmount ?? 0m,
            reconciliationRequired,
            brokerOrder?.ReasonCode ?? entry.Intent.RejectionReason);
    }

    private OmsEntry GetEntry(Guid orderIntentId)
    {
        if (!_entries.TryGetValue(orderIntentId, out var entry))
        {
            throw new KeyNotFoundException($"Order intent {orderIntentId} does not exist.");
        }

        return entry;
    }

    private static void ValidateRequest(OmsOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.OrderIntentId == Guid.Empty || request.BrokerOrderId == Guid.Empty)
        {
            throw new ArgumentException("Order intent and broker order identifiers are required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) ||
            string.IsNullOrWhiteSpace(request.AccountReference) ||
            string.IsNullOrWhiteSpace(request.InstrumentReference))
        {
            throw new ArgumentException("Idempotency, account and instrument references are required.", nameof(request));
        }

        if (request.Quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Order quantity must be positive.");
        }

        if (request.OrderType == BrokerOrderType.Limit && request.LimitPrice is null or <= 0m)
        {
            throw new ArgumentException("Limit order requires a positive exact-decimal limit price.", nameof(request));
        }

        if (request.OrderType == BrokerOrderType.Market && request.LimitPrice is not null)
        {
            throw new ArgumentException("Market order must not carry a limit price.", nameof(request));
        }
    }

    private sealed record IntentFingerprint(
        Guid OrderIntentId,
        Guid BrokerOrderId,
        string AccountReference,
        string InstrumentReference,
        BrokerOrderSide Side,
        BrokerOrderType OrderType,
        decimal Quantity,
        decimal? LimitPrice,
        BrokerTimeInForce TimeInForce)
    {
        public static IntentFingerprint From(OmsOrderRequest request) => new(
            request.OrderIntentId,
            request.BrokerOrderId,
            request.AccountReference,
            request.InstrumentReference,
            request.Side,
            request.OrderType,
            request.Quantity,
            request.LimitPrice,
            request.TimeInForce);
    }

    private sealed class OmsEntry
    {
        public OmsEntry(OmsOrderRequest request, OrderIntent intent)
        {
            Request = request;
            Intent = intent;
        }

        public OmsOrderRequest Request { get; }

        public OrderIntent Intent { get; }

        public ManagedBrokerOrder? BrokerOrder { get; set; }

        public OrderManagementService Owner { get; set; } = null!;
    }

    private sealed class MutableReconciliationItem
    {
        public MutableReconciliationItem(Guid id, Guid brokerOrderId, string reasonCode, DateTimeOffset createdAt)
        {
            Id = id;
            BrokerOrderId = brokerOrderId;
            ReasonCode = reasonCode;
            CreatedAt = createdAt;
        }

        public Guid Id { get; }

        public Guid BrokerOrderId { get; }

        public string ReasonCode { get; set; }

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset? ResolvedAt { get; set; }

        public OmsReconciliationItem ToSnapshot() => new(Id, BrokerOrderId, ReasonCode, CreatedAt, ResolvedAt);
    }
}
