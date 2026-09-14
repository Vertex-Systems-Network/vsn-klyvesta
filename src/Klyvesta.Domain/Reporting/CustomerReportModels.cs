using Klyvesta.Domain.Notifications;

namespace Klyvesta.Domain.Reporting;

public sealed record CustomerReportPeriod(
    DateOnly StartDate,
    DateOnly EndDate)
{
    public CustomerReportPeriod Normalize()
    {
        if (EndDate < StartDate)
        {
            throw new ArgumentException("Report end date cannot be before start date.", nameof(EndDate));
        }

        return this;
    }
}

public sealed record CustomerReportDeliveryMetadata(
    NotificationChannel Channel,
    string TemplateKey)
{
    public CustomerReportDeliveryMetadata Normalize()
    {
        if (Channel == NotificationChannel.Unknown)
        {
            throw new ArgumentException("Report delivery channel is required.", nameof(Channel));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(TemplateKey);
        var normalizedTemplate = TemplateKey.Trim();
        if (normalizedTemplate.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(TemplateKey), "Report template key is too long.");
        }

        return this with { TemplateKey = normalizedTemplate };
    }
}

public sealed record CustomerReportPosition(
    string InstrumentReference,
    decimal Quantity,
    decimal AverageCost,
    decimal CostBasis);

public sealed record CustomerReportAuthority(
    bool PaperOnly,
    bool UsesLiveMarketData,
    bool CanPlaceOrders,
    bool CanDispatchNotifications,
    bool IsInvestmentAdvice)
{
    public static CustomerReportAuthority ReadOnlyPaper { get; } =
        new(
            PaperOnly: true,
            UsesLiveMarketData: false,
            CanPlaceOrders: false,
            CanDispatchNotifications: false,
            IsInvestmentAdvice: false);
}

public sealed record CustomerPortfolioReport(
    string AccountReference,
    CustomerReportPeriod Period,
    DateTimeOffset GeneratedAt,
    decimal Cash,
    decimal InvestedCostBasis,
    decimal BookValue,
    IReadOnlyList<CustomerReportPosition> Positions,
    long LastProjectionSequence,
    int SourceEventCount,
    int ExecutionCount,
    CustomerReportDeliveryMetadata Delivery,
    CustomerReportAuthority Authority);
