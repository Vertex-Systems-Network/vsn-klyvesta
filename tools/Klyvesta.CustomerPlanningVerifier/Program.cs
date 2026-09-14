using Klyvesta.Application.Planning;
using Klyvesta.Domain.Customers;
using Klyvesta.Domain.Planning;
using Klyvesta.Domain.Portfolios;

var failures = new List<string>();
var passes = 0;
var engine = new DeterministicCustomerPlanningEngine();
var customerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
var otherCustomerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
var asOfDate = new DateOnly(2026, 9, 15);

Check("PLAN-001", "same inputs produce deterministic planning output", () =>
{
    var request = CreateRequest();
    var first = engine.Build(request);
    var second = engine.Build(request);
    Require(first == second, "identical planning inputs must produce structurally identical output");
});

Check("PLAN-002", "cross-customer request fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => engine.Build(CreateRequest(authenticatedCustomerId: otherCustomerId)),
        "CUSTOMER_PLANNING_SCOPE_MISMATCH");
});

Check("PLAN-003", "missing authenticated customer id is rejected", () =>
{
    RequireThrows<ArgumentException>(() => engine.Build(CreateRequest(authenticatedCustomerId: Guid.Empty)));
});

Check("PLAN-004", "foreign-owned profile is rejected", () =>
{
    RequireThrows<InvalidOperationException>(
        () => engine.Build(CreateRequest(profile: CreateProfile(otherCustomerId, 50m))),
        "CUSTOMER_PLANNING_PROFILE_SCOPE_MISMATCH");
});

Check("PLAN-005", "foreign-owned goal is rejected", () =>
{
    RequireThrows<InvalidOperationException>(
        () => engine.Build(CreateRequest(goal: CreateGoal(otherCustomerId))),
        "CUSTOMER_PLANNING_GOAL_SCOPE_MISMATCH");
});

Check("PLAN-006", "foreign-owned risk profile is rejected", () =>
{
    RequireThrows<InvalidOperationException>(
        () => engine.Build(CreateRequest(riskProfile: CreateRiskProfile(otherCustomerId, CustomerRiskBand.Balanced))),
        "CUSTOMER_PLANNING_RISK_SCOPE_MISMATCH");
});

Check("PLAN-007", "paused goal cannot produce an active contribution plan", () =>
{
    RequireThrows<InvalidOperationException>(
        () => engine.Build(CreateRequest(goal: CreateGoal(customerId, status: CustomerGoalStatus.Paused))),
        "CUSTOMER_PLANNING_GOAL_NOT_ACTIVE");
});

Check("PLAN-008", "completed goal cannot produce an active contribution plan", () =>
{
    RequireThrows<InvalidOperationException>(
        () => engine.Build(CreateRequest(goal: CreateGoal(customerId, status: CustomerGoalStatus.Completed))),
        "CUSTOMER_PLANNING_GOAL_NOT_ACTIVE");
});

Check("PLAN-009", "target date equal to as-of date is rejected", () =>
{
    RequireThrows<InvalidOperationException>(
        () => engine.Build(CreateRequest(goal: CreateGoal(customerId, targetDate: asOfDate))),
        "CUSTOMER_PLANNING_TARGET_DATE_NOT_FUTURE");
});

Check("PLAN-010", "past target date is rejected", () =>
{
    RequireThrows<InvalidOperationException>(
        () => engine.Build(CreateRequest(goal: CreateGoal(customerId, targetDate: asOfDate.AddDays(-1)))),
        "CUSTOMER_PLANNING_TARGET_DATE_NOT_FUTURE");
});

Check("PLAN-011", "same-month future target produces one contribution period", () =>
{
    var plan = engine.Build(CreateRequest(goal: CreateGoal(customerId, targetDate: asOfDate.AddDays(1))));
    Require(plan.ContributionPeriods == 1, "same-month future target must have one deterministic contribution period");
});

Check("PLAN-012", "exact annual anniversary produces twelve contribution periods", () =>
{
    var plan = engine.Build(CreateRequest(goal: CreateGoal(customerId, targetDate: new DateOnly(2027, 9, 15))));
    Require(plan.ContributionPeriods == 12, "one exact calendar year must produce twelve contribution periods");
});

