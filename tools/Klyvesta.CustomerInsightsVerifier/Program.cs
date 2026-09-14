using Klyvesta.Application.Insights;
using Klyvesta.Domain.Insights;
using Klyvesta.Domain.Risk;

var failures = new List<string>();
var passes = 0;

Run("CI-001", "same inputs produce deterministic insight output", () =>
{
    var engine = new DeterministicCustomerInsightEngine();
    var request = DefaultRequest();
    var first = engine.Generate(request);
    var second = engine.Generate(request);

    AssertEqual(first.AccountReference, second.AccountReference, "account reference");
    AssertEqual(first.AsOf, second.AsOf, "as-of time");
    AssertEqual(first.PortfolioValue, second.PortfolioValue, "portfolio value");
    AssertSequenceEqual(first.Insights.Select(item => item.Code), second.Insights.Select(item => item.Code), "insight codes");
});

Run("CI-002", "single-position concentration is surfaced from fresh evidence", () =>
{
    var request = DefaultRequest(
        cash: 100m,
        positions: new List<RiskValuedPosition>
        {
            Position("AAA", "BANKS", 9m, 100m),
        });
    var report = new DeterministicCustomerInsightEngine().Generate(request);

    var insight = RequireInsight(report, "POSITION_CONCENTRATION");
    AssertEqual(CustomerInsightSeverity.Warning, insight.Severity, "position concentration severity");
    AssertMetric(insight, "position_fraction", 0.9m);
});

Run("CI-003", "sector concentration can trigger without single-position concentration", () =>
{
    var request = DefaultRequest(
        cash: 200m,
        positions: new List<RiskValuedPosition>
        {
            Position("AAA", "BANKS", 3m, 100m),
            Position("BBB", "BANKS", 3m, 100m),
        });
    var report = new DeterministicCustomerInsightEngine().Generate(request);

    RequireInsight(report, "SECTOR_CONCENTRATION");
    Assert(!HasInsight(report, "POSITION_CONCENTRATION"), "single-position warning should remain absent");
});

Run("CI-004", "gross exposure near-limit threshold is deterministic", () =>
{
    var request = DefaultRequest(
        policy: DefaultPolicy() with
        {
            MaxSinglePositionFraction = 1m,
            MaxSectorFraction = 1m,
            MaxGrossExposure = 0.8m,
        },
        cash: 300m,
        positions: new List<RiskValuedPosition>
        {
            Position("AAA", "BANKS", 7m, 100m),
        });
    var report = new DeterministicCustomerInsightEngine().Generate(request);

    var insight = RequireInsight(report, "GROSS_EXPOSURE_NEAR_LIMIT");
    AssertMetric(insight, "gross_exposure_fraction", 0.7m);
});

Run("CI-005", "gross exposure over-limit is reported", () =>
{
    var request = DefaultRequest(
        policy: DefaultPolicy() with
        {
            MaxSinglePositionFraction = 1m,
            MaxSectorFraction = 1m,
            MaxGrossExposure = 0.8m,
        },
        cash: 100m,
        positions: new List<RiskValuedPosition>
        {
            Position("AAA", "BANKS", 9m, 100m),
        });
    var report = new DeterministicCustomerInsightEngine().Generate(request);

    RequireInsight(report, "GROSS_EXPOSURE_OVER_LIMIT");
    Assert(!HasInsight(report, "GROSS_EXPOSURE_NEAR_LIMIT"), "over-limit should replace near-limit signal");
});

Run("CI-006", "drawdown threshold uses supplied current and peak values", () =>
{
    var request = DefaultRequest(cash: 1_000m, peakPortfolioValue: 1_200m);
    var report = new DeterministicCustomerInsightEngine().Generate(request);

    var insight = RequireInsight(report, "DRAWDOWN_ALERT");
    AssertMetric(insight, "drawdown_fraction", 1m / 6m);
});

