namespace Klyvesta.Domain.Portfolios;

public enum PortfolioProjectionEventKind
{
    OpeningCash,
    ConfirmedExecution,
}

public enum PortfolioTradeSide
{
    Buy,
    Sell,
}

public enum PortfolioProjectionApplyResult
{
    Applied,
    DuplicateEvent,
    DuplicateExecution,
}

public sealed record PortfolioProjectionEvent(
    Guid EventId,
    string AccountReference,
    long Sequence,
    DateTimeOffset OccurredAt,
    PortfolioProjectionEventKind Kind,
    string EvidenceReference,
    decimal CashAmount,
    string? ExecutionId,
    string? InstrumentReference,
    PortfolioTradeSide? Side,
    decimal Quantity,
    decimal Price)
{
    public static PortfolioProjectionEvent OpeningCash(
        Guid eventId,
        string accountReference,
        long sequence,
        DateTimeOffset occurredAt,
        decimal amount,
        string evidenceReference) =>
        new(
            eventId,
            accountReference,
            sequence,
            occurredAt,
            PortfolioProjectionEventKind.OpeningCash,
            evidenceReference,
            amount,
            ExecutionId: null,
            InstrumentReference: null,
            Side: null,
            Quantity: 0m,
            Price: 0m);

    public static PortfolioProjectionEvent Execution(
        Guid eventId,
        string accountReference,
        long sequence,
        DateTimeOffset occurredAt,
        string executionId,
        string instrumentReference,
        PortfolioTradeSide side,
        decimal quantity,
        decimal price,
        string evidenceReference) =>
        new(
            eventId,
            accountReference,
            sequence,
            occurredAt,
            PortfolioProjectionEventKind.ConfirmedExecution,
            evidenceReference,
            CashAmount: 0m,
            executionId,
            instrumentReference,
            side,
            quantity,
            price);
}

public sealed record ProjectedPosition(
    string InstrumentReference,
    decimal Quantity,
    decimal AverageCost);

public sealed record PortfolioProjectionSnapshot(
    string AccountReference,
    decimal Cash,
    IReadOnlyList<ProjectedPosition> Positions,
    long LastSequence,
    int UniqueSourceEventCount,
    int UniqueExecutionCount);

public sealed class PortfolioProjection
{
    private readonly string _accountReference;
    private readonly HashSet<Guid> _eventIds = [];
    private readonly HashSet<string> _executionIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PositionState> _positions = new(StringComparer.Ordinal);
    private bool _openingCashApplied;
    private decimal _cash;
    private long _lastSequence = -1;

    public PortfolioProjection(string accountReference)
    {
        if (string.IsNullOrWhiteSpace(accountReference))
        {
            throw new ArgumentException("Account reference is required.", nameof(accountReference));
        }

        _accountReference = accountReference;
    }

    public PortfolioProjectionApplyResult Apply(PortfolioProjectionEvent sourceEvent)
    {
        ArgumentNullException.ThrowIfNull(sourceEvent);

        if (!StringComparer.Ordinal.Equals(sourceEvent.AccountReference, _accountReference))
        {
            throw new InvalidOperationException("Projection source event belongs to a different account.");
        }

        if (_eventIds.Contains(sourceEvent.EventId))
        {
            return PortfolioProjectionApplyResult.DuplicateEvent;
        }

        ValidateCommon(sourceEvent);

        if (sourceEvent.Sequence <= _lastSequence)
        {
            throw new InvalidOperationException("New unique projection source event sequence must be strictly increasing.");
        }

        PortfolioProjectionApplyResult result;
        switch (sourceEvent.Kind)
        {
            case PortfolioProjectionEventKind.OpeningCash:
                ApplyOpeningCash(sourceEvent);
                result = PortfolioProjectionApplyResult.Applied;
                break;

            case PortfolioProjectionEventKind.ConfirmedExecution:
                result = ApplyExecution(sourceEvent);
                break;

            default:
                throw new InvalidOperationException($"Unknown portfolio projection event kind {sourceEvent.Kind}.");
        }

        _eventIds.Add(sourceEvent.EventId);
        _lastSequence = sourceEvent.Sequence;
        return result;
    }

