using Klyvesta.Application.Brokerage;
using Klyvesta.Application.Risk;
using Klyvesta.Domain.Risk;
using Klyvesta.Infrastructure.Brokerage.Paper;

var tests = new (string Id, Func<Task> Run)[]
{
    ("RISK-001 fresh liquid bounded order is allowed", VerifyAllowAsync),
    ("RISK-002 unknown instrument fails closed", VerifyUnknownInstrumentAsync),
    ("RISK-003 missing market evidence holds", VerifyMissingMarketAsync),
    ("RISK-004 stale market evidence is denied", VerifyStaleMarketAsync),
    ("RISK-005 illiquid instrument is denied", VerifyLiquidityAsync),
    ("RISK-006 order notional limit is exact", VerifyOrderNotionalAsync),
    ("RISK-007 resulting position concentration is enforced", VerifyPositionConcentrationAsync),
    ("RISK-008 resulting sector concentration is enforced", VerifySectorConcentrationAsync),
    ("RISK-009 gross exposure limit is enforced", VerifyGrossExposureAsync),
    ("RISK-010 short selling is prohibited", VerifyShortSellingAsync),
    ("RISK-011 margin is prohibited", VerifyMarginAsync),
    ("RISK-012 leverage is prohibited", VerifyLeverageAsync),
    ("RISK-013 derivatives are prohibited", VerifyDerivativeAsync),
    ("RISK-014 activity rate and turnover are enforced", VerifyActivityLimitsAsync),
    ("RISK-015 system kill switch denies", VerifyKillSwitchAsync),
    ("RISK-016 deny blocks inner PaperBroker and records decision", VerifyAdapterDenyAsync),
    ("RISK-017 hold blocks inner PaperBroker and records decision", VerifyAdapterHoldAsync),
    ("RISK-018 allow forwards exactly once to PaperBroker", VerifyAdapterAllowAsync),
};

