namespace Klyvesta.Domain.Orders;

public enum OrderIntentState
{
    Created,
    Validating,
    Rejected,
    Approved,
    ExecutionPending,
    ExecutionCreated,
    Cancelled,
    Expired,
}

public enum ManagedBrokerOrderState
{
    PendingSubmit,
    Submitting,
    Submitted,
    Open,
    PartiallyFilled,
    CancelPending,
    Filled,
    Cancelled,
    Rejected,
    Unknown,
}

public enum ReservationKind
{
    Cash,
    PositionQuantity,
}

public enum ReservationState
{
    Active,
    PartiallyConsumed,
    Consumed,
    Released,
}

public sealed record OrderValidationEvidence(
    bool Approved,
    string DecisionCode,
    string PolicyVersion,
    string EvidenceReference);

public sealed record ManagedExecution(
    string ExecutionId,
    decimal Quantity,
    decimal Price,
    DateTimeOffset TradeAt);

public sealed class OrderReservation
{
    public OrderReservation(Guid id, ReservationKind kind, decimal reservedAmount)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Reservation id is required.", nameof(id));
        }

        if (reservedAmount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(reservedAmount), "Reserved amount must be positive.");
        }

        Id = id;
        Kind = kind;
        InitialAmount = reservedAmount;
        State = ReservationState.Active;
    }

    public Guid Id { get; }

    public ReservationKind Kind { get; }

    public decimal InitialAmount { get; }

    public decimal ConsumedAmount { get; private set; }

    public decimal ReleasedAmount { get; private set; }

    public decimal RemainingAmount => InitialAmount - ConsumedAmount - ReleasedAmount;

    public ReservationState State { get; private set; }

    public bool Consume(decimal amount)
    {
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Consumption amount must be positive.");
        }

        if (State == ReservationState.Released)
        {
            throw new InvalidOperationException("Released reservation cannot be consumed.");
        }

        if (amount > RemainingAmount)
        {
            throw new InvalidOperationException("Reservation consumption cannot exceed remaining amount.");
        }

        ConsumedAmount += amount;
        State = RemainingAmount == 0m
            ? ReservationState.Consumed
            : ReservationState.PartiallyConsumed;

        return true;
    }

    public decimal ReleaseRemaining()
    {
        if (State == ReservationState.Released || RemainingAmount == 0m)
        {
            return 0m;
        }

        var released = RemainingAmount;
        ReleasedAmount += released;
        State = ReservationState.Released;
        return released;
    }
}

public sealed class OrderIntent
{
    public OrderIntent(
        Guid id,
        Guid plannedBrokerOrderId,
        string idempotencyKey,
        string accountReference,
        string instrumentReference,
        decimal quantity)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Order intent id is required.", nameof(id));
        }

        if (plannedBrokerOrderId == Guid.Empty)
        {
            throw new ArgumentException("Planned broker order id is required.", nameof(plannedBrokerOrderId));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        }

        if (string.IsNullOrWhiteSpace(accountReference))
        {
            throw new ArgumentException("Account reference is required.", nameof(accountReference));
        }

        if (string.IsNullOrWhiteSpace(instrumentReference))
        {
            throw new ArgumentException("Instrument reference is required.", nameof(instrumentReference));
        }

        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        Id = id;
        PlannedBrokerOrderId = plannedBrokerOrderId;
        IdempotencyKey = idempotencyKey;
        AccountReference = accountReference;
        InstrumentReference = instrumentReference;
        Quantity = quantity;
        State = OrderIntentState.Created;
    }

    public Guid Id { get; }

    public Guid PlannedBrokerOrderId { get; }

    public string IdempotencyKey { get; }

    public string AccountReference { get; }

    public string InstrumentReference { get; }

    public decimal Quantity { get; }

    public OrderIntentState State { get; private set; }

    public OrderValidationEvidence? ValidationEvidence { get; private set; }

    public string? RejectionReason { get; private set; }

    public OrderReservation? Reservation { get; private set; }

    public void StartValidation()
    {
        RequireState(OrderIntentState.Created);
        State = OrderIntentState.Validating;
    }

    public void RecordValidation(OrderValidationEvidence evidence, OrderReservation? reservation)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        RequireState(OrderIntentState.Validating);

        if (string.IsNullOrWhiteSpace(evidence.DecisionCode) ||
            string.IsNullOrWhiteSpace(evidence.PolicyVersion) ||
            string.IsNullOrWhiteSpace(evidence.EvidenceReference))
        {
            throw new ArgumentException("Validation evidence must contain decision, policy version and evidence reference.", nameof(evidence));
        }

        ValidationEvidence = evidence;

        if (!evidence.Approved)
        {
            if (reservation is not null)
            {
                throw new InvalidOperationException("Rejected intent cannot receive a reservation.");
            }

            RejectionReason = evidence.DecisionCode;
            State = OrderIntentState.Rejected;
            return;
        }

        Reservation = reservation ?? throw new InvalidOperationException("Approved intent requires an atomic reservation.");
        State = OrderIntentState.Approved;
    }

    public void QueueExecution()
    {
        RequireState(OrderIntentState.Approved);
        State = OrderIntentState.ExecutionPending;
    }

    public void MarkExecutionCreated()
    {
        RequireState(OrderIntentState.ExecutionPending);
        State = OrderIntentState.ExecutionCreated;
    }

    public void CancelBeforeBrokerSubmission()
    {
        if (State is not (OrderIntentState.Approved or OrderIntentState.ExecutionPending))
        {
            throw new InvalidOperationException($"Order intent cannot be cancelled before broker submission from {State}.");
        }

        Reservation?.ReleaseRemaining();
        State = OrderIntentState.Cancelled;
    }

    public void ExpireBeforeBrokerSubmission()
    {
        if (State is not (OrderIntentState.Approved or OrderIntentState.ExecutionPending))
        {
            throw new InvalidOperationException($"Order intent cannot expire before broker submission from {State}.");
        }

        Reservation?.ReleaseRemaining();
        State = OrderIntentState.Expired;
    }

    private void RequireState(OrderIntentState expected)
    {
        if (State != expected)
        {
            throw new InvalidOperationException($"Order intent transition requires {expected}, current state is {State}.");
        }
    }
}

