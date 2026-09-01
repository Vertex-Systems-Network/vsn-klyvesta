using Klyvesta.Application.Brokerage;
using Klyvesta.Domain.Risk;

namespace Klyvesta.Application.Risk;

public sealed record RiskSubmissionContext(
    PaperRiskPolicy Policy,
    RiskInstrumentContext? Instrument,
    RiskMarketEvidence? MarketEvidence,
    RiskPortfolioSnapshot? Portfolio,
    RiskActivityWindow? Activity,
    bool UsesMargin = false,
    decimal RequestedLeverageMultiple = 1m);

public interface IRiskSubmissionContextProvider
{
    RiskSubmissionContext GetContext(SubmitBrokerOrderCommand command, DateTimeOffset evaluatedAt);
}

public sealed class RiskGuardBrokerAdapter : IBrokerAdapter
{
    private readonly object _sync = new();
    private readonly IBrokerAdapter _inner;
    private readonly DeterministicRiskGovernor _governor;
    private readonly IRiskSubmissionContextProvider _contextProvider;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Dictionary<Guid, RiskDecision> _decisions = [];

    public RiskGuardBrokerAdapter(
        IBrokerAdapter inner,
        DeterministicRiskGovernor governor,
        IRiskSubmissionContextProvider contextProvider,
        Func<DateTimeOffset>? clock = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _governor = governor ?? throw new ArgumentNullException(nameof(governor));
        _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public RiskDecision? GetRecordedDecision(Guid brokerOrderId)
    {
        lock (_sync)
        {
            return _decisions.TryGetValue(brokerOrderId, out var decision) ? decision : null;
        }
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
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(command);

        var evaluatedAt = _clock();
        var context = _contextProvider.GetContext(command, evaluatedAt) ??
            throw new InvalidOperationException("Risk submission context provider returned null.");
        ArgumentNullException.ThrowIfNull(context.Policy);

        var expectedPrice = command.LimitPrice ?? context.MarketEvidence?.LastPrice ?? 0m;
        RiskEvaluationRequest request = new(
            command.AccountReference,
            command.InstrumentReference,
            command.Side == BrokerOrderSide.Buy ? RiskTradeSide.Buy : RiskTradeSide.Sell,
            command.Quantity,
            expectedPrice,
            context.UsesMargin,
            context.RequestedLeverageMultiple,
            context.Instrument,
            context.MarketEvidence,
            context.Portfolio,
            context.Activity,
            evaluatedAt);

        var decision = _governor.Evaluate(context.Policy, request);
        lock (_sync)
        {
            _decisions[command.BrokerOrderId] = decision;
        }

        if (decision.Outcome == RiskDecisionOutcome.Allow)
        {
            return _inner.SubmitOrderAsync(command, cancellationToken);
        }

        var state = decision.Outcome == RiskDecisionOutcome.Hold
            ? BrokerResultState.RetryableFailure
            : BrokerResultState.Rejected;
        return Task.FromResult(new BrokerOperationResult<BrokerOrderSnapshot>(
            $"risk-guard-{command.BrokerOrderId:N}",
            BrokerEnvironment.Paper,
            "SubmitOrder",
            evaluatedAt,
            state,
            Value: null,
            decision.PrimaryReasonCode));
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