var failures = new List<string>();
foreach (var (id, run) in tests)
{
    try
    {
        await run();
        Console.WriteLine($"RISK_PASS {id}");
    }
    catch (Exception exception)
    {
        failures.Add($"{id}: {exception.Message}");
        Console.Error.WriteLine($"RISK_FAIL {id}: {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"Risk assertions passed: {tests.Length - failures.Count}/{tests.Length}");
Console.WriteLine("STACKED_ON_P1_08: this verifier requires the non-live PaperBroker, OMS and portfolio/reconciliation slices from draft PRs #68, #71 and #73.");
Console.WriteLine("POLICY_BOUNDARY: thresholds are synthetic paper defaults and every decision carries a policy version; they are not production business truth.");
Console.WriteLine("FAIL_CLOSED: missing/unknown/stale critical context cannot reach the inner PaperBroker.");
Console.WriteLine("NOT_ACCEPTED_AS_FULL_P1: authorization, compliance, mandate, ledger, AI shadow and remaining exit-gate evidence are separate work.");
Console.WriteLine("NOT_LIVE: no pyPSX mapping, live credentials, production customer data, personalized production advice or real-money authority is exercised.");

return failures.Count == 0 ? 0 : 1;

static Task VerifyAllowAsync()
{
    var decision = Evaluate(CreateRequest());
    Require(decision.Outcome == RiskDecisionOutcome.Allow, "bounded fresh/liquid paper order must be allowed");
    Require(decision.PolicyVersion == "paper-risk-v1", "allow decision must retain policy version");
    Require(decision.PrimaryReasonCode == "RISK_ALLOW", "allow decision must have explicit allow reason");
    Require(decision.OrderNotional == 10_000m, "order notional must use exact decimal arithmetic");
    return Task.CompletedTask;
}

static Task VerifyUnknownInstrumentAsync()
{
    var request = CreateRequest(instrumentReference: "ZZZ", instrument: Instrument("ZZZ", "OTHER"), market: Market("ZZZ"));
    var decision = Evaluate(request);
    RequireDenied(decision, "RISK_INSTRUMENT_NOT_ALLOWED");
    return Task.CompletedTask;
}

static Task VerifyMissingMarketAsync()
{
    var decision = Evaluate(CreateRequest(market: null, useDefaultMarket: false));
    Require(decision.Outcome == RiskDecisionOutcome.Hold, "missing market evidence must hold, not allow");
    Require(decision.PrimaryReasonCode == "RISK_MARKET_EVIDENCE_MISSING", "missing market hold reason must be explicit");
    Require(decision.PolicyVersion == "paper-risk-v1", "hold decision must retain policy version");
    return Task.CompletedTask;
}

static Task VerifyStaleMarketAsync()
{
    var stale = Market("AAA", observedAt: Now().AddMinutes(-10));
    var decision = Evaluate(CreateRequest(market: stale));
    RequireDenied(decision, "RISK_MARKET_DATA_STALE");
    return Task.CompletedTask;
}

static Task VerifyLiquidityAsync()
{
    var illiquid = Market("AAA", dailyTradedValue: 50_000m);
    var decision = Evaluate(CreateRequest(market: illiquid));
    RequireDenied(decision, "RISK_LIQUIDITY_BELOW_MINIMUM");
    return Task.CompletedTask;
}

static Task VerifyOrderNotionalAsync()
{
    var decision = Evaluate(CreateRequest(quantity: 501m));
    RequireDenied(decision, "RISK_ORDER_NOTIONAL_LIMIT_EXCEEDED");
    Require(decision.OrderNotional == 50_100m, "order limit evidence must preserve exact notional");
    return Task.CompletedTask;
}

static Task VerifyPositionConcentrationAsync()
{
    var portfolio = Portfolio(
        cash: 60_000m,
        Position("AAA", "TECH", 400m, 100m));
    var decision = Evaluate(CreateRequest(quantity: 200m, portfolio: portfolio));
    RequireDenied(decision, "RISK_POSITION_CONCENTRATION_LIMIT_EXCEEDED");
    Require(decision.ProjectedPositionFraction == 0.6m, "position concentration must be calculated from post-trade portfolio");
    return Task.CompletedTask;
}

static Task VerifySectorConcentrationAsync()
{
    var portfolio = Portfolio(
        cash: 60_000m,
        Position("BBB", "TECH", 400m, 100m));
    var decision = Evaluate(CreateRequest(quantity: 250m, portfolio: portfolio));
    RequireDenied(decision, "RISK_SECTOR_CONCENTRATION_LIMIT_EXCEEDED");
    Require(decision.ProjectedSectorFraction == 0.65m, "sector concentration must include existing sector positions plus proposed trade");
    return Task.CompletedTask;
}

static Task VerifyGrossExposureAsync()
{
    var policy = Policy(maxSinglePositionFraction: 0.9m, maxSectorFraction: 0.9m, maxGrossExposure: 80_000m);
    var portfolio = Portfolio(
        cash: 30_000m,
        Position("AAA", "TECH", 700m, 100m));
    var request = CreateRequest(
        instrumentReference: "BBB",
        quantity: 150m,
        instrument: Instrument("BBB", "FIN"),
        market: Market("BBB"),
        portfolio: portfolio);
    var decision = Evaluate(request, policy);
    RequireDenied(decision, "RISK_GROSS_EXPOSURE_LIMIT_EXCEEDED");
    Require(decision.ProjectedGrossExposure == 85_000m, "gross exposure must include all post-trade long market value");
    return Task.CompletedTask;
}

static Task VerifyShortSellingAsync()
{
    var decision = Evaluate(CreateRequest(side: RiskTradeSide.Sell, quantity: 1m));
    RequireDenied(decision, "RISK_SHORT_SELLING_PROHIBITED");
    return Task.CompletedTask;
}

static Task VerifyMarginAsync()
{
    var decision = Evaluate(CreateRequest(usesMargin: true));
    RequireDenied(decision, "RISK_MARGIN_PROHIBITED");
    return Task.CompletedTask;
}

static Task VerifyLeverageAsync()
{
    var decision = Evaluate(CreateRequest(requestedLeverageMultiple: 2m));
    RequireDenied(decision, "RISK_LEVERAGE_PROHIBITED");
    return Task.CompletedTask;
}

static Task VerifyDerivativeAsync()
{
    var derivative = Instrument("DERIV", "INDEX", RiskAssetClass.Derivative);
    var decision = Evaluate(CreateRequest(
        instrumentReference: "DERIV",
        instrument: derivative,
        market: Market("DERIV")));
    RequireDenied(decision, "RISK_DERIVATIVE_PROHIBITED");
    return Task.CompletedTask;
}

static Task VerifyActivityLimitsAsync()
{
    RiskActivityWindow activity = new(Now().AddMinutes(-10), 5, 49_000m);
    var decision = Evaluate(CreateRequest(activity: activity));
    RequireDenied(decision, "RISK_ORDER_RATE_LIMIT_EXCEEDED");
    Require(decision.ReasonCodes.Contains("RISK_TURNOVER_LIMIT_EXCEEDED", StringComparer.Ordinal), "turnover breach must be independently visible");
    return Task.CompletedTask;
}

static Task VerifyKillSwitchAsync()
{
    var decision = Evaluate(CreateRequest(), Policy(killSwitchEnabled: true));
    RequireDenied(decision, "RISK_KILL_SWITCH_ACTIVE");
    return Task.CompletedTask;
}

static async Task VerifyAdapterDenyAsync()
{
    var inner = CreatePaperBroker();
    var staleContext = SubmissionContext(market: Market("AAA", observedAt: Now().AddMinutes(-10)));
    var provider = new FixedRiskContextProvider(staleContext);
    var guarded = new RiskGuardBrokerAdapter(inner, new DeterministicRiskGovernor(), provider, FixedClock);
    var command = Command(16);

    var result = await guarded.SubmitOrderAsync(command);
    var decision = guarded.GetRecordedDecision(command.BrokerOrderId);

    Require(result.State == BrokerResultState.Rejected, "deterministic risk denial must return rejected pre-side-effect result");
    Require(inner.AcceptedOrderCount == 0, "risk denial must not reach inner PaperBroker");
    Require(decision is not null && decision.Outcome == RiskDecisionOutcome.Deny, "denial must be recorded against broker-order id");
    Require(decision!.PolicyVersion == "paper-risk-v1", "recorded denial must retain policy version");
    Require(decision.ReasonCodes.Contains("RISK_MARKET_DATA_STALE", StringComparer.Ordinal), "recorded denial must retain reason code");
}

static async Task VerifyAdapterHoldAsync()
{
    var inner = CreatePaperBroker();
    var provider = new FixedRiskContextProvider(SubmissionContext(market: null, useDefaultMarket: false));
    var guarded = new RiskGuardBrokerAdapter(inner, new DeterministicRiskGovernor(), provider, FixedClock);
    var command = Command(17);

    var result = await guarded.SubmitOrderAsync(command);
    var decision = guarded.GetRecordedDecision(command.BrokerOrderId);

    Require(result.State == BrokerResultState.RetryableFailure, "risk hold must return retryable pre-side-effect result");
    Require(inner.AcceptedOrderCount == 0, "risk hold must not reach inner PaperBroker");
    Require(decision is not null && decision.Outcome == RiskDecisionOutcome.Hold, "hold must be recorded against broker-order id");
    Require(decision!.PrimaryReasonCode == "RISK_MARKET_EVIDENCE_MISSING", "recorded hold must retain exact reason");
}

static async Task VerifyAdapterAllowAsync()
{
    var inner = CreatePaperBroker();
    var provider = new FixedRiskContextProvider(SubmissionContext());
    var guarded = new RiskGuardBrokerAdapter(inner, new DeterministicRiskGovernor(), provider, FixedClock);
    var command = Command(18);

    var result = await guarded.SubmitOrderAsync(command);
    var decision = guarded.GetRecordedDecision(command.BrokerOrderId);

    Require(result.State == BrokerResultState.Success, "allowed paper risk decision must forward to inner PaperBroker");
    Require(inner.AcceptedOrderCount == 1, "allowed submit must reach inner PaperBroker exactly once");
    Require(decision is not null && decision.Outcome == RiskDecisionOutcome.Allow, "allow decision must be recorded before forwarding");
    Require(decision!.PolicyVersion == "paper-risk-v1", "recorded allow must retain policy version");
}

static RiskDecision Evaluate(RiskEvaluationRequest request, PaperRiskPolicy? policy = null) =>
    new DeterministicRiskGovernor().Evaluate(policy ?? Policy(), request);

static PaperRiskPolicy Policy(
    bool killSwitchEnabled = false,
    decimal maxSinglePositionFraction = 0.5m,
    decimal maxSectorFraction = 0.6m,
    decimal maxGrossExposure = 80_000m) =>
    new(
        "paper-risk-v1",
        new HashSet<string>(["AAA", "BBB", "LEV", "DERIV"], StringComparer.Ordinal),
        TimeSpan.FromMinutes(5),
        MinimumDailyTradedValue: 100_000m,
        MaxOrderNotional: 50_000m,
        maxSinglePositionFraction,
        maxSectorFraction,
        maxGrossExposure,
        MaxOrdersPerWindow: 5,
        MaxTurnoverPerWindow: 50_000m,
        ActivityWindow: TimeSpan.FromHours(1),
        killSwitchEnabled);

static RiskEvaluationRequest CreateRequest(
    string instrumentReference = "AAA",
    RiskTradeSide side = RiskTradeSide.Buy,
    decimal quantity = 100m,
    RiskInstrumentContext? instrument = null,
    RiskMarketEvidence? market = null,
    bool useDefaultMarket = true,
    RiskPortfolioSnapshot? portfolio = null,
    RiskActivityWindow? activity = null,
    bool usesMargin = false,
    decimal requestedLeverageMultiple = 1m) =>
    new(
        "paper-risk-account",
        instrumentReference,
        side,
        quantity,
        ExpectedPrice: 100m,
        usesMargin,
        requestedLeverageMultiple,
        instrument ?? Instrument(instrumentReference, "TECH"),
        useDefaultMarket ? market ?? Market(instrumentReference) : market,
        portfolio ?? Portfolio(100_000m),
        activity ?? new RiskActivityWindow(Now().AddMinutes(-10), 0, 0m),
        Now());

static RiskSubmissionContext SubmissionContext(
    RiskMarketEvidence? market = null,
    bool useDefaultMarket = true) =>
    new(
        Policy(),
        Instrument("AAA", "TECH"),
        useDefaultMarket ? market ?? Market("AAA") : market,
        Portfolio(100_000m),
        new RiskActivityWindow(Now().AddMinutes(-10), 0, 0m));

static RiskInstrumentContext Instrument(
    string instrumentReference,
    string sectorReference,
    RiskAssetClass assetClass = RiskAssetClass.Equity,
    bool isEligible = true,
    bool isLeveragedProduct = false,
    bool requiresMargin = false) =>
    new(instrumentReference, sectorReference, assetClass, isEligible, isLeveragedProduct, requiresMargin);

static RiskMarketEvidence Market(
    string instrumentReference,
    decimal dailyTradedValue = 1_000_000m,
    DateTimeOffset? observedAt = null) =>
    new(instrumentReference, 100m, dailyTradedValue, observedAt ?? Now().AddMinutes(-1));

static RiskPortfolioSnapshot Portfolio(decimal cash, params RiskValuedPosition[] positions) =>
    new("paper-risk-account", cash, positions);

static RiskValuedPosition Position(
    string instrumentReference,
    string sectorReference,
    decimal quantity,
    decimal marketPrice) =>
    new(instrumentReference, sectorReference, quantity, marketPrice, Now().AddMinutes(-1));

static PaperBrokerAdapter CreatePaperBroker() =>
    new(new PaperBrokerProfile
    {
        StartingCash = 100_000m,
        SubmissionOutcome = PaperSubmissionOutcome.FullFill,
        FillPrice = 100m,
        StartTime = DateTimeOffset.UnixEpoch,
    });

static SubmitBrokerOrderCommand Command(int seed) =>
    new(
        CreateGuid(10_000 + seed),
        CreateGuid(20_000 + seed),
        $"risk-submit-{seed:D3}",
        "paper-risk-account",
        "AAA",
        BrokerOrderSide.Buy,
        BrokerOrderType.Limit,
        100m,
        100m,
        BrokerTimeInForce.Day);

static DateTimeOffset FixedClock() => Now();

static DateTimeOffset Now() => DateTimeOffset.UnixEpoch.AddHours(3);

static Guid CreateGuid(int seed) => Guid.Parse($"00000000-0000-0000-0000-{seed:D12}");

static void RequireDenied(RiskDecision decision, string reasonCode)
{
    Require(decision.Outcome == RiskDecisionOutcome.Deny, $"expected DENY for {reasonCode}, got {decision.Outcome}");
    Require(decision.ReasonCodes.Contains(reasonCode, StringComparer.Ordinal), $"expected denial reason {reasonCode}");
    Require(decision.PolicyVersion == "paper-risk-v1", "denial must retain policy version");
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class FixedRiskContextProvider(RiskSubmissionContext context) : IRiskSubmissionContextProvider
{
    public RiskSubmissionContext GetContext(SubmitBrokerOrderCommand command, DateTimeOffset evaluatedAt)
    {
        _ = command;
        _ = evaluatedAt;
        return context;
    }
}
