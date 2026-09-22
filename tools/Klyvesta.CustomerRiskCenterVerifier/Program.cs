using Klyvesta.Application.RiskCenter;
using Klyvesta.Domain.Customers;
using Klyvesta.Domain.Portfolios;
using Klyvesta.Domain.Risk;
using Klyvesta.Domain.RiskCenter;

var failures = new List<string>();
var passes = 0;
var builder = new DeterministicCustomerRiskCenterBuilder();
var customerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
var otherCustomerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
var evaluatedAt = new DateTimeOffset(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);

Check("RKC-001", "same inputs produce deterministic risk-center output", () =>
{
    var first = builder.Build(CreateRequest());
    var second = builder.Build(CreateRequest());
    Require(first.CustomerId == second.CustomerId, "customer id must be deterministic");
    Require(first.TotalMarketValue == second.TotalMarketValue, "total market value must be deterministic");
    Require(first.GrossExposureFraction == second.GrossExposureFraction, "gross exposure must be deterministic");
    Require(first.Positions.SequenceEqual(second.Positions), "position exposures must be deterministic");
    Require(first.Sectors.SequenceEqual(second.Sectors), "sector exposures must be deterministic");
    Require(first.Signals.SequenceEqual(second.Signals), "signals must be deterministic");
});

Check("RKC-002", "cross-customer request fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(authenticatedCustomerId: otherCustomerId)),
        "CUSTOMER_RISK_CENTER_SCOPE_MISMATCH");
});

Check("RKC-003", "missing authenticated customer id is rejected", () =>
{
    RequireThrows<ArgumentException>(
        () => builder.Build(CreateRequest(authenticatedCustomerId: Guid.Empty)));
});

Check("RKC-004", "missing customer id is rejected", () =>
{
    RequireThrows<ArgumentException>(
        () => builder.Build(CreateRequest(requestCustomerId: Guid.Empty)));
});

Check("RKC-005", "blank account reference is rejected", () =>
{
    RequireThrows<ArgumentException>(() => builder.Build(CreateRequest(accountReference: " ")));
});

Check("RKC-006", "portfolio account mismatch fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(portfolio: CreatePortfolio(accountReference: "PAPER-OTHER"))),
        "CUSTOMER_RISK_CENTER_PORTFOLIO_ACCOUNT_SCOPE_MISMATCH");
});

Check("RKC-007", "risk account mismatch fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(riskContext: CreateRisk(accountReference: "PAPER-OTHER"))),
        "CUSTOMER_RISK_CENTER_RISK_ACCOUNT_SCOPE_MISMATCH");
});

Check("RKC-008", "risk profile customer mismatch fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(riskProfile: CreateRiskProfile(owner: otherCustomerId))),
        "CUSTOMER_RISK_CENTER_RISK_PROFILE_SCOPE_MISMATCH");
});

Check("RKC-009", "stale risk profile evidence fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(
            riskProfile: CreateRiskProfile(updatedAt: evaluatedAt.AddDays(-31)),
            maximumRiskProfileAge: TimeSpan.FromDays(30))),
        "CUSTOMER_RISK_CENTER_RISK_PROFILE_STALE_OR_FUTURE");
});

Check("RKC-010", "future risk profile evidence fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(riskProfile: CreateRiskProfile(updatedAt: evaluatedAt.AddSeconds(1)))),
        "CUSTOMER_RISK_CENTER_RISK_PROFILE_STALE_OR_FUTURE");
});

Check("RKC-011", "non-positive risk profile age boundary is rejected", () =>
{
    RequireThrows<ArgumentOutOfRangeException>(
        () => builder.Build(CreateRequest(maximumRiskProfileAge: TimeSpan.Zero)));
});

Check("RKC-012", "non-positive risk context age boundary is rejected", () =>
{
    RequireThrows<ArgumentOutOfRangeException>(
        () => builder.Build(CreateRequest(maximumRiskContextAge: TimeSpan.Zero)));
});

Check("RKC-013", "future risk position evidence fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(riskContext: CreateRisk(
            positions: RiskPositions(RiskPosition("HBL", "BANKS", 10m, 100m, evaluatedAt.AddSeconds(1)))))),
        "CUSTOMER_RISK_CENTER_RISK_CONTEXT_STALE_OR_FUTURE");
});