Check("PLAN-013", "day beyond annual anniversary includes the next contribution period", () =>
{
    var plan = engine.Build(CreateRequest(goal: CreateGoal(customerId, targetDate: new DateOnly(2027, 9, 16))));
    Require(plan.ContributionPeriods == 13, "target day after anniversary must include the next calendar contribution period");
});

Check("PLAN-014", "fully funded goal has zero remaining contribution requirement", () =>
{
    var goal = CreateGoal(customerId, targetAmount: 1000m, currentAmount: 1000m);
    var plan = engine.Build(CreateRequest(goal: goal));
    Require(plan.RemainingAmount == 0m, "fully funded goal remaining amount must be zero");
    Require(plan.RequiredMonthlyContribution == 0m, "fully funded goal required contribution must be zero");
    Require(plan.AdditionalMonthlyContributionGap == 0m, "fully funded goal additional contribution gap must be zero");
});

Check("PLAN-015", "required contribution rounds upward to exact cents", () =>
{
    var plan = engine.Build(CreateRequest());
    Require(plan.RemainingAmount == 1000m, "default fixture must have a 1000.00 remaining goal amount");
    Require(plan.ContributionPeriods == 12, "default fixture must have twelve contribution periods");
    Require(plan.RequiredMonthlyContribution == 83.34m, "1000 / 12 must round upward to 83.34, never downward");
});

Check("PLAN-016", "contribution gap is deterministic exact-decimal arithmetic", () =>
{
    var plan = engine.Build(CreateRequest());
    Require(plan.PlannedMonthlyContribution == 50m, "fixture monthly contribution must come from customer profile evidence");
    Require(plan.AdditionalMonthlyContributionGap == 33.34m, "83.34 required less 50.00 planned must equal 33.34");
});

Check("PLAN-017", "planned contribution above arithmetic requirement has zero additional gap", () =>
{
    var plan = engine.Build(CreateRequest(profile: CreateProfile(customerId, 100m)));
    Require(plan.AdditionalMonthlyContributionGap == 0m, "additional contribution gap cannot become negative");
});

Check("PLAN-018", "portfolio account scope mismatch fails closed", () =>
{
    RequireThrows<InvalidOperationException>(
        () => engine.Build(CreateRequest(portfolio: CreatePortfolio(accountReference: "PAPER-OTHER"))),
        "CUSTOMER_PLANNING_ACCOUNT_SCOPE_MISMATCH");
});

Check("PLAN-019", "negative paper cash is rejected", () =>
{
    RequireThrows<ArgumentOutOfRangeException>(() =>
        engine.Build(CreateRequest(portfolio: CreatePortfolio(cash: -1m))));
});

Check("PLAN-020", "non-positive paper position values are rejected", () =>
{
    var positions = new List<ProjectedPosition>
    {
        new("HBL", 0m, 20m),
    };
    RequireThrows<ArgumentOutOfRangeException>(() =>
        engine.Build(CreateRequest(portfolio: CreatePortfolio(positions: positions))));
});

Check("PLAN-021", "normalized duplicate paper instruments are rejected", () =>
{
    var positions = new List<ProjectedPosition>
    {
        new("HBL", 10m, 20m),
        new(" HBL ", 1m, 10m),
    };
    RequireThrows<InvalidOperationException>(
        () => engine.Build(CreateRequest(portfolio: CreatePortfolio(positions: positions))),
        "CUSTOMER_PLANNING_DUPLICATE_INSTRUMENT");
});

Check("PLAN-022", "paper portfolio book value is exact cost-basis context", () =>
{
    var plan = engine.Build(CreateRequest());
    Require(plan.PaperPortfolioBookValue == 700m, "500 cash plus 10 x 20 paper cost basis must equal 700");
});

