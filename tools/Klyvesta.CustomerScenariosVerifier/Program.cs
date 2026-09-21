using Klyvesta.Application.Scenarios;
using Klyvesta.Domain.Portfolios;
using Klyvesta.Domain.Risk;
using Klyvesta.Domain.Scenarios;

var failures = new List<string>();
var passes = 0;
var engine = new DeterministicCustomerScenarioEngine();
var customerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
var otherCustomerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
var evaluatedAt = new DateTimeOffset(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);

Check("SCN-001", "same inputs produce deterministic scenario output", () =>
{
    var first = engine.Evaluate(CreateRequest());
    var second = engine.Evaluate(CreateRequest());
    Require(first.CustomerId == second.CustomerId, "customer id must be deterministic");
    Require(first.CurrentTotalValue == second.CurrentTotalValue, "current total must be deterministic");
    Require(first.ShockedTotalValue == second.ShockedTotalValue, "shocked total must be deterministic");
    Require(first.AbsoluteImpact == second.AbsoluteImpact, "impact must be deterministic");
    Require(first.Positions.SequenceEqual(second.Positions), "position results must be deterministic");
});

Check("SCN-002", "cross-customer request fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => engine.Evaluate(CreateRequest(authenticatedCustomerId: otherCustomerId)),
        "CUSTOMER_SCENARIO_SCOPE_MISMATCH");
});

Check("SCN-003", "missing authenticated customer id is rejected", () =>
{
    RequireThrows<ArgumentException>(() => engine.Evaluate(CreateRequest(authenticatedCustomerId: Guid.Empty)));
});

Check("SCN-004", "missing customer id is rejected", () =>
{
    RequireThrows<ArgumentException>(() => engine.Evaluate(CreateRequest(requestCustomerId: Guid.Empty)));
});

Check("SCN-005", "portfolio account mismatch fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => engine.Evaluate(CreateRequest(portfolio: CreatePortfolio(accountReference: "PAPER-OTHER"))),
        "CUSTOMER_SCENARIO_PORTFOLIO_ACCOUNT_SCOPE_MISMATCH");
});

Check("SCN-006", "risk account mismatch fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => engine.Evaluate(CreateRequest(riskContext: CreateRisk(accountReference: "PAPER-OTHER"))),
        "CUSTOMER_SCENARIO_RISK_ACCOUNT_SCOPE_MISMATCH");
});

Check("SCN-007", "cash context mismatch fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => engine.Evaluate(CreateRequest(riskContext: CreateRisk(cash: 499m))),
        "CUSTOMER_SCENARIO_CASH_CONTEXT_MISMATCH");
});

Check("SCN-008", "duplicate paper portfolio instrument is rejected", () =>
{
    var positions = new List<ProjectedPosition>
    {
        new("HBL", 10m, 20m),
        new(" HBL ", 1m, 10m),
    };
    RequireThrows<InvalidOperationException>(
        () => engine.Evaluate(CreateRequest(portfolio: CreatePortfolio(positions: positions))),
        "CUSTOMER_SCENARIO_PORTFOLIO_DUPLICATE_INSTRUMENT");
});

Check("SCN-009", "duplicate risk instrument is rejected", () =>
{
    var positions = new List<RiskValuedPosition>
    {
        RiskPosition("HBL", "BANKS", 10m, 100m),
        RiskPosition(" HBL ", "BANKS", 10m, 100m),
    };
    RequireThrows<InvalidOperationException>(
        () => engine.Evaluate(CreateRequest(riskContext: CreateRisk(positions: positions))),
        "CUSTOMER_SCENARIO_RISK_DUPLICATE_INSTRUMENT");
});

Check("SCN-010", "missing risk position fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => engine.Evaluate(CreateRequest(riskContext: CreateRisk(positions: new List<RiskValuedPosition>()))),
        "CUSTOMER_SCENARIO_POSITION_SET_MISMATCH");
});

Check("SCN-011", "unexpected risk position fails closed", () =>
{
    var positions = new List<RiskValuedPosition>
    {
        RiskPosition("HBL", "BANKS", 10m, 100m),
        RiskPosition("SYS", "TECH", 1m, 50m),
    };
    RequireThrows<InvalidOperationException>(
        () => engine.Evaluate(CreateRequest(riskContext: CreateRisk(positions: positions))),
        "CUSTOMER_SCENARIO_POSITION_SET_MISMATCH");
});

