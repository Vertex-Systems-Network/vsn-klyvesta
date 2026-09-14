using Klyvesta.Domain.Dashboard;
using Klyvesta.Domain.Insights;
using Klyvesta.Domain.Reporting;

namespace Klyvesta.Application.Dashboard;

public sealed record CustomerDashboardRequest(
    Guid AuthenticatedCustomerId,
    Guid CustomerId,
    string AccountReference,
    DateTimeOffset AsOf,
    CustomerPortfolioReport Report,
    CustomerInsightReport InsightReport);

public interface ICustomerDashboardBuilder
{
    CustomerDashboardSnapshot Build(CustomerDashboardRequest request);
}

public sealed class DeterministicCustomerDashboardBuilder : ICustomerDashboardBuilder
{
    private const int MaxInsights = 64;
    private const int MaxMetricsPerInsight = 32;

    public CustomerDashboardSnapshot Build(CustomerDashboardRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureCustomerScope(request.AuthenticatedCustomerId, request.CustomerId);
        var accountReference = NormalizeReference(request.AccountReference, nameof(request.AccountReference));
        if (request.AsOf == default)
        {
            throw new ArgumentException("Dashboard as-of time is required.", nameof(request));
        }

        ArgumentNullException.ThrowIfNull(request.Report);
        ArgumentNullException.ThrowIfNull(request.InsightReport);

        var reportAccount = NormalizeReference(request.Report.AccountReference, nameof(request.Report.AccountReference));
        if (!StringComparer.Ordinal.Equals(accountReference, reportAccount))
        {
            throw new InvalidOperationException("CUSTOMER_DASHBOARD_REPORT_ACCOUNT_SCOPE_MISMATCH");
        }

        var insightAccount = NormalizeReference(request.InsightReport.AccountReference, nameof(request.InsightReport.AccountReference));
        if (!StringComparer.Ordinal.Equals(accountReference, insightAccount))
        {
            throw new InvalidOperationException("CUSTOMER_DASHBOARD_INSIGHT_ACCOUNT_SCOPE_MISMATCH");
        }

        ValidateReport(request.Report, request.AsOf);
        ValidateInsights(request.InsightReport, request.AsOf);

        var portfolio = new CustomerDashboardPortfolioSummary(
            request.Report.Cash,
            request.Report.InvestedCostBasis,
            request.Report.BookValue,
            request.Report.Positions.Count,
            request.Report.LastProjectionSequence,
            request.Report.SourceEventCount,
            request.Report.ExecutionCount);

        var insights = NormalizeInsights(request.InsightReport.Insights);
        var period = new CustomerReportPeriod(request.Report.Period.StartDate, request.Report.Period.EndDate);

        return new CustomerDashboardSnapshot(
            request.CustomerId,
            accountReference,
            request.AsOf,
            request.Report.GeneratedAt,
            request.InsightReport.AsOf,
            period,
            portfolio,
            request.InsightReport.PortfolioValue,
            request.InsightReport.ValuationEvidenceFresh,
            insights,
            CustomerDashboardAuthority.ReadOnlyPaperInformational);
    }

    private static void ValidateReport(CustomerPortfolioReport report, DateTimeOffset asOf)
    {
        if (report.GeneratedAt == default || report.GeneratedAt > asOf)
        {
            throw new InvalidOperationException("CUSTOMER_DASHBOARD_REPORT_TIME_INVALID");
        }

        ArgumentNullException.ThrowIfNull(report.Period);
        var period = report.Period.Normalize();
        if (period.EndDate > DateOnly.FromDateTime(report.GeneratedAt.UtcDateTime))
        {
            throw new InvalidOperationException("CUSTOMER_DASHBOARD_REPORT_PERIOD_INVALID");
        }

        ArgumentNullException.ThrowIfNull(report.Positions);
        if (report.Cash < 0m || report.InvestedCostBasis < 0m || report.BookValue < 0m)
        {
            throw new InvalidOperationException("CUSTOMER_DASHBOARD_REPORT_VALUE_INVALID");
        }

        if (report.BookValue != report.Cash + report.InvestedCostBasis)
        {
            throw new InvalidOperationException("CUSTOMER_DASHBOARD_REPORT_BOOK_VALUE_MISMATCH");
        }

        if (report.LastProjectionSequence < -1 || report.SourceEventCount < 0 || report.ExecutionCount < 0 ||
            report.ExecutionCount > report.SourceEventCount)
        {
            throw new InvalidOperationException("CUSTOMER_DASHBOARD_REPORT_COUNTER_INVALID");
        }

        ArgumentNullException.ThrowIfNull(report.Authority);
        if (!report.Authority.PaperOnly ||
            report.Authority.UsesLiveMarketData ||
            report.Authority.CanPlaceOrders ||
            report.Authority.CanDispatchNotifications ||
            report.Authority.IsInvestmentAdvice)
        {
            throw new InvalidOperationException("CUSTOMER_DASHBOARD_REPORT_AUTHORITY_UNSAFE");
        }
    }

