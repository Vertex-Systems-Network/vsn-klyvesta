using System.Collections.ObjectModel;
using Klyvesta.Domain.Notifications;

namespace Klyvesta.Domain.AlertRules;

public enum CustomerAlertMetric
{
    Unknown = 0,
    PaperPortfolioValue = 1,
    PaperCashBalance = 2,
    PaperBuyingPower = 3,
    PaperDailyPnl = 4,
}

public enum CustomerAlertComparison
{
    Unknown = 0,
    GreaterThan = 1,
    GreaterThanOrEqual = 2,
    LessThan = 3,
    LessThanOrEqual = 4,
}

public sealed record CustomerAlertRuleAuthority(
    bool CustomerScoped,
    bool ContainsContactPii,
    bool CanDispatchNotifications,
    bool CanFetchLiveMarketData,
    bool CanPlaceOrders,
    bool CanMoveMoney)
{
    public static CustomerAlertRuleAuthority ConfigurationOnly { get; } =
        new(
            CustomerScoped: true,
            ContainsContactPii: false,
            CanDispatchNotifications: false,
            CanFetchLiveMarketData: false,
            CanPlaceOrders: false,
            CanMoveMoney: false);
}

public sealed record CustomerAlertObservation(CustomerAlertMetric Metric, decimal Value)
{
    public CustomerAlertObservation Normalize()
    {
        ValidateMetric(Metric);
        ValidateValue(Value, nameof(Value));
        return this;
    }

    internal static void ValidateMetric(CustomerAlertMetric metric)
    {
        if (metric == CustomerAlertMetric.Unknown || !Enum.IsDefined(metric))
        {
            throw new ArgumentException("A supported paper alert metric is required.", nameof(metric));
        }
    }

    internal static void ValidateValue(decimal value, string parameterName)
    {
        const decimal limit = 1_000_000_000_000_000m;
        if (value < -limit || value > limit)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Alert numeric value is outside the supported deterministic range.");
        }
    }
}

public sealed class CustomerAlertRule
{
    private readonly ReadOnlyCollection<NotificationChannel> _channels;

    public CustomerAlertRule(
        Guid ruleId,
        Guid customerId,
        long revision,
        string name,
        CustomerAlertMetric metric,
        CustomerAlertComparison comparison,
        decimal threshold,
        IEnumerable<NotificationChannel> channels,
        bool enabled,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (ruleId == Guid.Empty)
        {
            throw new ArgumentException("Rule ID is required.", nameof(ruleId));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID is required.", nameof(customerId));
        }

        if (revision <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision), "Rule revision must be positive.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedName = name.Trim();
        if (normalizedName.Length > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(name), "Rule name is too long.");
        }

        CustomerAlertObservation.ValidateMetric(metric);
        if (comparison == CustomerAlertComparison.Unknown || !Enum.IsDefined(comparison))
        {
            throw new ArgumentException("A supported alert comparison is required.", nameof(comparison));
        }

        CustomerAlertObservation.ValidateValue(threshold, nameof(threshold));
        ArgumentNullException.ThrowIfNull(channels);
        var normalizedChannels = channels
            .Select(NormalizeChannel)
            .OrderBy(channel => channel)
            .ToArray();
        if (normalizedChannels.Length == 0)
        {
            throw new ArgumentException("At least one notification channel is required.", nameof(channels));
        }

        if (normalizedChannels.Distinct().Count() != normalizedChannels.Length)
        {
            throw new ArgumentException("Alert notification channels must be unique.", nameof(channels));
        }

        if (createdAt == default || updatedAt == default)
        {
            throw new ArgumentException("Rule timestamps are required.");
        }

        if (updatedAt < createdAt)
        {
            throw new ArgumentException("Rule update time cannot precede creation time.", nameof(updatedAt));
        }

        RuleId = ruleId;
        CustomerId = customerId;
        Revision = revision;
        Name = normalizedName;
        Metric = metric;
        Comparison = comparison;
        Threshold = threshold;
        _channels = Array.AsReadOnly(normalizedChannels);
        Enabled = enabled;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid RuleId { get; }

    public Guid CustomerId { get; }

    public long Revision { get; }

    public string Name { get; }

    public CustomerAlertMetric Metric { get; }

    public CustomerAlertComparison Comparison { get; }

    public decimal Threshold { get; }

    public IReadOnlyList<NotificationChannel> Channels => _channels;

    public bool Enabled { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public CustomerAlertRuleAuthority Authority => CustomerAlertRuleAuthority.ConfigurationOnly;

    public CustomerAlertRule WithConfiguration(
        string name,
        CustomerAlertMetric metric,
        CustomerAlertComparison comparison,
        decimal threshold,
        IEnumerable<NotificationChannel> channels,
        bool enabled,
        DateTimeOffset updatedAt)
    {
        return new CustomerAlertRule(
            RuleId,
            CustomerId,
            Revision + 1,
            name,
            metric,
            comparison,
            threshold,
            channels,
            enabled,
            CreatedAt,
            updatedAt);
    }

    public bool Matches(CustomerAlertObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var normalized = observation.Normalize();
        if (normalized.Metric != Metric)
        {
            return false;
        }

        return Comparison switch
        {
            CustomerAlertComparison.GreaterThan => normalized.Value > Threshold,
            CustomerAlertComparison.GreaterThanOrEqual => normalized.Value >= Threshold,
            CustomerAlertComparison.LessThan => normalized.Value < Threshold,
            CustomerAlertComparison.LessThanOrEqual => normalized.Value <= Threshold,
            _ => false,
        };
    }

    private static NotificationChannel NormalizeChannel(NotificationChannel channel)
    {
        if (channel == NotificationChannel.Unknown || !Enum.IsDefined(channel))
        {
            throw new ArgumentException("A supported notification channel is required.", nameof(channel));
        }

        return channel;
    }
}
