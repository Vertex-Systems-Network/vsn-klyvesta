using System.Text.Json;
using Klyvesta.Application.Dashboard;
using Klyvesta.Domain.Dashboard;
using Klyvesta.Domain.Insights;
using Klyvesta.Domain.Notifications;
using Klyvesta.Domain.Reporting;

namespace Klyvesta.CustomerDashboardVerifier;

internal static class Program
{
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherCustomerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset AsOf = new(2026, 9, 15, 2, 0, 0, TimeSpan.Zero);
    private const string AccountReference = "paper-account-001";

    public static int Main()
    {
        var failures = new List<string>();
        var passes = 0;

        Check("DASH-001", "same inputs produce deterministic dashboard output", () =>
        {
            var builder = new DeterministicCustomerDashboardBuilder();
            var first = builder.Build(CreateRequest());
            var second = builder.Build(CreateRequest());
            Require(first == second, "record equality should remain deterministic for equivalent immutable output");
            Require(JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second), "serialized dashboard output drifted");
        });

        Check("DASH-002", "cross-customer request fails closed", () =>
            ExpectThrows<InvalidOperationException>(() => Build(CreateRequest() with { AuthenticatedCustomerId = OtherCustomerId }), "CUSTOMER_DASHBOARD_SCOPE_MISMATCH"));

        Check("DASH-003", "missing authenticated customer id is rejected", () =>
            ExpectThrows<ArgumentException>(() => Build(CreateRequest() with { AuthenticatedCustomerId = Guid.Empty })));

        Check("DASH-004", "missing target customer id is rejected", () =>
            ExpectThrows<ArgumentException>(() => Build(CreateRequest() with { CustomerId = Guid.Empty })));

        Check("DASH-005", "blank account reference is rejected", () =>
            ExpectThrows<ArgumentException>(() => Build(CreateRequest() with { AccountReference = "  " })));

        Check("DASH-006", "control characters in account reference are rejected", () =>
            ExpectThrows<ArgumentException>(() => Build(CreateRequest() with { AccountReference = "paper\naccount" })));

        Check("DASH-007", "report account scope mismatch is rejected", () =>
        {
            var request = CreateRequest();
            ExpectThrows<InvalidOperationException>(() => Build(request with { Report = request.Report with { AccountReference = "other-account" } }), "CUSTOMER_DASHBOARD_REPORT_ACCOUNT_SCOPE_MISMATCH");
        });

        Check("DASH-008", "insight account scope mismatch is rejected", () =>
        {
            var request = CreateRequest();
            ExpectThrows<InvalidOperationException>(() => Build(request with { InsightReport = request.InsightReport with { AccountReference = "other-account" } }), "CUSTOMER_DASHBOARD_INSIGHT_ACCOUNT_SCOPE_MISMATCH");
        });

        Check("DASH-009", "future report evidence is rejected", () =>
        {
            var request = CreateRequest();
            ExpectThrows<InvalidOperationException>(() => Build(request with { Report = request.Report with { GeneratedAt = AsOf.AddSeconds(1) } }), "CUSTOMER_DASHBOARD_REPORT_TIME_INVALID");
        });

        Check("DASH-010", "future insight evidence is rejected", () =>
        {
            var request = CreateRequest();
            ExpectThrows<InvalidOperationException>(() => Build(request with { InsightReport = request.InsightReport with { AsOf = AsOf.AddSeconds(1) } }), "CUSTOMER_DASHBOARD_INSIGHT_TIME_INVALID");
        });

        Check("DASH-011", "report period cannot extend beyond report generation date", () =>
        {
            var request = CreateRequest();
            var futurePeriod = new CustomerReportPeriod(DateOnly.FromDateTime(AsOf.UtcDateTime), DateOnly.FromDateTime(AsOf.UtcDateTime.AddDays(1)));
            ExpectThrows<InvalidOperationException>(() => Build(request with { Report = request.Report with { Period = futurePeriod } }), "CUSTOMER_DASHBOARD_REPORT_PERIOD_INVALID");
        });