Check("SCN-012", "risk quantity mismatch fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => engine.Evaluate(CreateRequest(riskContext: CreateRisk(
            positions: new List<RiskValuedPosition> { RiskPosition("HBL", "BANKS", 9m, 100m) }))),
        "CUSTOMER_SCENARIO_POSITION_QUANTITY_MISMATCH");
});

Check("SCN-013", "non-positive portfolio quantity is rejected", () =>
{
    RequireThrows<ArgumentOutOfRangeException>(() =>
        engine.Evaluate(CreateRequest(portfolio: CreatePortfolio(
            positions: new List<ProjectedPosition> { new("HBL", 0m, 20m) }))));
});

Check("SCN-014", "non-positive portfolio average cost is rejected", () =>
{
    RequireThrows<ArgumentOutOfRangeException>(() =>
        engine.Evaluate(CreateRequest(portfolio: CreatePortfolio(
            positions: new List<ProjectedPosition> { new("HBL", 10m, 0m) }))));
});

Check("SCN-015", "non-positive risk market price is rejected", () =>
{
    RequireThrows<ArgumentOutOfRangeException>(() =>
        engine.Evaluate(CreateRequest(riskContext: CreateRisk(
            positions: new List<RiskValuedPosition> { RiskPosition("HBL", "BANKS", 10m, 0m) }))));
});

Check("SCN-016", "missing risk sector is rejected", () =>
{
    RequireThrows<ArgumentException>(() =>
        engine.Evaluate(CreateRequest(riskContext: CreateRisk(
            positions: new List<RiskValuedPosition> { RiskPosition("HBL", " ", 10m, 100m) }))));
});

Check("SCN-017", "future risk evidence fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => engine.Evaluate(CreateRequest(riskContext: CreateRisk(
            positions: new List<RiskValuedPosition>
            {
                RiskPosition("HBL", "BANKS", 10m, 100m, evaluatedAt.AddSeconds(1)),
            }))),
        "CUSTOMER_SCENARIO_RISK_CONTEXT_STALE_OR_FUTURE");
});

Check("SCN-018", "stale risk evidence fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => engine.Evaluate(CreateRequest(riskContext: CreateRisk(
            positions: new List<RiskValuedPosition>
            {
                RiskPosition("HBL", "BANKS", 10m, 100m, evaluatedAt.AddMinutes(-11)),
            }))),
        "CUSTOMER_SCENARIO_RISK_CONTEXT_STALE_OR_FUTURE");
});

Check("SCN-019", "non-positive risk age boundary is rejected", () =>
{
    RequireThrows<ArgumentOutOfRangeException>(() =>
        engine.Evaluate(CreateRequest(maximumRiskContextAge: TimeSpan.Zero)));
});

Check("SCN-020", "market shock below negative one is rejected", () =>
{
    RequireThrows<ArgumentOutOfRangeException>(() =>
        engine.Evaluate(CreateRequest(shock: Shock(-1.01m))));
});

Check("SCN-021", "market shock above positive one is rejected", () =>
{
    RequireThrows<ArgumentOutOfRangeException>(() =>
        engine.Evaluate(CreateRequest(shock: Shock(1.01m))));
});

Check("SCN-022", "sector shock outside bounds is rejected", () =>
{
    RequireThrows<ArgumentOutOfRangeException>(() =>
        engine.Evaluate(CreateRequest(shock: Shock(-0.10m, new Dictionary<string, decimal>
        {
            ["BANKS"] = -1.01m,
        }))));
});

Check("SCN-023", "blank sector shock key is rejected", () =>
{
    RequireThrows<ArgumentException>(() =>
        engine.Evaluate(CreateRequest(shock: Shock(-0.10m, new Dictionary<string, decimal>
        {
            [" "] = -0.20m,
        }))));
});