Check("PLAN-023", "paper portfolio context does not reallocate the customer goal", () =>
{
    var baseline = engine.Build(CreateRequest());
    var richerPortfolio = CreatePortfolio(
        cash: 9000m,
        positions: new List<ProjectedPosition> { new("SYS", 100m, 100m) });
    var withDifferentPortfolio = engine.Build(CreateRequest(portfolio: richerPortfolio));

    Require(withDifferentPortfolio.PaperPortfolioBookValue == 19000m, "alternate paper portfolio fixture book value is incorrect");
    Require(withDifferentPortfolio.RemainingAmount == baseline.RemainingAmount, "portfolio book value must not be silently allocated to a customer goal");
    Require(withDifferentPortfolio.RequiredMonthlyContribution == baseline.RequiredMonthlyContribution, "portfolio context must not alter zero-return goal arithmetic");
});

Check("PLAN-024", "risk band is context-only and does not change contribution arithmetic", () =>
{
    var conservative = engine.Build(CreateRequest(
        riskProfile: CreateRiskProfile(customerId, CustomerRiskBand.Conservative)));
    var highGrowth = engine.Build(CreateRequest(
        riskProfile: CreateRiskProfile(customerId, CustomerRiskBand.HighGrowth)));

    Require(conservative.RiskBand == CustomerRiskBand.Conservative, "conservative source risk band must be retained");
    Require(highGrowth.RiskBand == CustomerRiskBand.HighGrowth, "high-growth source risk band must be retained");
    Require(conservative.RemainingAmount == highGrowth.RemainingAmount, "risk band cannot change goal gap arithmetic");
    Require(conservative.RequiredMonthlyContribution == highGrowth.RequiredMonthlyContribution, "risk band cannot change required contribution arithmetic");
    Require(conservative.AdditionalMonthlyContributionGap == highGrowth.AdditionalMonthlyContributionGap, "risk band cannot change contribution gap arithmetic");
});

Check("PLAN-025", "planning authority is permanently informational and non-executing", () =>
{
    var authority = engine.Build(CreateRequest()).Authority;
    Require(authority.InformationalOnly, "planning output must be explicitly informational only");
    Require(!authority.CanRecommendInvestments, "planning cannot recommend investments");
    Require(!authority.CanPlaceOrders, "planning cannot place orders");
    Require(!authority.CanMoveMoney, "planning cannot move money");
    Require(!authority.CanOverrideRisk, "planning cannot override risk authority");
});

Check("PLAN-026", "projection basis is explicit zero-return arithmetic", () =>
{
    var plan = engine.Build(CreateRequest());
    Require(plan.ProjectionBasis == CustomerPlanningProjectionBasis.ZeroReturnArithmetic,
        "planning basis must remain zero-return arithmetic without fabricated return assumptions");
});

Check("PLAN-027", "invalid paper projection counters are rejected", () =>
{
    RequireThrows<ArgumentOutOfRangeException>(() =>
        engine.Build(CreateRequest(portfolio: CreatePortfolio(lastSequence: -2))));
});

Check("PLAN-028", "paper execution count cannot exceed source event count", () =>
{
    RequireThrows<InvalidOperationException>(
        () => engine.Build(CreateRequest(portfolio: CreatePortfolio(sourceEventCount: 1, executionCount: 2))),
        "CUSTOMER_PLANNING_EXECUTION_COUNT_INVALID");
});

Check("PLAN-029", "planning output schema exposes no advice, broker, order, provider or contact authority", () =>
{
    var forbiddenFragments = new List<string>
    {
        "ExpectedReturn",
        "Recommendation",
        "SuggestedInstrument",
        "Order",
        "Broker",
        "PyPsx",
        "Provider",
        "Email",
        "Phone",
        "ContactDestination",
    };

    var propertyNames = typeof(CustomerGoalPlan).GetProperties().Select(static property => property.Name).ToArray();
    foreach (var fragment in forbiddenFragments)
    {
        Require(!propertyNames.Any(name => name.Contains(fragment, StringComparison.OrdinalIgnoreCase)),
            $"planning output must not expose authority-bearing field fragment '{fragment}'");
    }
});

Check("PLAN-030", "planning engine has no broker, provider, database or execution constructor dependency", () =>
{
    var constructors = typeof(DeterministicCustomerPlanningEngine).GetConstructors();
    Require(constructors.Length == 1, "planning engine must expose exactly one constructor");
    Require(constructors[0].GetParameters().Length == 0, "planning engine must not depend on live/provider/execution infrastructure");
});

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Customer planning verification FAILED ({failures.Count}):");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($" - {failure}");
    }

    return 1;
}