        Check("DASH-012", "report must remain paper-only", () =>
        {
            var request = CreateRequest();
            var authority = request.Report.Authority with { PaperOnly = false };
            ExpectThrows<InvalidOperationException>(() => Build(request with { Report = request.Report with { Authority = authority } }), "CUSTOMER_DASHBOARD_REPORT_AUTHORITY_UNSAFE");
        });

        Check("DASH-013", "live market-data authority is rejected", () =>
        {
            var request = CreateRequest();
            var authority = request.Report.Authority with { UsesLiveMarketData = true };
            ExpectThrows<InvalidOperationException>(() => Build(request with { Report = request.Report with { Authority = authority } }), "CUSTOMER_DASHBOARD_REPORT_AUTHORITY_UNSAFE");
        });

        Check("DASH-014", "order-placement authority is rejected", () =>
        {
            var request = CreateRequest();
            var authority = request.Report.Authority with { CanPlaceOrders = true };
            ExpectThrows<InvalidOperationException>(() => Build(request with { Report = request.Report with { Authority = authority } }), "CUSTOMER_DASHBOARD_REPORT_AUTHORITY_UNSAFE");
        });

        Check("DASH-015", "notification-dispatch authority is rejected", () =>
        {
            var request = CreateRequest();
            var authority = request.Report.Authority with { CanDispatchNotifications = true };
            ExpectThrows<InvalidOperationException>(() => Build(request with { Report = request.Report with { Authority = authority } }), "CUSTOMER_DASHBOARD_REPORT_AUTHORITY_UNSAFE");
        });

        Check("DASH-016", "investment-advice authority is rejected", () =>
        {
            var request = CreateRequest();
            var authority = request.Report.Authority with { IsInvestmentAdvice = true };
            ExpectThrows<InvalidOperationException>(() => Build(request with { Report = request.Report with { Authority = authority } }), "CUSTOMER_DASHBOARD_REPORT_AUTHORITY_UNSAFE");
        });

        Check("DASH-017", "insights must remain informational-only", () =>
        {
            var request = CreateRequest();
            var authority = request.InsightReport.Authority with { InformationalOnly = false };
            ExpectThrows<InvalidOperationException>(() => Build(request with { InsightReport = request.InsightReport with { Authority = authority } }), "CUSTOMER_DASHBOARD_INSIGHT_AUTHORITY_UNSAFE");
        });

        Check("DASH-018", "insight order-placement authority is rejected", () =>
        {
            var request = CreateRequest();
            var authority = request.InsightReport.Authority with { CanPlaceOrders = true };
            ExpectThrows<InvalidOperationException>(() => Build(request with { InsightReport = request.InsightReport with { Authority = authority } }), "CUSTOMER_DASHBOARD_INSIGHT_AUTHORITY_UNSAFE");
        });

        Check("DASH-019", "insight risk-override authority is rejected", () =>
        {
            var request = CreateRequest();
            var authority = request.InsightReport.Authority with { CanOverrideRiskPolicy = true };
            ExpectThrows<InvalidOperationException>(() => Build(request with { InsightReport = request.InsightReport with { Authority = authority } }), "CUSTOMER_DASHBOARD_INSIGHT_AUTHORITY_UNSAFE");
        });

        Check("DASH-020", "suitability decision authority is rejected", () =>
        {
            var request = CreateRequest();
            var authority = request.InsightReport.Authority with { IsSuitabilityDecision = true };
            ExpectThrows<InvalidOperationException>(() => Build(request with { InsightReport = request.InsightReport with { Authority = authority } }), "CUSTOMER_DASHBOARD_INSIGHT_AUTHORITY_UNSAFE");
        });

        Check("DASH-021", "paper report book-value arithmetic is validated", () =>
        {
            var request = CreateRequest();
            ExpectThrows<InvalidOperationException>(() => Build(request with { Report = request.Report with { BookValue = request.Report.BookValue + 0.01m } }), "CUSTOMER_DASHBOARD_REPORT_BOOK_VALUE_MISMATCH");
        });