Check("RKC-014", "stale risk position evidence fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(
            riskContext: CreateRisk(
                positions: RiskPositions(RiskPosition("HBL", "BANKS", 10m, 100m, evaluatedAt.AddMinutes(-11)))),
            maximumRiskContextAge: TimeSpan.FromMinutes(10))),
        "CUSTOMER_RISK_CENTER_RISK_CONTEXT_STALE_OR_FUTURE");
});

Check("RKC-015", "portfolio and risk cash mismatch fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(riskContext: CreateRisk(cash: 499m))),
        "CUSTOMER_RISK_CENTER_CASH_CONTEXT_MISMATCH");
});

Check("RKC-016", "missing risk position fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(riskContext: CreateRisk(positions: []))),
        "CUSTOMER_RISK_CENTER_POSITION_SET_MISMATCH");
});

Check("RKC-017", "unexpected risk position fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(riskContext: CreateRisk(
            positions: RiskPositions(
                RiskPosition("HBL", "BANKS", 10m, 100m),
                RiskPosition("SYS", "TECH", 1m, 50m))))),
        "CUSTOMER_RISK_CENTER_POSITION_SET_MISMATCH");
});

Check("RKC-018", "position quantity mismatch fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(riskContext: CreateRisk(
            positions: RiskPositions(RiskPosition("HBL", "BANKS", 9m, 100m))))),
        "CUSTOMER_RISK_CENTER_POSITION_QUANTITY_MISMATCH");
});

Check("RKC-019", "duplicate normalized portfolio instrument is rejected", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(portfolio: CreatePortfolio(
            positions:
            [
                new ProjectedPosition("HBL", 10m, 20m),
                new ProjectedPosition(" HBL ", 1m, 10m),
            ]))),
        "CUSTOMER_RISK_CENTER_PORTFOLIO_DUPLICATE_INSTRUMENT");
});

Check("RKC-020", "duplicate normalized risk instrument is rejected", () =>
{
    RequireThrows<InvalidOperationException>(
        () => builder.Build(CreateRequest(riskContext: CreateRisk(
            positions: RiskPositions(
                RiskPosition("HBL", "BANKS", 10m, 100m),
                RiskPosition(" HBL ", "BANKS", 10m, 100m))))),
        "CUSTOMER_RISK_CENTER_RISK_DUPLICATE_INSTRUMENT");
});

Check("RKC-021", "non-positive portfolio quantity is rejected", () =>
{
    RequireThrows<ArgumentOutOfRangeException>(
        () => builder.Build(CreateRequest(portfolio: CreatePortfolio(
            positions: [new ProjectedPosition("HBL", 0m, 20m)]))));
});

Check("RKC-022", "non-positive portfolio average cost is rejected", () =>
{
    RequireThrows<ArgumentOutOfRangeException>(
        () => builder.Build(CreateRequest(portfolio: CreatePortfolio(
            positions: [new ProjectedPosition("HBL", 10m, 0m)]))));
});

Check("RKC-023", "non-positive risk market price is rejected", () =>
{
    RequireThrows<ArgumentOutOfRangeException>(
        () => builder.Build(CreateRequest(riskContext: CreateRisk(
            positions: RiskPositions(RiskPosition("HBL", "BANKS", 10m, 0m))))));
});

Check("RKC-024", "blank risk sector is rejected", () =>
{
    RequireThrows<ArgumentException>(
        () => builder.Build(CreateRequest(riskContext: CreateRisk(
            positions: RiskPositions(RiskPosition("HBL", " ", 10m, 100m))))));
});

Check("RKC-025", "market values use exact caller-provided risk prices", () =>
{
    var result = builder.Build(CreateRequest());
    Require(result.Cash == 500m, "cash must come from paper portfolio");
    Require(result.InvestedMarketValue == 1000m, "10 x 100 market value must equal 1000");
    Require(result.TotalMarketValue == 1500m, "cash plus invested market value must equal 1500");
});

Check("RKC-026", "gross exposure fraction is exact decimal arithmetic", () =>
{
    var result = builder.Build(CreateRequest());
    Require(result.GrossExposureFraction == (1000m / 1500m), "gross exposure fraction must use total market value");
});

Check("RKC-027", "largest position fraction is exact", () =>
{
    var result = builder.Build(CreateRequest());
    Require(result.LargestPositionFraction == (1000m / 1500m), "largest position fraction must be exact");
});

