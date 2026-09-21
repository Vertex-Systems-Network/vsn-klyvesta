namespace Klyvesta.Domain.Scenarios;

public enum CustomerScenarioRiskBasis
{
    ExistingRiskPortfolioSnapshot,
}

public sealed record CustomerScenarioAuthority(
    bool InformationalOnly,
    bool CanRecommendInvestments,
    bool CanPlaceOrders,
    bool CanMoveMoney,
    bool CanOverrideRisk)
{
    public static CustomerScenarioAuthority Informational { get; } =
        new(
            InformationalOnly: true,
            CanRecommendInvestments: false,
            CanPlaceOrders: false,
            CanMoveMoney: false,
            CanOverrideRisk: false);
}

public sealed record CustomerScenarioShock(
    decimal MarketShockFraction,
    IReadOnlyDictionary<string, decimal> SectorShockFractions);

public sealed record CustomerScenarioPositionResult(
    string InstrumentReference,
    string SectorReference,
    decimal Quantity,
    decimal CurrentPrice,
    decimal AppliedShockFraction,
    decimal ShockedPrice,
    decimal CurrentValue,
    decimal ShockedValue,
    decimal ValueImpact);

public sealed record CustomerScenarioResult(
    Guid CustomerId,
    string AccountReference,
    DateTimeOffset EvaluatedAt,
    TimeSpan MaximumRiskContextAge,
    decimal CurrentCash,
    decimal CurrentInvestedValue,
    decimal CurrentTotalValue,
    decimal ShockedInvestedValue,
    decimal ShockedTotalValue,
    decimal AbsoluteImpact,
    decimal ImpactFraction,
    CustomerScenarioRiskBasis RiskBasis,
    IReadOnlyList<CustomerScenarioPositionResult> Positions,
    CustomerScenarioAuthority Authority);
