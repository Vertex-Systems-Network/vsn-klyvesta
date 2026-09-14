using Klyvesta.Domain.Insights;
using Klyvesta.Domain.Reporting;

namespace Klyvesta.Domain.Dashboard;

public sealed record CustomerDashboardPortfolioSummary(
    decimal Cash,
    decimal InvestedCostBasis,
    decimal BookValue,
    int PositionCount,
    long LastProjectionSequence,
    int SourceEventCount,
    int ExecutionCount);

public sealed record CustomerDashboardInsightMetric(
    string Name,
    decimal Value);

public sealed record CustomerDashboardInsight(
    string Code,
    CustomerInsightSeverity Severity,
    IReadOnlyList<CustomerDashboardInsightMetric> Metrics);

public sealed record CustomerDashboardAuthority(
    bool ReadOnly,
    bool PaperOnly,
    bool InformationalOnly,
    bool UsesLiveMarketData,
    bool CanPlaceOrders,
    bool CanDispatchNotifications,
    bool CanOverrideRiskPolicy,
    bool IsInvestmentAdvice,
    bool IsSuitabilityDecision)
{
    public static CustomerDashboardAuthority ReadOnlyPaperInformational { get; } =
        new(
            ReadOnly: true,
            PaperOnly: true,
            InformationalOnly: true,
            UsesLiveMarketData: false,
            CanPlaceOrders: false,
            CanDispatchNotifications: false,
            CanOverrideRiskPolicy: false,
            IsInvestmentAdvice: false,
            IsSuitabilityDecision: false);
}

public sealed record CustomerDashboardSnapshot(
    Guid CustomerId,
    string AccountReference,
    DateTimeOffset AsOf,
    DateTimeOffset ReportGeneratedAt,
    CustomerReportPeriod ReportPeriod,
    CustomerDashboardPortfolioSummary Portfolio,
    decimal InsightPortfolioValue,
    bool ValuationEvidenceFresh,
    IReadOnlyList<CustomerDashboardInsight> Insights,
    CustomerDashboardAuthority Authority);