Check("RKC-028", "sector exposure aggregates deterministically", () =>
{
    var portfolio = CreatePortfolio(
        cash: 500m,
        positions:
        [
            new ProjectedPosition("HBL", 10m, 20m),
            new ProjectedPosition("UBL", 5m, 30m),
        ]);
    var risk = CreateRisk(
        cash: 500m,
        positions: RiskPositions(
            RiskPosition("UBL", "BANKS", 5m, 40m),
            RiskPosition("HBL", "BANKS", 10m, 100m)));
    var result = builder.Build(CreateRequest(portfolio: portfolio, riskContext: risk));
    var banks = result.Sectors.Single();
    Require(banks.SectorReference == "BANKS", "bank sector must be normalized");
    Require(banks.MarketValue == 1200m, "sector market value must aggregate both positions");
    Require(banks.FractionOfTotalValue == (1200m / 1700m), "sector fraction must be exact");
});

Check("RKC-029", "position output ordering is stable by instrument", () =>
{
    var portfolio = CreatePortfolio(
        positions:
        [
            new ProjectedPosition("SYS", 2m, 30m),
            new ProjectedPosition("HBL", 10m, 20m),
        ]);
    var risk = CreateRisk(
        positions: RiskPositions(
            RiskPosition("SYS", "TECH", 2m, 50m),
            RiskPosition("HBL", "BANKS", 10m, 100m)));
    var result = builder.Build(CreateRequest(portfolio: portfolio, riskContext: risk));
    Require(
        result.Positions.Select(static position => position.InstrumentReference)
            .SequenceEqual(CustomerRiskCenterVerifierConstants.ExpectedInstrumentOrder),
        "positions must use stable ordinal instrument order");
});

Check("RKC-030", "sector output ordering is stable ordinal", () =>
{
    var portfolio = CreatePortfolio(
        positions:
        [
            new ProjectedPosition("SYS", 2m, 30m),
            new ProjectedPosition("HBL", 10m, 20m),
        ]);
    var risk = CreateRisk(
        positions: RiskPositions(
            RiskPosition("SYS", "TECH", 2m, 50m),
            RiskPosition("HBL", "BANKS", 10m, 100m)));
    var result = builder.Build(CreateRequest(portfolio: portfolio, riskContext: risk));
    Require(
        result.Sectors.Select(static sector => sector.SectorReference)
            .SequenceEqual(CustomerRiskCenterVerifierConstants.ExpectedSectorOrder),
        "sectors must use stable ordinal order");
});

Check("RKC-031", "clear posture requires bounded concentration", () =>
{
    var portfolio = CreatePortfolio(
        cash: 800m,
        positions:
        [
            new ProjectedPosition("HBL", 1m, 20m),
            new ProjectedPosition("SYS", 1m, 30m),
        ]);
    var risk = CreateRisk(
        cash: 800m,
        positions: RiskPositions(
            RiskPosition("HBL", "BANKS", 1m, 100m),
            RiskPosition("SYS", "TECH", 1m, 100m)));
    var result = builder.Build(CreateRequest(portfolio: portfolio, riskContext: risk));
    Require(result.ConcentrationPosture == CustomerRiskConcentrationPosture.Clear, "20% invested diversified fixture must be clear");
    Require(result.Signals.Count == 0, "clear posture should not emit concentration signals");
});

Check("RKC-032", "watch posture is deterministic at watch threshold", () =>
{
    var portfolio = CreatePortfolio(
        cash: 600m,
        positions: [new ProjectedPosition("HBL", 4m, 20m)]);
    var risk = CreateRisk(
        cash: 600m,
        positions: RiskPositions(RiskPosition("HBL", "BANKS", 4m, 100m)));
    var result = builder.Build(CreateRequest(portfolio: portfolio, riskContext: risk));
    Require(result.LargestPositionFraction == 0.4m, "fixture position fraction must be 40%");
    Require(result.ConcentrationPosture == CustomerRiskConcentrationPosture.Watch, "40% single-position fixture must be watch");
});