Run("CI-007", "stale valuation evidence suppresses valuation-based insights", () =>
{
    var request = DefaultRequest(
        cash: 100m,
        positions: new List<RiskValuedPosition>
        {
            Position("AAA", "BANKS", 9m, 100m, DefaultAsOf().AddMinutes(-10)),
        },
        peakPortfolioValue: 1_200m);
    var report = new DeterministicCustomerInsightEngine().Generate(request);

    Assert(!report.ValuationEvidenceFresh, "stale evidence flag");
    RequireInsight(report, "MARKET_EVIDENCE_STALE");
    Assert(!HasInsight(report, "POSITION_CONCENTRATION"), "position insight must be suppressed");
    Assert(!HasInsight(report, "SECTOR_CONCENTRATION"), "sector insight must be suppressed");
    Assert(!HasInsight(report, "GROSS_EXPOSURE_NEAR_LIMIT"), "exposure insight must be suppressed");
    Assert(!HasInsight(report, "GROSS_EXPOSURE_OVER_LIMIT"), "over-limit insight must be suppressed");
    Assert(!HasInsight(report, "DRAWDOWN_ALERT"), "drawdown insight must be suppressed");
});

Run("CI-008", "future market evidence is rejected", () =>
{
    var request = DefaultRequest(
        positions: new List<RiskValuedPosition>
        {
            Position("AAA", "BANKS", 1m, 100m, DefaultAsOf().AddSeconds(1)),
        });

    ExpectInvalidOperation(
        () => new DeterministicCustomerInsightEngine().Generate(request),
        "CUSTOMER_INSIGHT_FUTURE_MARKET_EVIDENCE");
});

Run("CI-009", "duplicate instrument evidence is rejected", () =>
{
    var request = DefaultRequest(
        positions: new List<RiskValuedPosition>
        {
            Position("AAA", "BANKS", 1m, 100m),
            Position("AAA", "BANKS", 2m, 100m),
        });

    ExpectInvalidOperation(
        () => new DeterministicCustomerInsightEngine().Generate(request),
        "CUSTOMER_INSIGHT_DUPLICATE_INSTRUMENT");
});

Run("CI-010", "goal progress behind elapsed-time pace is identified", () =>
{
    var goal = new CustomerGoalProgressInput(
        currentAmount: 100m,
        targetAmount: 1_000m,
        startedOn: new DateOnly(2026, 9, 9),
        targetDate: new DateOnly(2026, 9, 19));
    var report = new DeterministicCustomerInsightEngine().Generate(DefaultRequest(goal: goal));

    var insight = RequireInsight(report, "GOAL_PROGRESS_BEHIND_PACE");
    AssertEqual(CustomerInsightSeverity.Attention, insight.Severity, "goal behind severity");
});

Run("CI-011", "goal progress at or ahead of elapsed-time pace is informational", () =>
{
    var goal = new CustomerGoalProgressInput(
        currentAmount: 600m,
        targetAmount: 1_000m,
        startedOn: new DateOnly(2026, 9, 9),
        targetDate: new DateOnly(2026, 9, 19));
    var report = new DeterministicCustomerInsightEngine().Generate(DefaultRequest(goal: goal));

    var insight = RequireInsight(report, "GOAL_PROGRESS_ON_PACE");
    AssertEqual(CustomerInsightSeverity.Information, insight.Severity, "goal on-pace severity");
});

Run("CI-012", "invalid goal amounts and dates fail closed", () =>
{
    ExpectThrows<ArgumentOutOfRangeException>(() =>
        _ = new CustomerGoalProgressInput(0m, 0m, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2)));
    ExpectThrows<ArgumentException>(() =>
        _ = new CustomerGoalProgressInput(0m, 100m, new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 2)));
});

Run("CI-013", "goal evaluation before configured start date is rejected", () =>
{
    var goal = new CustomerGoalProgressInput(
        100m,
        1_000m,
        new DateOnly(2026, 9, 15),
        new DateOnly(2026, 10, 15));

    ExpectInvalidOperation(
        () => new DeterministicCustomerInsightEngine().Generate(DefaultRequest(goal: goal)),
        "CUSTOMER_INSIGHT_GOAL_NOT_STARTED");
});

