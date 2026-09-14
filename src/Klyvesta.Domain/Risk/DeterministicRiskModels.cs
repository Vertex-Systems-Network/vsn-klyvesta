namespace Klyvesta.Domain.Risk;

public enum RiskAssetClass
{
    Unknown,
    Equity,
    Fund,
    Derivative,
}

public enum RiskTradeSide
{
    Buy,
    Sell,
}

public enum RiskDecisionOutcome
{
    Allow,
    Deny,
    Hold,
}

public sealed record PaperRiskPolicy(
    string Version,
    IReadOnlySet<string> AllowedInstrumentReferences,
    TimeSpan MaxMarketDataAge,
    decimal MinimumDailyTradedValue,
    decimal MaxOrderNotional,
    decimal MaxSinglePositionFraction,
    decimal MaxSectorFraction,
    decimal MaxGrossExposure,
    int MaxOrdersPerWindow,
    decimal MaxTurnoverPerWindow,
    TimeSpan ActivityWindow,
    bool KillSwitchEnabled);

public sealed record RiskInstrumentContext(
    string InstrumentReference,
    string SectorReference,
    RiskAssetClass AssetClass,
    bool IsEligible,
    bool IsLeveragedProduct,
    bool RequiresMargin);

public sealed record RiskMarketEvidence(
    string InstrumentReference,
    decimal LastPrice,
    decimal DailyTradedValue,
    DateTimeOffset ObservedAt);

public sealed record RiskValuedPosition(
    string InstrumentReference,
    string SectorReference,
    decimal Quantity,
    decimal MarketPrice,
    DateTimeOffset MarketObservedAt);

public sealed record RiskPortfolioSnapshot(
    string AccountReference,
    decimal Cash,
    IReadOnlyList<RiskValuedPosition> Positions);

public sealed record RiskActivityWindow(
    DateTimeOffset StartedAt,
    int AcceptedOrderCount,
    decimal AcceptedTurnoverNotional);

public sealed record RiskEvaluationRequest(
    string AccountReference,
    string InstrumentReference,
    RiskTradeSide Side,
    decimal Quantity,
    decimal ExpectedPrice,
    bool UsesMargin,
    decimal RequestedLeverageMultiple,
    RiskInstrumentContext? Instrument,
    RiskMarketEvidence? MarketEvidence,
    RiskPortfolioSnapshot? Portfolio,
    RiskActivityWindow? Activity,
    DateTimeOffset EvaluatedAt);

public sealed record RiskDecision(
    RiskDecisionOutcome Outcome,
    string PolicyVersion,
    string PrimaryReasonCode,
    IReadOnlyList<string> ReasonCodes,
    decimal? OrderNotional,
    decimal? ProjectedGrossExposure,
    decimal? ProjectedPositionFraction,
    decimal? ProjectedSectorFraction,
    DateTimeOffset EvaluatedAt);