Check("RKC-033", "elevated posture is deterministic at elevated threshold", () =>
{
    var portfolio = CreatePortfolio(
        cash: 400m,
        positions: [new ProjectedPosition("HBL", 6m, 20m)]);
    var risk = CreateRisk(
        cash: 400m,
        positions: RiskPositions(RiskPosition("HBL", "BANKS", 6m, 100m)));
    var result = builder.Build(CreateRequest(portfolio: portfolio, riskContext: risk));
    Require(result.LargestPositionFraction == 0.6m, "fixture position fraction must be 60%");
    Require(result.ConcentrationPosture == CustomerRiskConcentrationPosture.Elevated, "60% single-position fixture must be elevated");
});

Check("RKC-034", "risk profile evidence metadata propagates without raw answers", () =>
{
    var result = builder.Build(CreateRequest());
    Require(result.RiskProfileVersion == 1, "risk profile version must propagate");
    Require(result.RiskProfileScore == 60, "risk profile score must propagate");
    Require(result.RiskBand == CustomerRiskBand.Balanced, "risk band must propagate");
    Require(result.RiskProfileUpdatedAt == evaluatedAt.AddDays(-1), "risk profile evidence timestamp must propagate");
});

Check("RKC-035", "authority is informational and non-executing", () =>
{
    var authority = builder.Build(CreateRequest()).Authority;
    Require(authority.InformationalOnly, "risk-center output must be informational");
    Require(!authority.CanRecommendInvestments, "risk center cannot recommend investments");
    Require(!authority.CanSuggestAllocations, "risk center cannot suggest allocations");
    Require(!authority.CanPlaceOrders, "risk center cannot place orders");
    Require(!authority.CanMoveMoney, "risk center cannot move money");
    Require(!authority.CanOverrideRisk, "risk center cannot override risk authority");
    Require(!authority.CanCallProvider, "risk center cannot call providers");
});

Check("RKC-036", "snapshot schema exposes no restricted pii advice or execution fields", () =>
{
    var names = typeof(CustomerRiskCenterSnapshot).GetProperties()
        .Select(static property => property.Name)
        .ToArray();

    foreach (var fragment in CustomerRiskCenterVerifierConstants.ForbiddenSnapshotFieldFragments)
    {
        Require(
            !names.Any(name => name.Contains(fragment, StringComparison.OrdinalIgnoreCase)),
            $"snapshot must not expose forbidden field fragment '{fragment}'");
    }
});

Check("RKC-037", "builder has no provider database or execution dependency", () =>
{
    var constructors = typeof(DeterministicCustomerRiskCenterBuilder).GetConstructors();
    Require(constructors.Length == 1, "builder must expose exactly one public constructor");
    Require(constructors[0].GetParameters().Length == 0, "builder must not depend on provider/database/execution infrastructure");
});

Check("RKC-038", "cash-only portfolio produces zero exposure deterministically", () =>
{
    var result = builder.Build(CreateRequest(
        portfolio: CreatePortfolio(cash: 1000m, positions: []),
        riskContext: CreateRisk(cash: 1000m, positions: [])));
    Require(result.TotalMarketValue == 1000m, "cash-only total must equal cash");
    Require(result.GrossExposureFraction == 0m, "cash-only gross exposure must be zero");
    Require(result.LargestPositionFraction == 0m, "cash-only largest position must be zero");
    Require(result.LargestSectorFraction == 0m, "cash-only largest sector must be zero");
    Require(result.ConcentrationPosture == CustomerRiskConcentrationPosture.Clear, "cash-only posture must be clear");
});

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Customer risk center verification FAILED ({failures.Count}):");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($" - {failure}");
    }

    return 1;
}

Console.WriteLine($"Customer risk center verifier PASS ({passes}/38).");
Console.WriteLine("CUSTOMER_SCOPE: authenticated customer, risk-profile owner and requested customer must agree.");
Console.WriteLine("ACCOUNT_SCOPE: paper portfolio and bounded risk context must match the requested account.");
Console.WriteLine("EVIDENCE_BOUNDARY: summary uses existing customer risk-profile, paper portfolio and bounded risk context only.");
Console.WriteLine("NO_ADVICE_NO_TRADING: output is informational and cannot recommend securities, suggest allocations, trade, move money or override risk.");
Console.WriteLine("SAFE_OUTPUT: no display name, contact identity, raw risk answers, restricted PII or authority-bearing execution fields are projected.");
Console.WriteLine("NOT_LIVE: no API, database, provider credential, market subscription, pyPSX transport or real-money path is exercised.");
return 0;