Run("CI-014", "recent activity near the risk envelope is surfaced without trading authority", () =>
{
    var activity = new RiskActivityWindow(DefaultAsOf().AddMinutes(-5), 8, 1_000m);
    var report = new DeterministicCustomerInsightEngine().Generate(DefaultRequest(activity: activity));

    var insight = RequireInsight(report, "ACTIVITY_INTENSITY_ELEVATED");
    AssertMetric(insight, "order_count_fraction", 0.8m);
    Assert(report.Authority.InformationalOnly, "activity insight remains informational only");
});

Run("CI-015", "future or negative activity evidence is rejected", () =>
{
    var future = new RiskActivityWindow(DefaultAsOf().AddSeconds(1), 0, 0m);
    ExpectInvalidOperation(
        () => new DeterministicCustomerInsightEngine().Generate(DefaultRequest(activity: future)),
        "CUSTOMER_INSIGHT_FUTURE_ACTIVITY_WINDOW");

    var negative = new RiskActivityWindow(DefaultAsOf().AddMinutes(-1), -1, 0m);
    ExpectThrows<ArgumentOutOfRangeException>(() =>
        _ = new DeterministicCustomerInsightEngine().Generate(DefaultRequest(activity: negative)));
});

Run("CI-016", "customer insight authority is permanently informational and non-executing", () =>
{
    var report = new DeterministicCustomerInsightEngine().Generate(DefaultRequest());

    Assert(report.Authority.InformationalOnly, "informational-only authority");
    Assert(!report.Authority.CanPlaceOrders, "cannot place orders");
    Assert(!report.Authority.CanOverrideRiskPolicy, "cannot override risk policy");
    Assert(!report.Authority.IsSuitabilityDecision, "not a suitability decision");
});

Run("CI-017", "threshold configuration rejects unsupported fractions", () =>
{
    ExpectThrows<ArgumentOutOfRangeException>(() =>
        _ = new CustomerInsightThresholds(0m, 0.8m, 0.05m));
    ExpectThrows<ArgumentOutOfRangeException>(() =>
        _ = new CustomerInsightThresholds(0.1m, 1.01m, 0.05m));
    ExpectThrows<ArgumentOutOfRangeException>(() =>
        _ = new CustomerInsightThresholds(0.1m, 0.8m, -0.01m));
});

Run("CI-018", "peak portfolio value cannot be below current portfolio value", () =>
{
    ExpectInvalidOperation(
        () => new DeterministicCustomerInsightEngine().Generate(DefaultRequest(cash: 1_000m, peakPortfolioValue: 999m)),
        "CUSTOMER_INSIGHT_PEAK_BELOW_CURRENT_VALUE");
});

Run("CI-019", "zero or negative total portfolio value is rejected", () =>
{
    ExpectInvalidOperation(
        () => new DeterministicCustomerInsightEngine().Generate(DefaultRequest(cash: 0m, peakPortfolioValue: 0m)),
        "CUSTOMER_INSIGHT_PORTFOLIO_VALUE_REQUIRED");

    ExpectThrows<ArgumentOutOfRangeException>(() =>
        _ = new DeterministicCustomerInsightEngine().Generate(DefaultRequest(cash: -1m, peakPortfolioValue: 1m)));
});

Run("CI-020", "insight codes are returned in stable ordinal order", () =>
{
    var goal = new CustomerGoalProgressInput(
        100m,
        1_000m,
        new DateOnly(2026, 9, 9),
        new DateOnly(2026, 9, 19));
    var request = DefaultRequest(
        cash: 100m,
        positions: new List<RiskValuedPosition>
        {
            Position("AAA", "BANKS", 9m, 100m),
        },
        activity: new RiskActivityWindow(DefaultAsOf().AddMinutes(-1), 8, 1_000m),
        goal: goal,
        peakPortfolioValue: 1_200m);
    var report = new DeterministicCustomerInsightEngine().Generate(request);
    var codes = report.Insights.Select(item => item.Code).ToArray();
    var sorted = codes.OrderBy(item => item, StringComparer.Ordinal).ToArray();

    AssertSequenceEqual(sorted, codes, "stable ordinal insight ordering");
});