        Check("DASH-022", "invalid paper projection counters are rejected", () =>
        {
            var request = CreateRequest();
            ExpectThrows<InvalidOperationException>(() => Build(request with { Report = request.Report with { ExecutionCount = request.Report.SourceEventCount + 1 } }), "CUSTOMER_DASHBOARD_REPORT_COUNTER_INVALID");
        });

        Check("DASH-023", "non-positive insight portfolio value is rejected", () =>
        {
            var request = CreateRequest();
            ExpectThrows<InvalidOperationException>(() => Build(request with { InsightReport = request.InsightReport with { PortfolioValue = 0m } }), "CUSTOMER_DASHBOARD_INSIGHT_VALUE_INVALID");
        });

        Check("DASH-024", "duplicate insight codes are rejected", () =>
        {
            var request = CreateRequest();
            var first = request.InsightReport.Insights[0];
            var duplicate = first with { Severity = CustomerInsightSeverity.Warning };
            ExpectThrows<InvalidOperationException>(() => Build(request with { InsightReport = request.InsightReport with { Insights = new[] { first, duplicate } } }), "CUSTOMER_DASHBOARD_DUPLICATE_INSIGHT_CODE");
        });

        Check("DASH-025", "unsafe insight code tokens are rejected", () =>
        {
            var request = CreateRequest();
            var unsafeInsight = request.InsightReport.Insights[0] with { Code = "customer email" };
            ExpectThrows<ArgumentException>(() => Build(request with { InsightReport = request.InsightReport with { Insights = new[] { unsafeInsight } } }));
        });

        Check("DASH-026", "unsafe metric-name tokens are rejected", () =>
        {
            var request = CreateRequest();
            var unsafeMetric = new CustomerInsightMetric("customer email", 1m);
            var unsafeInsight = request.InsightReport.Insights[0] with { Metrics = new[] { unsafeMetric } };
            ExpectThrows<ArgumentException>(() => Build(request with { InsightReport = request.InsightReport with { Insights = new[] { unsafeInsight } } }));
        });

        Check("DASH-027", "duplicate metric names are rejected", () =>
        {
            var request = CreateRequest();
            var first = new CustomerInsightMetric("limit_fraction", 0.8m);
            var second = new CustomerInsightMetric("limit_fraction", 0.9m);
            var duplicateMetrics = request.InsightReport.Insights[0] with { Metrics = new[] { first, second } };
            ExpectThrows<InvalidOperationException>(() => Build(request with { InsightReport = request.InsightReport with { Insights = new[] { duplicateMetrics } } }), "CUSTOMER_DASHBOARD_DUPLICATE_METRIC_NAME");
        });

        Check("DASH-028", "insight cards and metrics are returned in stable ordinal order", () =>
        {
            var request = CreateRequest();
            var zeta = new CustomerInsight("ZETA", CustomerInsightSeverity.Information, "ignored", "ignored", new[]
            {
                new CustomerInsightMetric("z_metric", 2m),
                new CustomerInsightMetric("a_metric", 1m),
            });
            var alpha = new CustomerInsight("ALPHA", CustomerInsightSeverity.Warning, "ignored", "ignored", Array.Empty<CustomerInsightMetric>());
            var output = Build(request with { InsightReport = request.InsightReport with { Insights = new[] { zeta, alpha } } });
            Require(output.Insights[0].Code == "ALPHA" && output.Insights[1].Code == "ZETA", "insight code ordering drifted");
            Require(output.Insights[1].Metrics[0].Name == "a_metric" && output.Insights[1].Metrics[1].Name == "z_metric", "metric ordering drifted");
        });

