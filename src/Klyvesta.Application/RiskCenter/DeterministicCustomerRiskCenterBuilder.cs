using Klyvesta.Domain.Customers;
using Klyvesta.Domain.Portfolios;
using Klyvesta.Domain.Risk;
using Klyvesta.Domain.RiskCenter;

namespace Klyvesta.Application.RiskCenter;

public sealed record CustomerRiskCenterRequest(
    Guid AuthenticatedCustomerId,
    Guid CustomerId,
    string AccountReference,
    DateTimeOffset EvaluatedAt,
    TimeSpan MaximumRiskProfileAge,
    TimeSpan MaximumRiskContextAge,
    CustomerRiskProfile RiskProfile,
    PortfolioProjectionSnapshot Portfolio,
    RiskPortfolioSnapshot RiskContext);

public interface ICustomerRiskCenterBuilder
{
    CustomerRiskCenterSnapshot Build(CustomerRiskCenterRequest request);
}

public sealed class DeterministicCustomerRiskCenterBuilder : ICustomerRiskCenterBuilder
{
    private const decimal PositionWatchFraction = 0.35m;
    private const decimal PositionElevatedFraction = 0.50m;
    private const decimal SectorWatchFraction = 0.50m;
    private const decimal SectorElevatedFraction = 0.70m;

    public CustomerRiskCenterSnapshot Build(CustomerRiskCenterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureCustomerScope(request.AuthenticatedCustomerId, request.CustomerId);

        var accountReference = RequireText(request.AccountReference, "Account reference");
        if (request.EvaluatedAt == default)
        {
            throw new ArgumentException("Risk-center evaluation time is required.", nameof(request));
        }

        if (request.MaximumRiskProfileAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Maximum risk-profile age must be positive.");
        }

        if (request.MaximumRiskContextAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Maximum risk-context age must be positive.");
        }

        ArgumentNullException.ThrowIfNull(request.RiskProfile);
        ArgumentNullException.ThrowIfNull(request.Portfolio);
        ArgumentNullException.ThrowIfNull(request.RiskContext);

        ValidateRiskProfile(
            request.RiskProfile,
            request.CustomerId,
            request.EvaluatedAt,
            request.MaximumRiskProfileAge);
        EnsureAccountScope(
            request.Portfolio.AccountReference,
            accountReference,
            "CUSTOMER_RISK_CENTER_PORTFOLIO_ACCOUNT_SCOPE_MISMATCH");
        EnsureAccountScope(
            request.RiskContext.AccountReference,
            accountReference,
            "CUSTOMER_RISK_CENTER_RISK_ACCOUNT_SCOPE_MISMATCH");

        var portfolioByInstrument = ValidatePortfolio(request.Portfolio);
        var riskByInstrument = ValidateRiskContext(
            request.RiskContext,
            request.EvaluatedAt,
            request.MaximumRiskContextAge);

        if (request.Portfolio.Cash != request.RiskContext.Cash)
        {
            throw new InvalidOperationException("CUSTOMER_RISK_CENTER_CASH_CONTEXT_MISMATCH");
        }

        if (portfolioByInstrument.Count != riskByInstrument.Count)
        {
            throw new InvalidOperationException("CUSTOMER_RISK_CENTER_POSITION_SET_MISMATCH");
        }

        var positionExposures = new List<CustomerRiskCenterPositionExposure>(riskByInstrument.Count);
        var sectorValues = new Dictionary<string, decimal>(StringComparer.Ordinal);
        decimal investedMarketValue = 0m;

        foreach (var pair in portfolioByInstrument.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!riskByInstrument.TryGetValue(pair.Key, out var riskPosition))
            {
                throw new InvalidOperationException("CUSTOMER_RISK_CENTER_POSITION_SET_MISMATCH");
            }

            if (pair.Value.Quantity != riskPosition.Quantity)
            {
                throw new InvalidOperationException("CUSTOMER_RISK_CENTER_POSITION_QUANTITY_MISMATCH");
            }

            var marketValue = riskPosition.Quantity * riskPosition.MarketPrice;
            investedMarketValue += marketValue;
            sectorValues.TryGetValue(riskPosition.SectorReference, out var sectorValue);
            sectorValues[riskPosition.SectorReference] = sectorValue + marketValue;

            positionExposures.Add(
                new CustomerRiskCenterPositionExposure(
                    pair.Key,
                    riskPosition.SectorReference,
                    marketValue,
                    FractionOfTotalValue: 0m));
        }

