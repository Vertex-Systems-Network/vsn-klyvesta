using Klyvesta.Application.Brokerage;
using Klyvesta.Application.Orders;
using Klyvesta.Domain.Portfolios;

namespace Klyvesta.Application.Portfolios;

public enum PortfolioMismatchKind
{
    BrokerEvidenceUnavailable,
    AmbiguousBrokerEvidence,
    CashMismatch,
    MissingBrokerPosition,
    UnexpectedBrokerPosition,
    PositionQuantityMismatch,
}

public enum PortfolioMismatchSeverity
{
    Critical,
}

public sealed record PortfolioMismatch(
    PortfolioMismatchKind Kind,
    PortfolioMismatchSeverity Severity,
    string ReasonCode,
    string? InstrumentReference,
    decimal? ProjectedValue,
    decimal? BrokerValue);

public sealed record PortfolioReconciliationReport(
    Guid ReportId,
    string AccountReference,
    DateTimeOffset GeneratedAt,
    PortfolioProjectionSnapshot Projection,
    IReadOnlyList<PortfolioMismatch> Mismatches,
    bool IsMatch,
    bool ExecutionHoldActive);

public sealed class PortfolioProjectionService : IOrderExecutionHoldProvider
{
    public const string CriticalMismatchHoldReason = "PORTFOLIO_RECONCILIATION_CRITICAL_MISMATCH";

    private readonly object _sync = new();
    private readonly IBrokerAdapter _brokerAdapter;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Dictionary<string, List<PortfolioProjectionEvent>> _sourceEvents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PortfolioProjectionSnapshot> _projections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PortfolioReconciliationReport> _lastReports = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _holds = new(StringComparer.Ordinal);