        Check("DASH-029", "arbitrary insight prose and delivery metadata do not leak into output", () =>
        {
            const string restrictedMarker = "sensitive-user@example.invalid";
            const string deliveryMarker = "private-delivery-template";
            var request = CreateRequest();
            var insight = request.InsightReport.Insights[0] with { Summary = restrictedMarker, Explanation = restrictedMarker };
            var report = request.Report with { Delivery = new CustomerReportDeliveryMetadata(NotificationChannel.Email, deliveryMarker) };
            var output = Build(request with
            {
                Report = report,
                InsightReport = request.InsightReport with { Insights = new[] { insight } },
            });
            var json = JsonSerializer.Serialize(output);
            Require(!json.Contains(restrictedMarker, StringComparison.Ordinal), "arbitrary insight prose leaked into dashboard output");
            Require(!json.Contains(deliveryMarker, StringComparison.Ordinal), "delivery metadata leaked into dashboard output");
        });

        Check("DASH-030", "dashboard authority is permanently read-only paper informational", () =>
        {
            var authority = Build(CreateRequest()).Authority;
            Require(authority.ReadOnly && authority.PaperOnly && authority.InformationalOnly, "dashboard safe authority flags are missing");
            Require(!authority.UsesLiveMarketData && !authority.CanPlaceOrders && !authority.CanDispatchNotifications &&
                    !authority.CanOverrideRiskPolicy && !authority.IsInvestmentAdvice && !authority.IsSuitabilityDecision,
                "dashboard output gained prohibited authority");
        });

