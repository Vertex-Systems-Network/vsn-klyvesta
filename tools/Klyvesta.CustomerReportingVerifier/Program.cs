using System.Reflection;
using Klyvesta.Application.Reporting;
using Klyvesta.Domain.Notifications;
using Klyvesta.Domain.Portfolios;
using Klyvesta.Domain.Reporting;

var failures = new List<string>();
var passes = 0;

Run("CR-001", "same inputs produce structurally deterministic reports", () =>
{
    var builder = new DeterministicCustomerReportBuilder();
    var request = DefaultRequest();
    var first = builder.Build(request);
    var second = builder.Build(request);

    AssertEqual(first.AccountReference, second.AccountReference, "account reference");
    AssertEqual(first.Period, second.Period, "period");
    AssertEqual(first.GeneratedAt, second.GeneratedAt, "generated at");
    AssertEqual(first.Cash, second.Cash, "cash");
    AssertEqual(first.InvestedCostBasis, second.InvestedCostBasis, "invested cost basis");
    AssertEqual(first.BookValue, second.BookValue, "book value");
    AssertSequenceEqual(first.Positions, second.Positions, "positions");
    AssertEqual(first.LastProjectionSequence, second.LastProjectionSequence, "last sequence");
    AssertEqual(first.SourceEventCount, second.SourceEventCount, "source event count");
    AssertEqual(first.ExecutionCount, second.ExecutionCount, "execution count");
    AssertEqual(first.Delivery, second.Delivery, "delivery metadata");
    AssertEqual(first.Authority, second.Authority, "authority");
});

Run("CR-002", "cash-only paper portfolio produces cash book value", () =>
{
    var report = new DeterministicCustomerReportBuilder().Build(DefaultRequest(cash: 2_500m));

    AssertEqual(2_500m, report.Cash, "cash");
    AssertEqual(0m, report.InvestedCostBasis, "invested cost basis");
    AssertEqual(2_500m, report.BookValue, "book value");
    AssertEqual(0, report.Positions.Count, "position count");
});

Run("CR-003", "positions are ordinally sorted and cost basis is exact", () =>
{
    var positions = new List<ProjectedPosition>
    {
        new("ZZZ", 2m, 125m),
        new("AAA", 3m, 50m),
    };
    var report = new DeterministicCustomerReportBuilder().Build(DefaultRequest(cash: 100m, positions: positions));
    var expectedOrder = new List<string> { "AAA", "ZZZ" };

    AssertSequenceEqual(expectedOrder, report.Positions.Select(position => position.InstrumentReference), "position ordering");
    AssertEqual(150m, report.Positions[0].CostBasis, "AAA cost basis");
    AssertEqual(250m, report.Positions[1].CostBasis, "ZZZ cost basis");
    AssertEqual(400m, report.InvestedCostBasis, "total invested cost basis");
    AssertEqual(500m, report.BookValue, "book value");
});

Run("CR-004", "projection counters are preserved", () =>
{
    var report = new DeterministicCustomerReportBuilder().Build(DefaultRequest(
        lastSequence: 9,
        sourceEvents: 10,
        executions: 7));

    AssertEqual(9L, report.LastProjectionSequence, "last sequence");
    AssertEqual(10, report.SourceEventCount, "source event count");
    AssertEqual(7, report.ExecutionCount, "execution count");
});

Run("CR-005", "authenticated customer mismatch fails closed", () =>
{
    var request = DefaultRequest() with { AuthenticatedCustomerId = Guid.Parse("22222222-2222-2222-2222-222222222222") };
    ExpectInvalidOperation(
        () => new DeterministicCustomerReportBuilder().Build(request),
        "CUSTOMER_REPORT_SCOPE_MISMATCH");
});

Run("CR-006", "missing authenticated customer id is rejected", () =>
{
    var request = DefaultRequest() with { AuthenticatedCustomerId = Guid.Empty };
    ExpectThrows<ArgumentException>(() => new DeterministicCustomerReportBuilder().Build(request));
});

Run("CR-007", "missing customer id is rejected", () =>
{
    var request = DefaultRequest() with { CustomerId = Guid.Empty };
    ExpectThrows<ArgumentException>(() => new DeterministicCustomerReportBuilder().Build(request));
});