public sealed class ManagedBrokerOrder
{
    private readonly Dictionary<string, ManagedExecution> _executions = new(StringComparer.Ordinal);

    public ManagedBrokerOrder(Guid id, Guid orderIntentId, decimal requestedQuantity)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Broker order id is required.", nameof(id));
        }

        if (orderIntentId == Guid.Empty)
        {
            throw new ArgumentException("Order intent id is required.", nameof(orderIntentId));
        }

        if (requestedQuantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedQuantity), "Requested quantity must be positive.");
        }

        Id = id;
        OrderIntentId = orderIntentId;
        RequestedQuantity = requestedQuantity;
        State = ManagedBrokerOrderState.PendingSubmit;
    }

    public Guid Id { get; }

    public Guid OrderIntentId { get; }

    public decimal RequestedQuantity { get; }

    public ManagedBrokerOrderState State { get; private set; }

    public string? ExternalOrderId { get; private set; }

    public string? ReasonCode { get; private set; }

    public decimal FilledQuantity => _executions.Values.Sum(execution => execution.Quantity);

    public IReadOnlyCollection<ManagedExecution> Executions => _executions.Values.ToArray();

    public void BeginSubmission()
    {
        RequireState(ManagedBrokerOrderState.PendingSubmit);
        State = ManagedBrokerOrderState.Submitting;
    }

    public void MarkRetryableBeforeSideEffect(string reasonCode)
    {
        RequireState(ManagedBrokerOrderState.Submitting);
        ReasonCode = RequireReason(reasonCode);
        State = ManagedBrokerOrderState.PendingSubmit;
    }

    public void MarkUnknown(string reasonCode, string? externalOrderId)
    {
        if (IsTerminal(State))
        {
            throw new InvalidOperationException($"Terminal broker order {State} cannot become UNKNOWN.");
        }

        ReasonCode = RequireReason(reasonCode);
        ExternalOrderId ??= externalOrderId;
        State = ManagedBrokerOrderState.Unknown;
    }

    public bool ApplyExecution(ManagedExecution execution)
    {
        ArgumentNullException.ThrowIfNull(execution);

        if (string.IsNullOrWhiteSpace(execution.ExecutionId))
        {
            throw new ArgumentException("Execution id is required.", nameof(execution));
        }

        if (execution.Quantity <= 0m || execution.Price <= 0m)
        {
            throw new InvalidOperationException("Execution quantity and price must be positive exact decimals.");
        }

        if (_executions.ContainsKey(execution.ExecutionId))
        {
            return false;
        }

        if (FilledQuantity + execution.Quantity > RequestedQuantity)
        {
            throw new InvalidOperationException("Execution would exceed requested order quantity.");
        }

        _executions.Add(execution.ExecutionId, execution);

        if (FilledQuantity == RequestedQuantity)
        {
            State = ManagedBrokerOrderState.Filled;
        }
        else if (State is not (ManagedBrokerOrderState.CancelPending or ManagedBrokerOrderState.Unknown))
        {
            State = ManagedBrokerOrderState.PartiallyFilled;
        }

        return true;
    }

    public void ObserveState(ManagedBrokerOrderState targetState, string? externalOrderId = null, string? reasonCode = null)
    {
        ExternalOrderId ??= externalOrderId;
        ReasonCode = reasonCode ?? ReasonCode;

        if (State == ManagedBrokerOrderState.Unknown)
        {
            throw new InvalidOperationException("UNKNOWN broker order must be resolved through explicit evidence recovery.");
        }

        if (IsTerminal(State))
        {
            return;
        }

        if (targetState == ManagedBrokerOrderState.Unknown)
        {
            MarkUnknown(reasonCode ?? "UNKNOWN_BROKER_STATE", externalOrderId);
            return;
        }

        if (targetState == ManagedBrokerOrderState.Filled && FilledQuantity != RequestedQuantity)
        {
            return;
        }

        if (targetState == ManagedBrokerOrderState.PartiallyFilled &&
            (FilledQuantity <= 0m || FilledQuantity >= RequestedQuantity))
        {
            return;
        }

        if (targetState == ManagedBrokerOrderState.Rejected && FilledQuantity > 0m)
        {
            return;
        }

        if (targetState == ManagedBrokerOrderState.Cancelled)
        {
            State = ManagedBrokerOrderState.Cancelled;
            return;
        }

        if (State == ManagedBrokerOrderState.CancelPending && targetState == ManagedBrokerOrderState.PartiallyFilled)
        {
            return;
        }

        if (Rank(targetState) >= Rank(State))
        {
            State = targetState;
        }
    }

    public void BeginCancel()
    {
        if (State is not (ManagedBrokerOrderState.Submitted or ManagedBrokerOrderState.Open or ManagedBrokerOrderState.PartiallyFilled))
        {
            throw new InvalidOperationException($"Broker order cannot enter cancel-pending from {State}.");
        }

        State = ManagedBrokerOrderState.CancelPending;
    }

    public void ResolveUnknown(ManagedBrokerOrderState evidenceState, string? externalOrderId = null, string? reasonCode = null)
    {
        RequireState(ManagedBrokerOrderState.Unknown);

        if (evidenceState is ManagedBrokerOrderState.PendingSubmit or ManagedBrokerOrderState.Submitting or ManagedBrokerOrderState.Unknown)
        {
            throw new InvalidOperationException($"Evidence state {evidenceState} cannot resolve UNKNOWN.");
        }

        if (evidenceState == ManagedBrokerOrderState.Filled && FilledQuantity != RequestedQuantity)
        {
            throw new InvalidOperationException("UNKNOWN cannot resolve to FILLED without complete execution quantity.");
        }

        if (evidenceState == ManagedBrokerOrderState.PartiallyFilled && FilledQuantity <= 0m)
        {
            throw new InvalidOperationException("UNKNOWN cannot resolve to PARTIALLY_FILLED without execution evidence.");
        }

        if (evidenceState == ManagedBrokerOrderState.Rejected && FilledQuantity > 0m)
        {
            throw new InvalidOperationException("UNKNOWN cannot resolve to REJECTED after execution evidence exists.");
        }

        ExternalOrderId ??= externalOrderId;
        ReasonCode = reasonCode;
        State = evidenceState;
    }

    private static int Rank(ManagedBrokerOrderState state) => state switch
    {
        ManagedBrokerOrderState.PendingSubmit => 0,
        ManagedBrokerOrderState.Submitting => 1,
        ManagedBrokerOrderState.Submitted => 2,
        ManagedBrokerOrderState.Open => 3,
        ManagedBrokerOrderState.PartiallyFilled => 4,
        ManagedBrokerOrderState.CancelPending => 5,
        ManagedBrokerOrderState.Filled => 6,
        ManagedBrokerOrderState.Cancelled => 6,
        ManagedBrokerOrderState.Rejected => 6,
        ManagedBrokerOrderState.Unknown => 7,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown broker order state."),
    };

    private static bool IsTerminal(ManagedBrokerOrderState state) =>
        state is ManagedBrokerOrderState.Filled or ManagedBrokerOrderState.Cancelled or ManagedBrokerOrderState.Rejected;

    private static string RequireReason(string reasonCode) =>
        string.IsNullOrWhiteSpace(reasonCode)
            ? throw new ArgumentException("Reason code is required.", nameof(reasonCode))
            : reasonCode;

    private void RequireState(ManagedBrokerOrderState expected)
    {
        if (State != expected)
        {
            throw new InvalidOperationException($"Broker order transition requires {expected}, current state is {State}.");
        }
    }
}
