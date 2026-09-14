using System.Runtime.CompilerServices;
using Klyvesta.Application.Dashboard;
using Klyvesta.Domain.Insights;
using Klyvesta.Domain.Notifications;
using Klyvesta.Domain.Reporting;

namespace Klyvesta.CustomerDashboardVerifier;

internal static class DashboardProvenanceVerifier
{
    [ModuleInitializer]
    internal static void VerifyInsightEvidenceTimestamp()
    {
        var customerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var dashboardAsOf = new DateTimeOffset(2026, 9, 15, 3, 0, 0, TimeSpan.Zero);
        var reportGeneratedAt = dashboardAsOf.AddMinutes(-10);
        var insightAsOf = dashboardAsOf.AddMinutes(-5);
        var reportDate = DateOnly.FromDateTime(reportGeneratedAt.UtcDateTime);

        var report = new CustomerPortfolioReport(
            "paper-account-001",
            new CustomerReportPeriod(reportDate, reportDate),
            reportGeneratedAt,
            100m,
            0m,
            100m,
            Array.Empty<CustomerReportPosition>(),
            -1,
            0,
            0,
            new CustomerReportDeliveryMetadata(NotificationChannel.InApp, "dashboard-provenance-verifier"),
            CustomerReportAuthority.ReadOnlyPaper);

        var insightReport = new CustomerInsightReport(
            "paper-account-001",
            insightAsOf,
            100m,
            true,
            Array.Empty<CustomerInsight>(),
            CustomerInsightAuthority.Informational);

        var snapshot = new DeterministicCustomerDashboardBuilder().Build(
            new CustomerDashboardRequest(
                customerId,
                customerId,
                "paper-account-001",
                dashboardAsOf,
                report,
                insightReport));

        if (snapshot.InsightsAsOf != insightAsOf)
        {
            throw new InvalidOperationException("DASH-036 insight evidence timestamp provenance drifted.");
        }

        Console.WriteLine("PASS DASH-036: insight evidence timestamp is propagated exactly");
    }
}