Run("CR-008", "account scope mismatch fails closed", () =>
{
    var request = DefaultRequest() with { AccountReference = "KLY-OTHER" };
    ExpectInvalidOperation(
        () => new DeterministicCustomerReportBuilder().Build(request),
        "CUSTOMER_REPORT_ACCOUNT_SCOPE_MISMATCH");
});

Run("CR-009", "invalid report period is rejected", () =>
{
    var request = DefaultRequest() with
    {
        Period = new CustomerReportPeriod(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 13)),
    };
    ExpectThrows<ArgumentException>(() => new DeterministicCustomerReportBuilder().Build(request));
});

Run("CR-010", "future report period fails closed", () =>
{
    var request = DefaultRequest() with
    {
        Period = new CustomerReportPeriod(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 15)),
    };
    ExpectInvalidOperation(
        () => new DeterministicCustomerReportBuilder().Build(request),
        "CUSTOMER_REPORT_PERIOD_IN_FUTURE");
});

Run("CR-011", "unknown notification channel is rejected without dispatching", () =>
{
    var request = DefaultRequest() with
    {
        Delivery = new CustomerReportDeliveryMetadata(NotificationChannel.Unknown, "paper-summary"),
    };
    ExpectThrows<ArgumentException>(() => new DeterministicCustomerReportBuilder().Build(request));
});

Run("CR-012", "delivery template key is normalized deterministically", () =>
{
    var request = DefaultRequest() with
    {
        Delivery = new CustomerReportDeliveryMetadata(NotificationChannel.InApp, "  paper-summary  "),
    };
    var report = new DeterministicCustomerReportBuilder().Build(request);

    AssertEqual("paper-summary", report.Delivery.TemplateKey, "normalized template key");
    AssertEqual(NotificationChannel.InApp, report.Delivery.Channel, "delivery channel");
});

Run("CR-013", "overlong delivery template key is rejected", () =>
{
    var request = DefaultRequest() with
    {
        Delivery = new CustomerReportDeliveryMetadata(NotificationChannel.Email, new string('x', 101)),
    };
    ExpectThrows<ArgumentOutOfRangeException>(() => new DeterministicCustomerReportBuilder().Build(request));
});

Run("CR-014", "negative paper cash is rejected", () =>
{
    ExpectThrows<ArgumentOutOfRangeException>(() =>
        new DeterministicCustomerReportBuilder().Build(DefaultRequest(cash: -1m)));
});

Run("CR-015", "duplicate instruments are rejected", () =>
{
    var positions = new List<ProjectedPosition>
    {
        new("AAA", 1m, 10m),
        new("AAA", 2m, 10m),
    };
    ExpectInvalidOperation(
        () => new DeterministicCustomerReportBuilder().Build(DefaultRequest(positions: positions)),
        "CUSTOMER_REPORT_DUPLICATE_INSTRUMENT");
});

Run("CR-016", "non-positive paper position values are rejected", () =>
{
    var zeroQuantity = new List<ProjectedPosition> { new("AAA", 0m, 10m) };
    ExpectThrows<ArgumentOutOfRangeException>(() =>
        new DeterministicCustomerReportBuilder().Build(DefaultRequest(positions: zeroQuantity)));

    var zeroCost = new List<ProjectedPosition> { new("AAA", 1m, 0m) };
    ExpectThrows<ArgumentOutOfRangeException>(() =>
        new DeterministicCustomerReportBuilder().Build(DefaultRequest(positions: zeroCost)));
});

Run("CR-017", "invalid projection counters are rejected", () =>
{
    ExpectThrows<ArgumentOutOfRangeException>(() =>
        new DeterministicCustomerReportBuilder().Build(DefaultRequest(lastSequence: -2)));
    ExpectInvalidOperation(
        () => new DeterministicCustomerReportBuilder().Build(DefaultRequest(sourceEvents: 1, executions: 2)),
        "CUSTOMER_REPORT_EXECUTION_COUNT_INVALID");
});

