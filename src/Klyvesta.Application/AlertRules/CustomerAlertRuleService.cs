using Klyvesta.Domain.AlertRules;
using Klyvesta.Domain.Notifications;
using Klyvesta.Application.Preferences;

namespace Klyvesta.Application.AlertRules;

public sealed record CreateCustomerAlertRuleCommand(
    Guid RuleId,
    string Name,
    CustomerAlertMetric Metric,
    CustomerAlertComparison Comparison,
    decimal Threshold,
    IReadOnlyCollection<NotificationChannel> Channels,
    bool Enabled = true);

public sealed record UpdateCustomerAlertRuleCommand(
    long ExpectedRevision,
    string Name,
    CustomerAlertMetric Metric,
    CustomerAlertComparison Comparison,
    decimal Threshold,
    IReadOnlyCollection<NotificationChannel> Channels,
    bool Enabled);

public sealed record CustomerAlertCandidate(
    Guid RuleId,
    long RuleRevision,
    CustomerAlertMetric Metric,
    decimal ObservedValue,
    decimal Threshold,
    IReadOnlyList<NotificationChannel> Channels);

public sealed record CustomerAlertEvaluationResult(
    Guid CustomerId,
    IReadOnlyList<CustomerAlertCandidate> Candidates,
    CustomerAlertRuleAuthority Authority);

public interface ICustomerAlertRuleStore
{
    ValueTask<IReadOnlyList<CustomerAlertRule>> ListAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    ValueTask<CustomerAlertRule?> FindAsync(
        Guid customerId,
        Guid ruleId,
        CancellationToken cancellationToken = default);

    ValueTask<CustomerAlertRule> CommitAsync(
        CustomerAlertRule candidate,
        long expectedRevision,
        CancellationToken cancellationToken = default);
}

public sealed class CustomerAlertRuleService
{
    private readonly ICustomerAlertRuleStore _store;
    private readonly CustomerPreferenceService _preferences;
    private readonly TimeProvider _timeProvider;

    public CustomerAlertRuleService(
        ICustomerAlertRuleStore store,
        CustomerPreferenceService preferences,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _store = store;
        _preferences = preferences;
        _timeProvider = timeProvider;
    }

    public async ValueTask<CustomerAlertRule> CreateAsync(
        Guid authenticatedCustomerId,
        Guid customerId,
        CreateCustomerAlertRuleCommand command,
        CancellationToken cancellationToken = default)
    {
        EnsureOwnership(authenticatedCustomerId, customerId);
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (command.RuleId == Guid.Empty)
        {
            throw new ArgumentException("Rule ID is required.", nameof(command));
        }

        if (await _store.FindAsync(customerId, command.RuleId, cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new InvalidOperationException("CUSTOMER_ALERT_RULE_ALREADY_EXISTS");
        }

        var now = _timeProvider.GetUtcNow();
        var candidate = new CustomerAlertRule(
            command.RuleId,
            customerId,
            revision: 1,
            command.Name,
            command.Metric,
            command.Comparison,
            command.Threshold,
            command.Channels,
            command.Enabled,
            now,
            now);

        return await _store.CommitAsync(candidate, expectedRevision: 0, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<CustomerAlertRule> UpdateAsync(
        Guid authenticatedCustomerId,
        Guid customerId,
        Guid ruleId,
        UpdateCustomerAlertRuleCommand command,
        CancellationToken cancellationToken = default)
    {
        EnsureOwnership(authenticatedCustomerId, customerId);
        EnsureRuleId(ruleId);
        ArgumentNullException.ThrowIfNull(command);
        if (command.ExpectedRevision <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command), "Expected revision must be positive.");
        }

        var current = await _store.FindAsync(customerId, ruleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("CUSTOMER_ALERT_RULE_NOT_FOUND");
        if (current.Revision != command.ExpectedRevision)
        {
            throw new InvalidOperationException("CUSTOMER_ALERT_RULE_REVISION_CONFLICT");
        }

        var candidate = current.WithConfiguration(
            command.Name,
            command.Metric,
            command.Comparison,
            command.Threshold,
            command.Channels,
            command.Enabled,
            _timeProvider.GetUtcNow());

        return await _store.CommitAsync(candidate, command.ExpectedRevision, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<CustomerAlertRule> GetAsync(
        Guid authenticatedCustomerId,
        Guid customerId,
        Guid ruleId,
        CancellationToken cancellationToken = default)
    {
        EnsureOwnership(authenticatedCustomerId, customerId);
        EnsureRuleId(ruleId);
        return await _store.FindAsync(customerId, ruleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("CUSTOMER_ALERT_RULE_NOT_FOUND");
    }

    public async ValueTask<IReadOnlyList<CustomerAlertRule>> ListAsync(
        Guid authenticatedCustomerId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        EnsureOwnership(authenticatedCustomerId, customerId);
        return await _store.ListAsync(customerId, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<CustomerAlertEvaluationResult> EvaluateAsync(
        Guid authenticatedCustomerId,
        Guid customerId,
        IReadOnlyCollection<CustomerAlertObservation> observations,
        CancellationToken cancellationToken = default)
    {
        EnsureOwnership(authenticatedCustomerId, customerId);
        ArgumentNullException.ThrowIfNull(observations);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedObservations = observations.Select(observation =>
        {
            ArgumentNullException.ThrowIfNull(observation);
            return observation.Normalize();
        }).ToArray();

        if (normalizedObservations.Select(observation => observation.Metric).Distinct().Count() != normalizedObservations.Length)
        {
            throw new ArgumentException("Alert observations must contain at most one value per metric.", nameof(observations));
        }

        var observationByMetric = normalizedObservations.ToDictionary(observation => observation.Metric);
        var preferences = await _preferences.GetAsync(
            authenticatedCustomerId,
            customerId,
            cancellationToken).ConfigureAwait(false);
        var rules = await _store.ListAsync(customerId, cancellationToken).ConfigureAwait(false);

        var candidates = new List<CustomerAlertCandidate>();
        foreach (var rule in rules.Where(rule => rule.Enabled).OrderBy(rule => rule.RuleId))
        {
            if (!observationByMetric.TryGetValue(rule.Metric, out var observation) || !rule.Matches(observation))
            {
                continue;
            }

            var allowedChannels = rule.Channels
                .Where(preferences.IsEnabled)
                .OrderBy(channel => channel)
                .ToArray();
            if (allowedChannels.Length == 0)
            {
                continue;
            }

            candidates.Add(new CustomerAlertCandidate(
                rule.RuleId,
                rule.Revision,
                rule.Metric,
                observation.Value,
                rule.Threshold,
                allowedChannels));
        }

        return new CustomerAlertEvaluationResult(
            customerId,
            candidates.AsReadOnly(),
            CustomerAlertRuleAuthority.ConfigurationOnly);
    }

    private static void EnsureOwnership(Guid authenticatedCustomerId, Guid customerId)
    {
        if (authenticatedCustomerId == Guid.Empty)
        {
            throw new ArgumentException("Authenticated customer ID is required.", nameof(authenticatedCustomerId));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID is required.", nameof(customerId));
        }

        if (authenticatedCustomerId != customerId)
        {
            throw new InvalidOperationException("CUSTOMER_ALERT_RULE_OWNERSHIP_MISMATCH");
        }
    }

    private static void EnsureRuleId(Guid ruleId)
    {
        if (ruleId == Guid.Empty)
        {
            throw new ArgumentException("Rule ID is required.", nameof(ruleId));
        }
    }
}
