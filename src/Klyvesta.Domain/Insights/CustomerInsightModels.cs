namespace Klyvesta.Domain.Insights;

public enum CustomerInsightSeverity
{
    Information = 0,
    Attention = 1,
    Warning = 2,
}

public sealed record CustomerInsightMetric(
    string Name,
    decimal Value);

public sealed record CustomerInsight(
    string Code,
    CustomerInsightSeverity Severity,
    string Summary,
    string Explanation,
    IReadOnlyList<CustomerInsightMetric> Metrics);

public sealed record CustomerInsightAuthority(
    bool InformationalOnly,
    bool CanPlaceOrders,
    bool CanOverrideRiskPolicy,
    bool IsSuitabilityDecision)
{
    public static CustomerInsightAuthority Informational { get; } =
        new(
            InformationalOnly: true,
            CanPlaceOrders: false,
            CanOverrideRiskPolicy: false,
            IsSuitabilityDecision: false);
}

public sealed record CustomerInsightReport(
    string AccountReference,
    DateTimeOffset AsOf,
    decimal PortfolioValue,
    bool ValuationEvidenceFresh,
    IReadOnlyList<CustomerInsight> Insights,
    CustomerInsightAuthority Authority);