    private static void ValidateInsights(CustomerInsightReport insightReport, DateTimeOffset asOf)
    {
        if (insightReport.AsOf == default || insightReport.AsOf > asOf)
        {
            throw new InvalidOperationException("CUSTOMER_DASHBOARD_INSIGHT_TIME_INVALID");
        }

        if (insightReport.PortfolioValue <= 0m)
        {
            throw new InvalidOperationException("CUSTOMER_DASHBOARD_INSIGHT_VALUE_INVALID");
        }

        ArgumentNullException.ThrowIfNull(insightReport.Insights);
        if (insightReport.Insights.Count > MaxInsights)
        {
            throw new InvalidOperationException("CUSTOMER_DASHBOARD_INSIGHT_COUNT_EXCEEDED");
        }

        ArgumentNullException.ThrowIfNull(insightReport.Authority);
        if (!insightReport.Authority.InformationalOnly ||
            insightReport.Authority.CanPlaceOrders ||
            insightReport.Authority.CanOverrideRiskPolicy ||
            insightReport.Authority.IsSuitabilityDecision)
        {
            throw new InvalidOperationException("CUSTOMER_DASHBOARD_INSIGHT_AUTHORITY_UNSAFE");
        }
    }

    private static CustomerDashboardInsight[] NormalizeInsights(IReadOnlyList<CustomerInsight> insights)
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<CustomerDashboardInsight>(insights.Count);

        foreach (var insight in insights)
        {
            ArgumentNullException.ThrowIfNull(insight);
            if (!Enum.IsDefined(insight.Severity))
            {
                throw new InvalidOperationException("CUSTOMER_DASHBOARD_INSIGHT_SEVERITY_INVALID");
            }

            var code = NormalizeToken(insight.Code, nameof(insight.Code), 64);
            if (!codes.Add(code))
            {
                throw new InvalidOperationException("CUSTOMER_DASHBOARD_DUPLICATE_INSIGHT_CODE");
            }

            ArgumentNullException.ThrowIfNull(insight.Metrics);
            if (insight.Metrics.Count > MaxMetricsPerInsight)
            {
                throw new InvalidOperationException("CUSTOMER_DASHBOARD_METRIC_COUNT_EXCEEDED");
            }

            var metricNames = new HashSet<string>(StringComparer.Ordinal);
            var metrics = new List<CustomerDashboardInsightMetric>(insight.Metrics.Count);
            foreach (var metric in insight.Metrics)
            {
                ArgumentNullException.ThrowIfNull(metric);
                var name = NormalizeToken(metric.Name, nameof(metric.Name), 64);
                if (!metricNames.Add(name))
                {
                    throw new InvalidOperationException("CUSTOMER_DASHBOARD_DUPLICATE_METRIC_NAME");
                }

                metrics.Add(new CustomerDashboardInsightMetric(name, metric.Value));
            }

            result.Add(new CustomerDashboardInsight(
                code,
                insight.Severity,
                metrics.OrderBy(metric => metric.Name, StringComparer.Ordinal).ToArray()));
        }

        return result.OrderBy(insight => insight.Code, StringComparer.Ordinal).ToArray();
    }

    private static void EnsureCustomerScope(Guid authenticatedCustomerId, Guid customerId)
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
            throw new InvalidOperationException("CUSTOMER_DASHBOARD_SCOPE_MISMATCH");
        }
    }

    private static string NormalizeReference(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > 128 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("Reference must be at most 128 non-control characters.", parameterName);
        }

        return normalized;
    }

    private static string NormalizeToken(string value, string parameterName, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength || normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new ArgumentException("Dashboard tokens must contain only ASCII letters, digits, or underscore.", parameterName);
        }

        return normalized;
    }
}