if (failures.Count != 0)
{
    Console.Error.WriteLine($"Customer insight verifier FAILED ({failures.Count}/20): {string.Join(", ", failures)}");
    return 1;
}

Console.WriteLine($"Customer insight verifier PASS ({passes}/20).");
return 0;

void Run(string id, string name, Action action)
{
    try
    {
        action();
        passes++;
        Console.WriteLine($"PASS {id}: {name}");
    }
    catch (Exception exception)
    {
        failures.Add(id);
        Console.Error.WriteLine($"FAIL {id}: {name}: {exception.Message}");
    }
}

static CustomerInsightRequest DefaultRequest(
    PaperRiskPolicy? policy = null,
    decimal cash = 1_000m,
    IReadOnlyList<RiskValuedPosition>? positions = null,
    RiskActivityWindow? activity = null,
    CustomerGoalProgressInput? goal = null,
    decimal? peakPortfolioValue = null) =>
    new(
        new RiskPortfolioSnapshot(
            "KLY-INSIGHT-001",
            cash,
            positions ?? new List<RiskValuedPosition>()),
        policy ?? DefaultPolicy(),
        activity ?? new RiskActivityWindow(DefaultAsOf().AddMinutes(-5), 1, 100m),
        new CustomerInsightThresholds(0.10m, 0.80m, 0.05m),
        peakPortfolioValue ?? cash + (positions?.Sum(position => position.Quantity * position.MarketPrice) ?? 0m),
        goal,
        DefaultAsOf());

static PaperRiskPolicy DefaultPolicy() =>
    new(
        Version: "insight-policy-v1",
        AllowedInstrumentReferences: new HashSet<string>(StringComparer.Ordinal) { "AAA", "BBB", "CCC" },
        MaxMarketDataAge: TimeSpan.FromMinutes(5),
        MinimumDailyTradedValue: 1m,
        MaxOrderNotional: 100_000m,
        MaxSinglePositionFraction: 0.40m,
        MaxSectorFraction: 0.60m,
        MaxGrossExposure: 1.00m,
        MaxOrdersPerWindow: 10,
        MaxTurnoverPerWindow: 10_000m,
        ActivityWindow: TimeSpan.FromHours(1),
        KillSwitchEnabled: false);

static RiskValuedPosition Position(
    string instrument,
    string sector,
    decimal quantity,
    decimal price,
    DateTimeOffset? observedAt = null) =>
    new(instrument, sector, quantity, price, observedAt ?? DefaultAsOf().AddMinutes(-1));

static DateTimeOffset DefaultAsOf() =>
    new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

static CustomerInsight RequireInsight(CustomerInsightReport report, string code) =>
    report.Insights.FirstOrDefault(item => StringComparer.Ordinal.Equals(item.Code, code))
    ?? throw new InvalidOperationException($"ASSERTION_FAILED: missing insight {code}");

static bool HasInsight(CustomerInsightReport report, string code) =>
    report.Insights.Any(item => StringComparer.Ordinal.Equals(item.Code, code));

static void AssertMetric(CustomerInsight insight, string name, decimal expected)
{
    var metric = insight.Metrics.FirstOrDefault(item => StringComparer.Ordinal.Equals(item.Name, name))
        ?? throw new InvalidOperationException($"ASSERTION_FAILED: missing metric {name}");
    AssertEqual(expected, metric.Value, $"metric {name}");
}

static void ExpectInvalidOperation(Action action, string expectedMessage)
{
    try
    {
        action();
    }
    catch (InvalidOperationException exception) when (StringComparer.Ordinal.Equals(exception.Message, expectedMessage))
    {
        return;
    }

    throw new InvalidOperationException($"Expected InvalidOperationException with message {expectedMessage}.");
}

static void ExpectThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected exception {typeof(TException).Name}.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException($"ASSERTION_FAILED:{message}");
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
    where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"ASSERTION_FAILED:{message}: expected={expected}, actual={actual}");
    }
}

static void AssertSequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException($"ASSERTION_FAILED:{message}");
    }
}