    public PortfolioProjectionService(IBrokerAdapter brokerAdapter, Func<DateTimeOffset>? clock = null)
    {
        _brokerAdapter = brokerAdapter ?? throw new ArgumentNullException(nameof(brokerAdapter));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public PortfolioProjectionSnapshot AppendSourceEvent(PortfolioProjectionEvent sourceEvent)
    {
        ArgumentNullException.ThrowIfNull(sourceEvent);

        lock (_sync)
        {
            if (!_sourceEvents.TryGetValue(sourceEvent.AccountReference, out var events))
            {
                events = [];
                _sourceEvents.Add(sourceEvent.AccountReference, events);
            }

            var existing = events.FirstOrDefault(item => item.EventId == sourceEvent.EventId);
            if (existing is not null)
            {
                if (existing != sourceEvent)
                {
                    throw new InvalidOperationException("Projection source event id conflicts with a different payload.");
                }

                return GetProjectionLocked(sourceEvent.AccountReference);
            }

            events.Add(sourceEvent);
            var projection = PortfolioProjection.Rebuild(sourceEvent.AccountReference, events);
            _projections[sourceEvent.AccountReference] = projection;
            return projection;
        }
    }

    public PortfolioProjectionSnapshot Rebuild(string accountReference)
    {
        ValidateAccountReference(accountReference);

        lock (_sync)
        {
            if (!_sourceEvents.TryGetValue(accountReference, out var events))
            {
                throw new KeyNotFoundException($"No projection source events exist for account {accountReference}.");
            }

            var projection = PortfolioProjection.Rebuild(accountReference, events);
            _projections[accountReference] = projection;
            return projection;
        }
    }

    public PortfolioProjectionSnapshot GetProjection(string accountReference)
    {
        ValidateAccountReference(accountReference);

        lock (_sync)
        {
            return GetProjectionLocked(accountReference);
        }
    }

    public PortfolioReconciliationReport? GetLastReport(string accountReference)
    {
        ValidateAccountReference(accountReference);

        lock (_sync)
        {
            return _lastReports.TryGetValue(accountReference, out var report) ? report : null;
        }
    }

    public OrderExecutionHold GetHold(string accountReference)
    {
        ValidateAccountReference(accountReference);

        lock (_sync)
        {
            return _holds.TryGetValue(accountReference, out var reasonCode)
                ? OrderExecutionHold.Hold(reasonCode)
                : OrderExecutionHold.Allow;
        }
    }

    public async Task<PortfolioReconciliationReport> ReconcileAsync(
        string accountReference,
        CancellationToken cancellationToken = default)
    {
        ValidateAccountReference(accountReference);
        PortfolioProjectionSnapshot projection;

        lock (_sync)
        {
            projection = GetProjectionLocked(accountReference);
        }

        var balancesTask = _brokerAdapter.GetBalancesAsync(accountReference, cancellationToken);
        var positionsTask = _brokerAdapter.GetPositionsAsync(accountReference, cancellationToken);
        await Task.WhenAll(balancesTask, positionsTask).ConfigureAwait(false);

        var balanceResult = await balancesTask.ConfigureAwait(false);
        var positionResult = await positionsTask.ConfigureAwait(false);
        var mismatches = Compare(projection, balanceResult, positionResult);
        var report = new PortfolioReconciliationReport(
            Guid.NewGuid(),
            accountReference,
            _clock(),
            projection,
            mismatches,
            IsMatch: mismatches.Count == 0,
            ExecutionHoldActive: mismatches.Count != 0);

        lock (_sync)
        {
            _lastReports[accountReference] = report;
            if (mismatches.Count == 0)
            {
                _holds.Remove(accountReference);
            }
            else
            {
                _holds[accountReference] = CriticalMismatchHoldReason;
            }
        }

        return report;
    }

    private static IReadOnlyList<PortfolioMismatch> Compare(
        PortfolioProjectionSnapshot projection,
        BrokerOperationResult<IReadOnlyList<BrokerCashBalance>> balanceResult,
        BrokerOperationResult<IReadOnlyList<BrokerPosition>> positionResult)
    {
        var mismatches = new List<PortfolioMismatch>();

        if (balanceResult.State != BrokerResultState.Success || balanceResult.Value is null)
        {
            mismatches.Add(Critical(
                PortfolioMismatchKind.BrokerEvidenceUnavailable,
                balanceResult.ReasonCode ?? "BROKER_BALANCE_EVIDENCE_UNAVAILABLE"));
        }
        else
        {
            CompareCash(projection, balanceResult.Value, mismatches);
        }

        if (positionResult.State != BrokerResultState.Success || positionResult.Value is null)
        {
            mismatches.Add(Critical(
                PortfolioMismatchKind.BrokerEvidenceUnavailable,
                positionResult.ReasonCode ?? "BROKER_POSITION_EVIDENCE_UNAVAILABLE"));
        }
        else
        {
            ComparePositions(projection, positionResult.Value, mismatches);
        }

        return mismatches;
    }

    private static void CompareCash(
        PortfolioProjectionSnapshot projection,
        IReadOnlyList<BrokerCashBalance> brokerBalances,
        ICollection<PortfolioMismatch> mismatches)
    {
        var pkrBalances = brokerBalances
            .Where(balance => StringComparer.Ordinal.Equals(balance.Currency, "PKR"))
            .ToArray();

        if (pkrBalances.Length != 1)
        {
            mismatches.Add(Critical(
                PortfolioMismatchKind.AmbiguousBrokerEvidence,
                pkrBalances.Length == 0 ? "BROKER_PKR_CASH_MISSING" : "BROKER_PKR_CASH_DUPLICATE"));
            return;
        }

        var brokerCash = pkrBalances[0].Cash;
        if (brokerCash != projection.Cash)
        {
            mismatches.Add(new PortfolioMismatch(
                PortfolioMismatchKind.CashMismatch,
                PortfolioMismatchSeverity.Critical,
                "CASH_MISMATCH",
                InstrumentReference: null,
                ProjectedValue: projection.Cash,
                BrokerValue: brokerCash));
        }
    }

    private static void ComparePositions(
        PortfolioProjectionSnapshot projection,
        IReadOnlyList<BrokerPosition> brokerPositions,
        ICollection<PortfolioMismatch> mismatches)
    {
        var duplicateBrokerInstrument = brokerPositions
            .GroupBy(position => position.InstrumentReference, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateBrokerInstrument is not null)
        {
            mismatches.Add(new PortfolioMismatch(
                PortfolioMismatchKind.AmbiguousBrokerEvidence,
                PortfolioMismatchSeverity.Critical,
                "BROKER_POSITION_DUPLICATE_INSTRUMENT",
                duplicateBrokerInstrument.Key,
                ProjectedValue: null,
                BrokerValue: null));
            return;
        }

        var projectedByInstrument = projection.Positions.ToDictionary(
            position => position.InstrumentReference,
            position => position,
            StringComparer.Ordinal);
        var brokerByInstrument = brokerPositions
            .Where(position => position.Quantity != 0m)
            .ToDictionary(
                position => position.InstrumentReference,
                position => position,
                StringComparer.Ordinal);

        foreach (var projected in projectedByInstrument.Values)
        {
            if (!brokerByInstrument.TryGetValue(projected.InstrumentReference, out var broker))
            {
                mismatches.Add(new PortfolioMismatch(
                    PortfolioMismatchKind.MissingBrokerPosition,
                    PortfolioMismatchSeverity.Critical,
                    "PROJECTED_POSITION_MISSING_AT_BROKER",
                    projected.InstrumentReference,
                    ProjectedValue: projected.Quantity,
                    BrokerValue: 0m));
                continue;
            }

            if (broker.Quantity != projected.Quantity)
            {
                mismatches.Add(new PortfolioMismatch(
                    PortfolioMismatchKind.PositionQuantityMismatch,
                    PortfolioMismatchSeverity.Critical,
                    "POSITION_QUANTITY_MISMATCH",
                    projected.InstrumentReference,
                    ProjectedValue: projected.Quantity,
                    BrokerValue: broker.Quantity));
            }
        }

        foreach (var broker in brokerByInstrument.Values)
        {
            if (projectedByInstrument.ContainsKey(broker.InstrumentReference))
            {
                continue;
            }

            mismatches.Add(new PortfolioMismatch(
                PortfolioMismatchKind.UnexpectedBrokerPosition,
                PortfolioMismatchSeverity.Critical,
                "UNEXPECTED_BROKER_POSITION",
                broker.InstrumentReference,
                ProjectedValue: 0m,
                BrokerValue: broker.Quantity));
        }
    }

    private PortfolioProjectionSnapshot GetProjectionLocked(string accountReference) =>
        _projections.TryGetValue(accountReference, out var projection)
            ? projection
            : throw new KeyNotFoundException($"Portfolio projection does not exist for account {accountReference}.");

    private static PortfolioMismatch Critical(PortfolioMismatchKind kind, string reasonCode) =>
        new(kind, PortfolioMismatchSeverity.Critical, reasonCode, null, null, null);

    private static void ValidateAccountReference(string accountReference)
    {
        if (string.IsNullOrWhiteSpace(accountReference))
        {
            throw new ArgumentException("Account reference is required.", nameof(accountReference));
        }
    }
}