        Check("DASH-031", "public dashboard schema excludes restricted PII and provider fields", () =>
        {
            var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Email", "Phone", "Address", "Cnic", "Passport", "BankAccount", "Iban", "Token", "Secret",
                "Credential", "ProviderReference", "BrokerOrderId", "Delivery", "Positions", "InstrumentReference",
                "Summary", "Explanation",
            };

            foreach (var type in DashboardPayloadTypes())
            {
                foreach (var property in type.GetProperties())
                {
                    Require(!forbidden.Contains(property.Name), $"{type.Name}.{property.Name} is forbidden in dashboard output");
                }
            }
        });

        Check("DASH-032", "builder has no provider database notification or execution constructor dependency", () =>
        {
            var forbiddenFragments = new[] { "Broker", "Provider", "Database", "DbContext", "Notification", "Order", "Execution", "HttpClient" };
            foreach (var constructor in typeof(DeterministicCustomerDashboardBuilder).GetConstructors())
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    var name = parameter.ParameterType.FullName ?? parameter.ParameterType.Name;
                    Require(!forbiddenFragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase)),
                        $"builder constructor gained forbidden dependency {name}");
                }
            }
        });

        Check("DASH-033", "accepted exact-decimal portfolio values are preserved without recomputation", () =>
        {
            var request = CreateRequest();
            var report = request.Report with { Cash = 123.45678901m, InvestedCostBasis = 456.12345678m, BookValue = 579.58024579m };
            var output = Build(request with { Report = report });
            Require(output.Portfolio.Cash == 123.45678901m, "cash precision drifted");
            Require(output.Portfolio.InvestedCostBasis == 456.12345678m, "cost-basis precision drifted");
            Require(output.Portfolio.BookValue == 579.58024579m, "book-value precision drifted");
        });

        Check("DASH-034", "accepted valuation freshness state is propagated", () =>
        {
            var request = CreateRequest();
            var output = Build(request with { InsightReport = request.InsightReport with { ValuationEvidenceFresh = false } });
            Require(!output.ValuationEvidenceFresh, "valuation freshness was silently upgraded");
        });

        Check("DASH-035", "dashboard snapshots isolate insight metric collections from later input mutation", () =>
        {
            var metrics = new List<CustomerInsightMetric> { new("limit_fraction", 0.8m) };
            var insight = new CustomerInsight("LIMIT_NEAR", CustomerInsightSeverity.Attention, "ignored", "ignored", metrics);
            var request = CreateRequest();
            var output = Build(request with { InsightReport = request.InsightReport with { Insights = new[] { insight } } });
            metrics.Add(new CustomerInsightMetric("later_mutation", 1m));
            Require(output.Insights[0].Metrics.Count == 1, "dashboard output retained mutable input metric collection");
        });

        if (failures.Count > 0)
        {
            Console.Error.WriteLine($"Customer dashboard verifier FAILED ({failures.Count}):");
            foreach (var failure in failures)
            {
                Console.Error.WriteLine($" - {failure}");
            }

            return 1;
        }

        Console.WriteLine($"Customer dashboard verifier PASS ({passes}/35).");
        Console.WriteLine("COMPOSITION_ONLY: dashboard consumes accepted Reporting and Insights outputs and does not recompute portfolio valuation, risk, suitability or recommendations.");
        Console.WriteLine("CUSTOMER_SCOPE: authenticated customer identity and account references are validated before output.");
        Console.WriteLine("AUTHORITY_PROPAGATION: unsafe reporting or insight authority fails closed; output is permanently read-only paper informational.");
        Console.WriteLine("SANITIZED_OUTPUT: arbitrary insight prose, delivery metadata, raw positions, restricted PII and provider identifiers are not copied into dashboard payloads.");
        Console.WriteLine("NOT_LIVE: no database, market provider, notification dispatch, broker transport, pyPSX credential, order placement or real-money path is exercised.");
        return 0;

        void Check(string id, string description, Action assertion)
        {
            try
            {
                assertion();
                passes++;
                Console.WriteLine($"PASS {id}: {description}");
            }
            catch (Exception exception)
            {
                failures.Add($"{id}: {exception.GetType().Name}: {exception.Message}");
            }
        }
    }

    private static CustomerDashboardSnapshot Build(CustomerDashboardRequest request) =>
        new DeterministicCustomerDashboardBuilder().Build(request);

    private static CustomerDashboardRequest CreateRequest()
    {
        var report = new CustomerPortfolioReport(
            AccountReference,
            new CustomerReportPeriod(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 14)),
            AsOf.AddMinutes(-5),
            1_000m,
            500m,
            1_500m,
            DefaultPositions,
            20,
            10,
            4,
            new CustomerReportDeliveryMetadata(NotificationChannel.InApp, "paper_dashboard"),
            CustomerReportAuthority.ReadOnlyPaper);

        var insightReport = new CustomerInsightReport(
            AccountReference,
            AsOf.AddMinutes(-2),
            1_600m,
            true,
            DefaultInsights,
            CustomerInsightAuthority.Informational);

        return new CustomerDashboardRequest(
            CustomerId,
            CustomerId,
            AccountReference,
            AsOf,
            report,
            insightReport);
    }

    private static void ExpectThrows<TException>(Action action, string? expectedMessage = null)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            if (expectedMessage is not null)
            {
                Require(exception.Message.Contains(expectedMessage, StringComparison.Ordinal),
                    $"expected message containing '{expectedMessage}', got '{exception.Message}'");
            }

            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name} was not thrown.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static Type[] DashboardPayloadTypes() =>
    [
        typeof(CustomerDashboardSnapshot),
        typeof(CustomerDashboardPortfolioSummary),
        typeof(CustomerDashboardInsight),
        typeof(CustomerDashboardInsightMetric),
    ];

    private static readonly CustomerReportPosition[] DefaultPositions =
    [
        new("HBL", 10m, 20m, 200m),
        new("SYS", 5m, 60m, 300m),
    ];

    private static readonly CustomerInsight[] DefaultInsights =
    [
        new(
            "CONCENTRATION_NEAR_LIMIT",
            CustomerInsightSeverity.Attention,
            "Position concentration is near the configured paper boundary",
            "Deterministic accepted insight prose that must not become an authority source.",
            [new CustomerInsightMetric("limit_fraction", 0.8m)]),
        new(
            "GOAL_BEHIND_PACE",
            CustomerInsightSeverity.Information,
            "Goal progress is behind elapsed-time pace",
            "Deterministic accepted insight prose that remains informational only.",
            [new CustomerInsightMetric("progress_fraction", 0.4m)]),
    ];
}
