using Klyvesta.Application.AlertRules;
using Klyvesta.Domain.AlertRules;

namespace Klyvesta.Infrastructure.AlertRules;

public sealed class InMemoryCustomerAlertRuleStore : ICustomerAlertRuleStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, Dictionary<Guid, CustomerAlertRule>> _rules = [];

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _rules.Values.Sum(group => group.Count);
            }
        }
    }

    public ValueTask<IReadOnlyList<CustomerAlertRule>> ListAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        EnsureCustomerId(customerId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_rules.TryGetValue(customerId, out var customerRules))
            {
                return ValueTask.FromResult<IReadOnlyList<CustomerAlertRule>>(Array.Empty<CustomerAlertRule>());
            }

            return ValueTask.FromResult<IReadOnlyList<CustomerAlertRule>>(
                customerRules.Values.OrderBy(rule => rule.RuleId).ToArray());
        }
    }

    public ValueTask<CustomerAlertRule?> FindAsync(
        Guid customerId,
        Guid ruleId,
        CancellationToken cancellationToken = default)
    {
        EnsureCustomerId(customerId);
        if (ruleId == Guid.Empty)
        {
            throw new ArgumentException("Rule ID is required.", nameof(ruleId));
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_rules.TryGetValue(customerId, out var customerRules)
                && customerRules.TryGetValue(ruleId, out var rule))
            {
                return ValueTask.FromResult<CustomerAlertRule?>(rule);
            }

            return ValueTask.FromResult<CustomerAlertRule?>(null);
        }
    }

    public ValueTask<CustomerAlertRule> CommitAsync(
        CustomerAlertRule candidate,
        long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (expectedRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedRevision), "Expected revision cannot be negative.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_rules.TryGetValue(candidate.CustomerId, out var customerRules))
            {
                customerRules = [];
                _rules[candidate.CustomerId] = customerRules;
            }

            if (customerRules.TryGetValue(candidate.RuleId, out var current))
            {
                if (current.Revision != expectedRevision)
                {
                    throw new InvalidOperationException("CUSTOMER_ALERT_RULE_REVISION_CONFLICT");
                }
            }
            else if (expectedRevision != 0)
            {
                throw new InvalidOperationException("CUSTOMER_ALERT_RULE_REVISION_CONFLICT");
            }

            if (candidate.Revision != expectedRevision + 1)
            {
                throw new InvalidOperationException("CUSTOMER_ALERT_RULE_INVALID_NEXT_REVISION");
            }

            customerRules[candidate.RuleId] = candidate;
            return ValueTask.FromResult(candidate);
        }
    }

    private static void EnsureCustomerId(Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID is required.", nameof(customerId));
        }
    }
}