Check("SCN-024", "market shock arithmetic is exact decimal math", () =>
{
    var result = engine.Evaluate(CreateRequest(shock: Shock(-0.10m)));
    var position = result.Positions.Single();
    Require(position.CurrentPrice == 100m, "fixture current price must be 100");
    Require(position.ShockedPrice == 90m, "10% negative shock must produce exact price 90");
    Require(position.CurrentValue == 1000m, "10 x 100 current value must equal 1000");
    Require(position.ShockedValue == 900m, "10 x 90 shocked value must equal 900");
    Require(position.ValueImpact == -100m, "position impact must equal -100");
});

Check("SCN-025", "sector shock overrides market shock deterministically", () =>
{
    var result = engine.Evaluate(CreateRequest(shock: Shock(-0.10m, new Dictionary<string, decimal>
    {
        ["BANKS"] = -0.25m,
    })));
    var position = result.Positions.Single();
    Require(position.AppliedShockFraction == -0.25m, "sector override must win over market shock");
    Require(position.ShockedPrice == 75m, "25% negative sector shock must produce price 75");
});

Check("SCN-026", "paper cash remains unchanged by scenario", () =>
{
    var result = engine.Evaluate(CreateRequest(shock: Shock(-0.50m)));
    Require(result.CurrentCash == 500m, "current cash must come from paper portfolio");
    Require(result.ShockedTotalValue == 1000m, "500 cash plus shocked 500 invested value must equal 1000");
});

Check("SCN-027", "current total uses risk market context not portfolio average cost", () =>
{
    var result = engine.Evaluate(CreateRequest());
    Require(result.CurrentInvestedValue == 1000m, "market-valued position must be 1000");
    Require(result.CurrentTotalValue == 1500m, "500 cash plus 1000 market value must equal 1500");
});

Check("SCN-028", "impact fraction is deterministic against current total", () =>
{
    var result = engine.Evaluate(CreateRequest(shock: Shock(-0.10m)));
    Require(result.AbsoluteImpact == -100m, "scenario impact must equal -100");
    Require(result.ImpactFraction == (-100m / 1500m), "impact fraction must use exact current total denominator");
});

Check("SCN-029", "position output ordering is stable by instrument", () =>
{
    var portfolio = CreatePortfolio(
        positions: new List<ProjectedPosition>
        {
            new("SYS", 2m, 30m),
            new("HBL", 10m, 20m),
        });
    var risk = CreateRisk(
        positions: new List<RiskValuedPosition>
        {
            RiskPosition("SYS", "TECH", 2m, 50m),
            RiskPosition("HBL", "BANKS", 10m, 100m),
        });
    var result = engine.Evaluate(CreateRequest(portfolio: portfolio, riskContext: risk));
    Require(result.Positions.Select(static position => position.InstrumentReference)
        .SequenceEqual(new List<string> { "HBL", "SYS" }), "scenario positions must use stable ordinal instrument order");
});

Check("SCN-030", "risk basis is explicit and existing-context only", () =>
{
    var result = engine.Evaluate(CreateRequest());
    Require(result.RiskBasis == CustomerScenarioRiskBasis.ExistingRiskPortfolioSnapshot,
        "scenario must explicitly use the caller-provided existing risk portfolio snapshot");
});

Check("SCN-031", "scenario authority is informational and non-executing", () =>
{
    var authority = engine.Evaluate(CreateRequest()).Authority;
    Require(authority.InformationalOnly, "scenario output must be informational");
    Require(!authority.CanRecommendInvestments, "scenario engine cannot recommend investments");
    Require(!authority.CanPlaceOrders, "scenario engine cannot place orders");
    Require(!authority.CanMoveMoney, "scenario engine cannot move money");
    Require(!authority.CanOverrideRisk, "scenario engine cannot override risk authority");
});

Check("SCN-032", "scenario result schema exposes no advice or execution fields", () =>
{
    var forbiddenFragments = new List<string>
    {
        "Recommendation",
        "SuggestedInstrument",
        "TargetAllocation",
        "Order",
        "Broker",
        "PyPsx",
        "Provider",
        "Execution",
        "TradeInstruction",
    };
    var names = typeof(CustomerScenarioResult).GetProperties().Select(static property => property.Name).ToArray();
    foreach (var fragment in forbiddenFragments)
    {
        Require(!names.Any(name => name.Contains(fragment, StringComparison.OrdinalIgnoreCase)),
            $"scenario result must not expose authority-bearing field fragment '{fragment}'");
    }
});