        foreach (var instrument in riskByInstrument.Keys)
        {
            if (!portfolioByInstrument.ContainsKey(instrument))
            {
                throw new InvalidOperationException("CUSTOMER_RISK_CENTER_POSITION_SET_MISMATCH");
            }
        }

        var totalMarketValue = request.Portfolio.Cash + investedMarketValue;
        var grossExposureFraction = Fraction(investedMarketValue, totalMarketValue);

        var normalizedPositions = positionExposures
            .Select(position => position with
            {
                FractionOfTotalValue = Fraction(position.MarketValue, totalMarketValue),
            })
            .ToArray();

        var sectorExposures = sectorValues
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new CustomerRiskCenterSectorExposure(
                pair.Key,
                pair.Value,
                Fraction(pair.Value, totalMarketValue)))
            .ToArray();

        var largestPositionFraction = normalizedPositions.Length == 0
            ? 0m
            : normalizedPositions.Max(static position => position.FractionOfTotalValue);
        var largestSectorFraction = sectorExposures.Length == 0
            ? 0m
            : sectorExposures.Max(static sector => sector.FractionOfTotalValue);

        var signals = BuildSignals(largestPositionFraction, largestSectorFraction);
        var posture = DeterminePosture(largestPositionFraction, largestSectorFraction);

        return new CustomerRiskCenterSnapshot(
            request.CustomerId,
            accountReference,
            request.EvaluatedAt,
            request.RiskProfile.Version,
            request.RiskProfile.Score,
            request.RiskProfile.RiskBand,
            request.RiskProfile.UpdatedAt,
            request.Portfolio.Cash,
            investedMarketValue,
            totalMarketValue,
            grossExposureFraction,
            largestPositionFraction,
            largestSectorFraction,
            normalizedPositions.Length,
            sectorExposures.Length,
            posture,
            CustomerRiskCenterEvidenceBasis.CustomerRiskProfileAndExistingPortfolioRiskContext,
            normalizedPositions,
            sectorExposures,
            signals,
            CustomerRiskCenterAuthority.Informational);
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
            throw new InvalidOperationException("CUSTOMER_RISK_CENTER_SCOPE_MISMATCH");
        }
    }

    private static void ValidateRiskProfile(
        CustomerRiskProfile riskProfile,
        Guid customerId,
        DateTimeOffset evaluatedAt,
        TimeSpan maximumAge)
    {
        if (riskProfile.CustomerId != customerId)
        {
            throw new InvalidOperationException("CUSTOMER_RISK_CENTER_RISK_PROFILE_SCOPE_MISMATCH");
        }

        if (riskProfile.UpdatedAt == default ||
            riskProfile.UpdatedAt > evaluatedAt ||
            evaluatedAt - riskProfile.UpdatedAt > maximumAge)
        {
            throw new InvalidOperationException("CUSTOMER_RISK_CENTER_RISK_PROFILE_STALE_OR_FUTURE");
        }

        if (riskProfile.RiskBand == CustomerRiskBand.Unknown ||
            riskProfile.Score is < 0 or > 100 ||
            riskProfile.Version < 1)
        {
            throw new InvalidOperationException("CUSTOMER_RISK_CENTER_RISK_PROFILE_INVALID");
        }
    }

    private static Dictionary<string, ProjectedPosition> ValidatePortfolio(PortfolioProjectionSnapshot portfolio)
    {
        ArgumentNullException.ThrowIfNull(portfolio.Positions);

        if (portfolio.Cash < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(portfolio), "Paper portfolio cash cannot be negative.");
        }

        if (portfolio.LastSequence < -1 ||
            portfolio.UniqueSourceEventCount < 0 ||
            portfolio.UniqueExecutionCount < 0 ||
            portfolio.UniqueExecutionCount > portfolio.UniqueSourceEventCount)
        {
            throw new InvalidOperationException("CUSTOMER_RISK_CENTER_PORTFOLIO_COUNTERS_INVALID");
        }

        var result = new Dictionary<string, ProjectedPosition>(StringComparer.Ordinal);
        foreach (var position in portfolio.Positions)
        {
            var instrument = RequireText(position.InstrumentReference, "Portfolio instrument reference");
            if (!result.TryAdd(instrument, position))
            {
                throw new InvalidOperationException("CUSTOMER_RISK_CENTER_PORTFOLIO_DUPLICATE_INSTRUMENT");
            }

            if (position.Quantity <= 0m || position.AverageCost <= 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(portfolio),
                    "Paper portfolio position quantity and average cost must be positive.");
            }
        }

        return result;
    }

    private static Dictionary<string, NormalizedRiskPosition> ValidateRiskContext(
        RiskPortfolioSnapshot riskContext,
        DateTimeOffset evaluatedAt,
        TimeSpan maximumAge)
    {
        ArgumentNullException.ThrowIfNull(riskContext.Positions);

        if (riskContext.Cash < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(riskContext), "Risk-context cash cannot be negative.");
        }

        var result = new Dictionary<string, NormalizedRiskPosition>(StringComparer.Ordinal);
        foreach (var position in riskContext.Positions)
        {
            var instrument = RequireText(position.InstrumentReference, "Risk instrument reference");
            var sector = RequireText(position.SectorReference, "Risk sector reference");

            if (position.Quantity <= 0m || position.MarketPrice <= 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(riskContext),
                    "Risk position quantity and market price must be positive.");
            }

            if (position.MarketObservedAt == default ||
                position.MarketObservedAt > evaluatedAt ||
                evaluatedAt - position.MarketObservedAt > maximumAge)
            {
                throw new InvalidOperationException("CUSTOMER_RISK_CENTER_RISK_CONTEXT_STALE_OR_FUTURE");
            }

            if (!result.TryAdd(
                    instrument,
                    new NormalizedRiskPosition(
                        instrument,
                        sector,
                        position.Quantity,
                        position.MarketPrice,
                        position.MarketObservedAt)))
            {
                throw new InvalidOperationException("CUSTOMER_RISK_CENTER_RISK_DUPLICATE_INSTRUMENT");
            }
        }

        return result;
    }

    private static IReadOnlyList<string> BuildSignals(
        decimal largestPositionFraction,
        decimal largestSectorFraction)
    {
        var signals = new List<string>();

        if (largestPositionFraction >= PositionElevatedFraction)
        {
            signals.Add("RISK_CENTER_POSITION_CONCENTRATION_ELEVATED");
        }
        else if (largestPositionFraction >= PositionWatchFraction)
        {
            signals.Add("RISK_CENTER_POSITION_CONCENTRATION_WATCH");
        }

        if (largestSectorFraction >= SectorElevatedFraction)
        {
            signals.Add("RISK_CENTER_SECTOR_CONCENTRATION_ELEVATED");
        }
        else if (largestSectorFraction >= SectorWatchFraction)
        {
            signals.Add("RISK_CENTER_SECTOR_CONCENTRATION_WATCH");
        }

        signals.Sort(StringComparer.Ordinal);
        return signals.AsReadOnly();
    }

    private static CustomerRiskConcentrationPosture DeterminePosture(
        decimal largestPositionFraction,
        decimal largestSectorFraction)
    {
        if (largestPositionFraction >= PositionElevatedFraction ||
            largestSectorFraction >= SectorElevatedFraction)
        {
            return CustomerRiskConcentrationPosture.Elevated;
        }

        if (largestPositionFraction >= PositionWatchFraction ||
            largestSectorFraction >= SectorWatchFraction)
        {
            return CustomerRiskConcentrationPosture.Watch;
        }

        return CustomerRiskConcentrationPosture.Clear;
    }

    private static decimal Fraction(decimal numerator, decimal denominator) =>
        denominator == 0m ? 0m : numerator / denominator;

    private static void EnsureAccountScope(
        string sourceAccountReference,
        string requestedAccountReference,
        string errorCode)
    {
        if (!StringComparer.Ordinal.Equals(
                RequireText(sourceAccountReference, "Source account reference"),
                requestedAccountReference))
        {
            throw new InvalidOperationException(errorCode);
        }
    }

    private static string RequireText(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{label} is required.");
        }

        return value.Trim();
    }

    private sealed record NormalizedRiskPosition(
        string InstrumentReference,
        string SectorReference,
        decimal Quantity,
        decimal MarketPrice,
        DateTimeOffset MarketObservedAt);
}