Run("CR-018", "report authority is paper-only and non-executing", () =>
{
    var report = new DeterministicCustomerReportBuilder().Build(DefaultRequest());

    Assert(report.Authority.PaperOnly, "paper-only authority");
    Assert(!report.Authority.UsesLiveMarketData, "no live market data");
    Assert(!report.Authority.CanPlaceOrders, "cannot place orders");
    Assert(!report.Authority.CanDispatchNotifications, "cannot dispatch notifications");
    Assert(!report.Authority.IsInvestmentAdvice, "not investment advice");
});

Run("CR-019", "report output schema excludes restricted PII fields", () =>
{
    var outputTypes = new List<Type>
    {
        typeof(CustomerPortfolioReport),
        typeof(CustomerReportPosition),
        typeof(CustomerReportDeliveryMetadata),
        typeof(CustomerReportAuthority),
    };
    var prohibitedFragments = new List<string>
    {
        "cnic",
        "passport",
        "biometric",
        "iban",
        "bankaccount",
        "emailaddress",
        "phonenumber",
        "postaladdress",
    };

    foreach (var type in outputTypes)
    {
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var normalized = property.Name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
            Assert(!prohibitedFragments.Any(fragment => normalized.Contains(fragment, StringComparison.Ordinal)),
                $"restricted PII-like output field {type.Name}.{property.Name}");
        }
    }
});

Run("CR-020", "report schema contains no provider, broker or market-price output authority", () =>
{
    var propertyNames = typeof(CustomerPortfolioReport)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Select(property => property.Name)
        .Concat(typeof(CustomerReportPosition).GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(property => property.Name))
        .ToArray();

    Assert(!propertyNames.Any(name => name.Contains("Provider", StringComparison.OrdinalIgnoreCase)), "no provider field");
    Assert(!propertyNames.Any(name => name.Contains("Broker", StringComparison.OrdinalIgnoreCase)), "no broker field");
    Assert(!propertyNames.Any(name => name.Contains("MarketPrice", StringComparison.OrdinalIgnoreCase)), "no market-price field");
});

Run("CR-021", "same-day period is allowed", () =>
{
    var request = DefaultRequest() with
    {
        Period = new CustomerReportPeriod(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 14)),
    };
    var report = new DeterministicCustomerReportBuilder().Build(request);
    AssertEqual(request.Period, report.Period, "same-day report period");
});

Run("CR-022", "empty paper projection can be represented without live valuation", () =>
{
    var report = new DeterministicCustomerReportBuilder().Build(DefaultRequest(
        cash: 0m,
        positions: Array.Empty<ProjectedPosition>(),
        lastSequence: -1,
        sourceEvents: 0,
        executions: 0));

    AssertEqual(0m, report.BookValue, "empty book value");
    AssertEqual(0, report.Positions.Count, "empty positions");
    Assert(report.Authority.PaperOnly, "empty report remains paper only");
});

Run("CR-023", "instrument identity is normalized before duplicate detection", () =>
{
    var positions = new List<ProjectedPosition>
    {
        new("AAA", 1m, 10m),
        new("  AAA  ", 2m, 10m),
    };
    ExpectInvalidOperation(
        () => new DeterministicCustomerReportBuilder().Build(DefaultRequest(positions: positions)),
        "CUSTOMER_REPORT_DUPLICATE_INSTRUMENT");
});

if (failures.Count != 0)
{
    Console.Error.WriteLine($"Customer reporting verifier FAILED ({failures.Count}/23): {string.Join(", ", failures)}");
    return 1;
}

Console.WriteLine($"Customer reporting verifier PASS ({passes}/23).");
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

static CustomerReportRequest DefaultRequest(
    decimal cash = 1_000m,
    IReadOnlyList<ProjectedPosition>? positions = null,
    long lastSequence = 3,
    int sourceEvents = 4,
    int executions = 2)
{
    var customerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    return new CustomerReportRequest(
        customerId,
        customerId,
        "KLY-REPORT-001",
        new CustomerReportPeriod(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 14)),
        new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero),
        new PortfolioProjectionSnapshot(
            "KLY-REPORT-001",
            cash,
            positions ?? Array.Empty<ProjectedPosition>(),
            lastSequence,
            sourceEvents,
            executions),
        new CustomerReportDeliveryMetadata(NotificationChannel.InApp, "paper-portfolio-summary"));
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