void Check(string id, string description, Action assertion)
{
    try
    {
        assertion();
        passes++;
        Console.WriteLine($"RISK_CENTER_PASS {id} {description}");
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
            Require(
                exception.Message.Contains(expectedMessage, StringComparison.Ordinal),
                $"expected exception containing '{expectedMessage}', received '{exception.Message}'");
        }

        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name} was not thrown.");
}

CustomerRiskCenterRequest CreateRequest(
    Guid? authenticatedCustomerId = null,
    Guid? requestCustomerId = null,
    string accountReference = "PAPER-001",
    DateTimeOffset? requestEvaluatedAt = null,
    TimeSpan? maximumRiskProfileAge = null,
    TimeSpan? maximumRiskContextAge = null,
    CustomerRiskProfile? riskProfile = null,
    PortfolioProjectionSnapshot? portfolio = null,
    RiskPortfolioSnapshot? riskContext = null)
{
    var owner = requestCustomerId ?? customerId;
    return new CustomerRiskCenterRequest(
        authenticatedCustomerId ?? owner,
        owner,
        accountReference,
        requestEvaluatedAt ?? evaluatedAt,
        maximumRiskProfileAge ?? TimeSpan.FromDays(30),
        maximumRiskContextAge ?? TimeSpan.FromMinutes(10),
        riskProfile ?? CreateRiskProfile(owner: owner),
        portfolio ?? CreatePortfolio(),
        riskContext ?? CreateRisk());
}

CustomerRiskProfile CreateRiskProfile(Guid? owner = null, DateTimeOffset? updatedAt = null) =>
    new(
        owner ?? customerId,
        version: 1,
        new CustomerRiskAnswers(
            lossTolerance: 3,
            marketExperience: 3,
            horizonCapacity: 3,
            liquidityNeed: 3),
        updatedAt ?? evaluatedAt.AddDays(-1));

PortfolioProjectionSnapshot CreatePortfolio(
    string accountReference = "PAPER-001",
    decimal cash = 500m,
    IReadOnlyList<ProjectedPosition>? positions = null) =>
    new(
        accountReference,
        cash,
        positions ?? CustomerRiskCenterVerifierConstants.DefaultPortfolioPositions,
        LastSequence: 1,
        UniqueSourceEventCount: 2,
        UniqueExecutionCount: 1);

RiskPortfolioSnapshot CreateRisk(
    string accountReference = "PAPER-001",
    decimal cash = 500m,
    IReadOnlyList<RiskValuedPosition>? positions = null) =>
    new(
        accountReference,
        cash,
        positions ?? CustomerRiskCenterVerifierConstants.DefaultRiskPositions);

RiskValuedPosition RiskPosition(
    string instrument,
    string sector,
    decimal quantity,
    decimal price,
    DateTimeOffset? observedAt = null) =>
    new(instrument, sector, quantity, price, observedAt ?? evaluatedAt.AddMinutes(-1));

IReadOnlyList<RiskValuedPosition> RiskPositions(params RiskValuedPosition[] positions) => positions;

static class CustomerRiskCenterVerifierConstants
{
    internal static readonly ProjectedPosition[] DefaultPortfolioPositions =
    [
        new("HBL", 10m, 20m),
    ];

    internal static readonly RiskValuedPosition[] DefaultRiskPositions =
    [
        new("HBL", "BANKS", 10m, 100m, new DateTimeOffset(2026, 9, 21, 23, 59, 0, TimeSpan.Zero)),
    ];

    internal static readonly string[] ExpectedInstrumentOrder = ["HBL", "SYS"];

    internal static readonly string[] ExpectedSectorOrder = ["BANKS", "TECH"];

    internal static readonly string[] ForbiddenSnapshotFieldFragments =
    [
        "DisplayName",
        "Email",
        "Phone",
        "Address",
        "National",
        "Cnic",
        "Password",
        "Credential",
        "Token",
        "RiskAnswers",
        "LossTolerance",
        "LiquidityNeed",
        "Recommendation",
        "SuggestedInstrument",
        "TargetAllocation",
        "Order",
        "Broker",
        "Provider",
        "PyPsx",
        "Execution",
        "TradeInstruction",
    ];
}
