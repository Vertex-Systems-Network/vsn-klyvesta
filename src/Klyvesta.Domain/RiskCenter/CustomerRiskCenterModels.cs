using Klyvesta.Domain.Customers;

namespace Klyvesta.Domain.RiskCenter;

public enum CustomerRiskConcentrationPosture
{
    Clear = 0,
    Watch = 1,
    Elevated = 2,
}

public enum CustomerRiskCenterEvidenceBasis
{
    CustomerRiskProfileAndExistingPortfolioRiskContext = 1,
}

public sealed record CustomerRiskCenterAuthority(
    bool InformationalOnly,
    bool CanRecommendInvestments,
    bool CanSuggestAllocations,
    bool CanPlaceOrders,
    bool CanMoveMoney,
    bool CanOverrideRisk,
    bool CanCallProvider)
{
    public static CustomerRiskCenterAuthority Informational { get; } =
        new(
            InformationalOnly: true,
            CanRecommendInvestments: false,
            CanSuggestAllocations: false,
            CanPlaceOrders: false,
            CanMoveMoney: false,
            CanOverrideRisk: false,
            CanCallProvider: false);
}

public sealed record CustomerRiskCenterPositionExposure(
    string InstrumentReference,
    string SectorReference,
    decimal MarketValue,
    decimal FractionOfTotalValue);

public sealed record CustomerRiskCenterSectorExposure(
    string SectorReference,
    decimal MarketValue,
    decimal FractionOfTotalValue);

public sealed record CustomerRiskCenterSnapshot(
    Guid CustomerId,
    string AccountReference,
    DateTimeOffset EvaluatedAt,
    int RiskProfileVersion,
    int RiskProfileScore,
    CustomerRiskBand RiskBand,
    DateTimeOffset RiskProfileUpdatedAt,
    decimal Cash,
    decimal InvestedMarketValue,
    decimal TotalMarketValue,
    decimal GrossExposureFraction,
    decimal LargestPositionFraction,
    decimal LargestSectorFraction,
    int PositionCount,
    int SectorCount,
    CustomerRiskConcentrationPosture ConcentrationPosture,
    CustomerRiskCenterEvidenceBasis EvidenceBasis,
    IReadOnlyList<CustomerRiskCenterPositionExposure> Positions,
    IReadOnlyList<CustomerRiskCenterSectorExposure> Sectors,
    IReadOnlyList<string> Signals,
    CustomerRiskCenterAuthority Authority);