Check("SCN-033", "scenario engine has no provider database or execution dependency", () =>
{
    var constructors = typeof(DeterministicCustomerScenarioEngine).GetConstructors();
    Require(constructors.Length == 1, "scenario engine must expose exactly one public constructor");
    Require(constructors[0].GetParameters().Length == 0,
        "scenario engine must not depend on provider/database/execution infrastructure");
});

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Customer scenarios verification FAILED ({failures.Count}):");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($" - {failure}");
    }

    return 1;
}

Console.WriteLine($"Customer scenarios verifier PASS ({passes}/33).");
Console.WriteLine("DETERMINISTIC_SHOCKS: exact-decimal market/sector shocks are applied to caller-provided paper risk prices.");
Console.WriteLine("CUSTOMER_ACCOUNT_SCOPE: authenticated customer and paper/risk account contexts fail closed on mismatch.");
Console.WriteLine("RISK_CONTEXT_ONLY: scenario evaluation consumes existing bounded risk context and cannot fetch or override live risk authority.");
Console.WriteLine("NO_ADVICE_NO_TRADING: scenario output is informational and cannot recommend securities, place orders, move money or override risk.");
Console.WriteLine("NOT_LIVE: no API, database, provider credential, live market subscription, pyPSX transport or real-money path is exercised.");
return 0;

void Check(string id, string description, Action assertion)
{
    try
    {
        assertion();
        passes++;
        Console.WriteLine($"SCENARIO_PASS {id} {description}");
    }
    catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
    {
        failures.Add($"{id}: {exception.Message}");
    }
}

void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

void RequireThrows<TException>(Action action, string? expectedMessage = null)
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
                $"expected exception containing '{expectedMessage}', received '{exception.Message}'");
        }

        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name} was not thrown.");
}

CustomerScenarioRequest CreateRequest(
    Guid? authenticatedCustomerId = null,
    Guid? requestCustomerId = null,
    string accountReference = "PAPER-001",
    PortfolioProjectionSnapshot? portfolio = null,
    RiskPortfolioSnapshot? riskContext = null,
    CustomerScenarioShock? shock = null,
    TimeSpan? maximumRiskContextAge = null)
{
    var owner = requestCustomerId ?? customerId;
    return new CustomerScenarioRequest(
        authenticatedCustomerId ?? owner,
        owner,
        accountReference,
        evaluatedAt,
        maximumRiskContextAge ?? TimeSpan.FromMinutes(10),
        portfolio ?? CreatePortfolio(),
        riskContext ?? CreateRisk(),
        shock ?? Shock(-0.10m));
}

PortfolioProjectionSnapshot CreatePortfolio(
    decimal cash = 500m,
    IReadOnlyList<ProjectedPosition>? positions = null,
    string accountReference = "PAPER-001") =>
    new(
        accountReference,
        cash,
        positions ?? new List<ProjectedPosition> { new("HBL", 10m, 20m) },
        LastSequence: 2,
        UniqueSourceEventCount: 2,
        UniqueExecutionCount: 1);

RiskPortfolioSnapshot CreateRisk(
    decimal cash = 500m,
    IReadOnlyList<RiskValuedPosition>? positions = null,
    string accountReference = "PAPER-001") =>
    new(
        accountReference,
        cash,
        positions ?? new List<RiskValuedPosition> { RiskPosition("HBL", "BANKS", 10m, 100m) });

RiskValuedPosition RiskPosition(
    string instrumentReference,
    string sectorReference,
    decimal quantity,
    decimal marketPrice,
    DateTimeOffset? observedAt = null) =>
    new(
        instrumentReference,
        sectorReference,
        quantity,
        marketPrice,
        observedAt ?? evaluatedAt.AddMinutes(-1));

CustomerScenarioShock Shock(
    decimal marketShock,
    IReadOnlyDictionary<string, decimal>? sectorShocks = null) =>
    new(
        marketShock,
        sectorShocks ?? new Dictionary<string, decimal>(StringComparer.Ordinal));
