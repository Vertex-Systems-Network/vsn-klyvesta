using Klyvesta.Domain.Customers;
using Klyvesta.Domain.Planning;
using Klyvesta.Domain.Portfolios;

namespace Klyvesta.Application.Planning;

public sealed record CustomerPlanningRequest(
    Guid AuthenticatedCustomerId,
    Guid CustomerId,
    string AccountReference,
    DateOnly AsOfDate,
    CustomerProfile Profile,
    CustomerGoal Goal,
    CustomerRiskProfile RiskProfile,
    PortfolioProjectionSnapshot Portfolio);

public interface ICustomerPlanningEngine
{
    CustomerGoalPlan Build(CustomerPlanningRequest request);
}

public sealed class DeterministicCustomerPlanningEngine : ICustomerPlanningEngine
{
    public CustomerGoalPlan Build(CustomerPlanningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureCustomerScope(request.AuthenticatedCustomerId, request.CustomerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AccountReference);
        ArgumentNullException.ThrowIfNull(request.Profile);
        ArgumentNullException.ThrowIfNull(request.Goal);
        ArgumentNullException.ThrowIfNull(request.RiskProfile);
        ArgumentNullException.ThrowIfNull(request.Portfolio);

        EnsureOwnedByCustomer(request.Profile.CustomerId, request.CustomerId, "CUSTOMER_PLANNING_PROFILE_SCOPE_MISMATCH");
        EnsureOwnedByCustomer(request.Goal.CustomerId, request.CustomerId, "CUSTOMER_PLANNING_GOAL_SCOPE_MISMATCH");
        EnsureOwnedByCustomer(request.RiskProfile.CustomerId, request.CustomerId, "CUSTOMER_PLANNING_RISK_SCOPE_MISMATCH");

        if (request.Goal.Status != CustomerGoalStatus.Active)
        {
            throw new InvalidOperationException("CUSTOMER_PLANNING_GOAL_NOT_ACTIVE");
        }

        if (request.AsOfDate == default)
        {
            throw new ArgumentException("Planning as-of date is required.", nameof(request));
        }

        if (request.Goal.TargetDate <= request.AsOfDate)
        {
            throw new InvalidOperationException("CUSTOMER_PLANNING_TARGET_DATE_NOT_FUTURE");
        }

        if (request.RiskProfile.RiskBand == CustomerRiskBand.Unknown)
        {
            throw new InvalidOperationException("CUSTOMER_PLANNING_RISK_BAND_UNKNOWN");
        }

        var accountReference = request.AccountReference.Trim();
        var paperPortfolioBookValue = CalculatePaperBookValue(request.Portfolio, accountReference);
        var contributionPeriods = CalculateContributionPeriods(request.AsOfDate, request.Goal.TargetDate);
        var remainingAmount = Math.Max(0m, request.Goal.TargetAmount - request.Goal.CurrentAmount);
        var requiredMonthlyContribution = remainingAmount == 0m
            ? 0m
            : RoundUpToCents(remainingAmount / contributionPeriods);
        var contributionGap = Math.Max(0m, requiredMonthlyContribution - request.Profile.MonthlyContribution);

        return new CustomerGoalPlan(
            request.CustomerId,
            request.Goal.GoalId,
            request.AsOfDate,
            request.Goal.TargetDate,
            contributionPeriods,
            request.Goal.TargetAmount,
            request.Goal.CurrentAmount,
            remainingAmount,
            request.Profile.MonthlyContribution,
            requiredMonthlyContribution,
            contributionGap,
            paperPortfolioBookValue,
            request.RiskProfile.RiskBand,
            CustomerPlanningProjectionBasis.ZeroReturnArithmetic,
            CustomerPlanningAuthority.Informational);
    }

    private static void EnsureCustomerScope(Guid authenticatedCustomerId, Guid customerId)
    {
        if (authenticatedCustomerId == Guid.Empty)
        {
            throw new ArgumentException("Authenticated customer ID is required.", nameof(authenticatedCustomerId));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer ID is required.", nameof(customerId));
        }

        if (authenticatedCustomerId != customerId)
        {
            throw new InvalidOperationException("CUSTOMER_PLANNING_SCOPE_MISMATCH");
        }
    }

    private static void EnsureOwnedByCustomer(Guid ownerCustomerId, Guid customerId, string errorCode)
    {
        if (ownerCustomerId != customerId)
        {
            throw new InvalidOperationException(errorCode);
        }
    }

    private static int CalculateContributionPeriods(DateOnly asOfDate, DateOnly targetDate)
    {
        var periods = checked(((targetDate.Year - asOfDate.Year) * 12) + targetDate.Month - asOfDate.Month);
        if (targetDate.Day > asOfDate.Day)
        {
            periods = checked(periods + 1);
        }

        if (periods < 1)
        {
            throw new InvalidOperationException("CUSTOMER_PLANNING_CONTRIBUTION_PERIOD_INVALID");
        }

        return periods;
    }

    private static decimal CalculatePaperBookValue(
        PortfolioProjectionSnapshot portfolio,
        string requestedAccountReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portfolio.AccountReference);
        ArgumentNullException.ThrowIfNull(portfolio.Positions);
        if (!StringComparer.Ordinal.Equals(portfolio.AccountReference.Trim(), requestedAccountReference))
        {
            throw new InvalidOperationException("CUSTOMER_PLANNING_ACCOUNT_SCOPE_MISMATCH");
        }

        if (portfolio.Cash < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(portfolio), "Paper portfolio cash cannot be negative.");
        }

        if (portfolio.LastSequence < -1 || portfolio.UniqueSourceEventCount < 0 || portfolio.UniqueExecutionCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(portfolio), "Portfolio projection counters are invalid.");
        }

        if (portfolio.UniqueExecutionCount > portfolio.UniqueSourceEventCount)
        {
            throw new InvalidOperationException("CUSTOMER_PLANNING_EXECUTION_COUNT_INVALID");
        }

        var instruments = new HashSet<string>(StringComparer.Ordinal);
        var investedCostBasis = 0m;
        foreach (var position in portfolio.Positions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(position.InstrumentReference);
            if (position.Quantity <= 0m || position.AverageCost <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(portfolio), "Paper planning positions require positive quantity and average cost.");
            }

            var instrumentReference = position.InstrumentReference.Trim();
            if (!instruments.Add(instrumentReference))
            {
                throw new InvalidOperationException("CUSTOMER_PLANNING_DUPLICATE_INSTRUMENT");
            }

            investedCostBasis = checked(investedCostBasis + checked(position.Quantity * position.AverageCost));
        }

        return checked(portfolio.Cash + investedCostBasis);
    }

    private static decimal RoundUpToCents(decimal amount) =>
        Math.Ceiling(checked(amount * 100m)) / 100m;
}