    public PortfolioProjectionSnapshot Snapshot() => new(
        _accountReference,
        _cash,
        _positions
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new ProjectedPosition(pair.Key, pair.Value.Quantity, pair.Value.AverageCost))
            .ToArray(),
        _lastSequence,
        _eventIds.Count,
        _executionIds.Count);

    public static PortfolioProjectionSnapshot Rebuild(
        string accountReference,
        IEnumerable<PortfolioProjectionEvent> sourceEvents)
    {
        ArgumentNullException.ThrowIfNull(sourceEvents);

        var projection = new PortfolioProjection(accountReference);
        foreach (var sourceEvent in sourceEvents
                     .OrderBy(item => item.Sequence)
                     .ThenBy(item => item.EventId))
        {
            projection.Apply(sourceEvent);
        }

        return projection.Snapshot();
    }

    private void ApplyOpeningCash(PortfolioProjectionEvent sourceEvent)
    {
        if (_openingCashApplied)
        {
            throw new InvalidOperationException("Opening cash can be established only once per projection stream.");
        }

        if (_eventIds.Count != 0)
        {
            throw new InvalidOperationException("Opening cash must be the first unique projection source event.");
        }

        if (sourceEvent.CashAmount < 0m)
        {
            throw new InvalidOperationException("Paper opening cash cannot be negative.");
        }

        if (sourceEvent.ExecutionId is not null ||
            sourceEvent.InstrumentReference is not null ||
            sourceEvent.Side is not null ||
            sourceEvent.Quantity != 0m ||
            sourceEvent.Price != 0m)
        {
            throw new InvalidOperationException("Opening cash event cannot contain execution fields.");
        }

        _cash = sourceEvent.CashAmount;
        _openingCashApplied = true;
    }

    private PortfolioProjectionApplyResult ApplyExecution(PortfolioProjectionEvent sourceEvent)
    {
        if (!_openingCashApplied)
        {
            throw new InvalidOperationException("Projection execution requires an opening cash source event first.");
        }

        var executionId = RequireText(sourceEvent.ExecutionId, "Execution id");
        var instrument = RequireText(sourceEvent.InstrumentReference, "Instrument reference");
        var side = sourceEvent.Side ?? throw new InvalidOperationException("Execution side is required.");

        if (sourceEvent.CashAmount != 0m)
        {
            throw new InvalidOperationException("Execution event cannot carry an independent cash amount.");
        }

        if (sourceEvent.Quantity <= 0m || sourceEvent.Price <= 0m)
        {
            throw new InvalidOperationException("Execution quantity and price must be positive exact decimals.");
        }

        if (_executionIds.Contains(executionId))
        {
            return PortfolioProjectionApplyResult.DuplicateExecution;
        }

        _positions.TryGetValue(instrument, out var position);
        position ??= new PositionState();
        var notional = sourceEvent.Quantity * sourceEvent.Price;

        switch (side)
        {
            case PortfolioTradeSide.Buy:
                if (_cash < notional)
                {
                    throw new InvalidOperationException("Confirmed paper buy execution would make projected cash negative.");
                }

                var oldCost = position.Quantity * position.AverageCost;
                var newQuantity = position.Quantity + sourceEvent.Quantity;
                position.AverageCost = (oldCost + notional) / newQuantity;
                position.Quantity = newQuantity;
                _cash -= notional;
                _positions[instrument] = position;
                break;

            case PortfolioTradeSide.Sell:
                if (position.Quantity < sourceEvent.Quantity)
                {
                    throw new InvalidOperationException("Confirmed paper sell execution exceeds projected position quantity.");
                }

                position.Quantity -= sourceEvent.Quantity;
                _cash += notional;

                if (position.Quantity == 0m)
                {
                    _positions.Remove(instrument);
                }
                else
                {
                    _positions[instrument] = position;
                }

                break;

            default:
                throw new InvalidOperationException($"Unknown portfolio trade side {side}.");
        }

        _executionIds.Add(executionId);
        return PortfolioProjectionApplyResult.Applied;
    }

    private static void ValidateCommon(PortfolioProjectionEvent sourceEvent)
    {
        if (sourceEvent.EventId == Guid.Empty)
        {
            throw new InvalidOperationException("Projection source event id is required.");
        }

        if (sourceEvent.Sequence < 0)
        {
            throw new InvalidOperationException("Projection source event sequence cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(sourceEvent.EvidenceReference))
        {
            throw new InvalidOperationException("Projection source event evidence reference is required.");
        }
    }

    private static string RequireText(string? value, string label) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{label} is required.")
            : value;

    private sealed class PositionState
    {
        public decimal Quantity { get; set; }

        public decimal AverageCost { get; set; }
    }
}
