using Klyvesta.Application.Brokerage;
using Klyvesta.Domain.Compliance;

namespace Klyvesta.Application.Compliance;

public sealed record ComplianceSubmissionContext(
    PaperCompliancePolicy Policy,
    PaperExecutionMode ExecutionMode,
    ComplianceAccountStatus AccountStatus,
    RegulatoryFeatureStatus RegulatoryFeatureStatus,
    ManualReviewStatus ManualReviewStatus,
    InstrumentRestrictionStatus InstrumentRestrictionStatus,
    ComplianceMandateEvidence? Mandate = null);

public interface IComplianceSubmissionContextProvider
{
    ComplianceSubmissionContext GetContext(SubmitBrokerOrderCommand command, DateTimeOffset evaluatedAt);
}

public sealed class ComplianceGuardBrokerAdapter : IBrokerAdapter
{
    private readonly object _sync = new();
    private readonly IBrokerAdapter _inner;
    private readonly IComplianceGate _gate;
    private readonly IComplianceSubmissionContextProvider _contextProvider;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Dictionary<Guid, ComplianceDecision> _decisions = [];

    public ComplianceGuardBrokerAdapter(
        IBrokerAdapter inner,
        IComplianceGate gate,
        IComplianceSubmissionContextProvider contextProvider,
        Func<DateTimeOffset>? clock = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public ComplianceDecision? GetRecordedDecision(Guid brokerOrderId)
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
            throw new InvalidOperationException("Compliance submission context provider returned null.");
        ArgumentNullException.ThrowIfNull(context.Policy);

        ComplianceEvaluationRequest request = new(
            command.AccountReference,
            command.InstrumentReference,
            context.ExecutionMode,
            context.AccountStatus,
            context.RegulatoryFeatureStatus,
            context.ManualReviewStatus,
            context.InstrumentRestrictionStatus,
            context.Mandate,
            evaluatedAt);

        var decision = _gate.Evaluate(context.Policy, request);
        lock (_sync)
        {
            _decisions[command.BrokerOrderId] = decision;
        }

        if (decision.Outcome == ComplianceDecisionOutcome.Allow)
        {
            return _inner.SubmitOrderAsync(command, cancellationToken);
        }

        var state = decision.Outcome == ComplianceDecisionOutcome.Hold
            ? BrokerResultState.RetryableFailure
            : BrokerResultState.Rejected;

        return Task.FromResult(new BrokerOperationResult<BrokerOrderSnapshot>(
            $"compliance-guard-{command.BrokerOrderId:N}",
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