Console.WriteLine($"Customer planning verifier PASS ({passes}/30).");
Console.WriteLine("ZERO_RETURN_ARITHMETIC: contribution requirements are deterministic calendar arithmetic with no expected-return assumption.");
Console.WriteLine("GOAL_AUTHORITY: CustomerGoal.CurrentAmount remains the goal-progress source; paper portfolio book value is context only and is not silently allocated to the goal.");
Console.WriteLine("RISK_CONTEXT_ONLY: the existing customer risk band is retained as context and cannot alter planning arithmetic or override Risk authority.");
Console.WriteLine("NO_ADVICE_NO_TRADING: planning cannot recommend securities, place orders, move money, dispatch notifications or create broker/provider authority.");
Console.WriteLine("NOT_LIVE: no database, production customer PII, live market subscription, pyPSX credential, broker transport or real-money path is exercised.");
return 0;

void Check(string id, string description, Action assertion)
{
    try
    {
        assertion();
        passes++;
        Console.WriteLine($"PASS {id}: {description}");
    }
    catch (InvalidOperationException exception)
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

CustomerPlanningRequest CreateRequest(
    Guid? authenticatedCustomerId = null,
    Guid? requestCustomerId = null,
    CustomerProfile? profile = null,
    CustomerGoal? goal = null,
    CustomerRiskProfile? riskProfile = null,
    PortfolioProjectionSnapshot? portfolio = null,
    string accountReference = "PAPER-001",
    DateOnly? requestAsOfDate = null)
{
    var owner = requestCustomerId ?? customerId;
    return new CustomerPlanningRequest(
        authenticatedCustomerId ?? owner,
        owner,
        accountReference,
        requestAsOfDate ?? asOfDate,
        profile ?? CreateProfile(owner, 50m),
        goal ?? CreateGoal(owner),
        riskProfile ?? CreateRiskProfile(owner, CustomerRiskBand.Balanced),
        portfolio ?? CreatePortfolio());
}

CustomerProfile CreateProfile(Guid ownerCustomerId, decimal monthlyContribution) => new(
    ownerCustomerId,
    version: 1,
    displayName: "Planning Customer",
    CustomerExperienceLevel.Intermediate,
    investmentHorizon: "Long term",
    primaryGoal: "Future goal",
    monthlyContribution,
    updatedAt: new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero));

CustomerGoal CreateGoal(
    Guid ownerCustomerId,
    decimal targetAmount = 1200m,
    decimal currentAmount = 200m,
    DateOnly? targetDate = null,
    CustomerGoalStatus status = CustomerGoalStatus.Active) => new(
        ownerCustomerId,
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        "Education fund",
        targetAmount,
        currentAmount,
        targetDate ?? new DateOnly(2027, 9, 15),
        status,
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

CustomerRiskProfile CreateRiskProfile(Guid ownerCustomerId, CustomerRiskBand riskBand)
{
    var answers = riskBand switch
    {
        CustomerRiskBand.Conservative => new CustomerRiskAnswers(1, 1, 1, 5),
        CustomerRiskBand.Balanced => new CustomerRiskAnswers(3, 3, 3, 3),
        CustomerRiskBand.Growth => new CustomerRiskAnswers(4, 4, 4, 2),
        CustomerRiskBand.HighGrowth => new CustomerRiskAnswers(5, 5, 5, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(riskBand), "Verifier requires a concrete supported risk band."),
    };

    var profile = new CustomerRiskProfile(
        ownerCustomerId,
        version: 1,
        answers,
        new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero));
    Require(profile.RiskBand == riskBand, $"risk fixture produced {profile.RiskBand} instead of {riskBand}");
    return profile;
}

PortfolioProjectionSnapshot CreatePortfolio(
    decimal cash = 500m,
    IReadOnlyList<ProjectedPosition>? positions = null,
    string accountReference = "PAPER-001",
    long lastSequence = 1,
    int sourceEventCount = 2,
    int executionCount = 1) => new(
        accountReference,
        cash,
        positions ?? new List<ProjectedPosition> { new("HBL", 10m, 20m) },
        lastSequence,
        sourceEventCount,
        executionCount);
