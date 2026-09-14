using Klyvesta.Domain.Customers;

namespace Klyvesta.Domain.Planning;

public enum CustomerPlanningProjectionBasis
{
    ZeroReturnArithmetic = 1,
}

public sealed record CustomerPlanningAuthority(
    bool InformationalOnly,
    bool CanRecommendInvestments,
    bool CanPlaceOrders,
    bool CanMoveMoney,
    bool CanOverrideRisk)
{
    public static CustomerPlanningAuthority Informational { get; } = new(
        InformationalOnly: true,
        CanRecommendInvestments: false,
        CanPlaceOrders: false,
        CanMoveMoney: false,
        CanOverrideRisk: false);
}

public sealed record CustomerGoalPlan(
    Guid CustomerId,
    Guid GoalId,
    DateOnly AsOfDate,
    DateOnly TargetDate,
    int ContributionPeriods,
    decimal TargetAmount,
    decimal CurrentGoalAmount,
    decimal RemainingAmount,
    decimal PlannedMonthlyContribution,
    decimal RequiredMonthlyContribution,
    decimal AdditionalMonthlyContributionGap,
    decimal PaperPortfolioBookValue,
    CustomerRiskBand RiskBand,
    CustomerPlanningProjectionBasis ProjectionBasis,
    CustomerPlanningAuthority Authority);
